using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text.Json;
using Selio.Core;

namespace Selio;

internal static class Verification
{
    internal static int Run(string[] args)
    {
        int index = Array.IndexOf(args, "--verify");
        string output = index + 1 < args.Length ? args[index + 1] : Path.Combine(AppContext.BaseDirectory, "verification.json");
        int exit = 1;
        using var dispatcher = new Form();
        _ = dispatcher.Handle;
        using var context = new ApplicationContext();
        dispatcher.BeginInvoke(async () =>
        {
            try { exit = await RunAsync(output, args.Contains("--allow-clipboard-tests")); }
            finally { context.ExitThread(); }
        });
        Application.Run(context);
        return exit;
    }

    private static async Task<int> RunAsync(string output, bool allowClipboard)
    {
        var results = new List<object>();
        int failures = 0, skipped = 0;
        async Task Check(string name, Func<Task<string>> action)
        {
            var timer = Stopwatch.StartNew();
            try { var detail = await action(); results.Add(new { name, passed = true, detail, milliseconds = timer.Elapsed.TotalMilliseconds }); }
            catch (NotSupportedException ex) { skipped++; results.Add(new { name, passed = (bool?)null, detail = ex.Message, milliseconds = timer.Elapsed.TotalMilliseconds }); }
            catch (Exception ex) { failures++; results.Add(new { name, passed = false, detail = ex.Message, milliseconds = timer.Elapsed.TotalMilliseconds }); }
        }
        var languages = OcrService.Languages();
        await Check("Raccourci : conflit et conservation de l'ancien", () =>
        {
            using var first = new HotkeyWindow();
            using var second = new HotkeyWindow();
            var a = new Hotkey(7, 0x79); var b = new Hotkey(7, 0x78);
            if (!first.TryRegister(a) || !second.TryRegister(b)) throw new Exception("Raccourcis de test indisponibles.");
            if (second.TryRegister(a)) throw new Exception("Le conflit n'a pas été détecté.");
            using var third = new HotkeyWindow();
            if (third.TryRegister(b)) throw new Exception("L'ancien raccourci a été perdu.");
            return Task.FromResult("Deux raccourcis enregistrés ; conflit refusé sans perdre l'ancien.");
        });
        await Check("Presse-papiers texte persistant", async () =>
        {
            if (!allowClipboard) throw new NotSupportedException("Non exécuté : --allow-clipboard-tests requis (remplace le contenu actuel).");
            await ClipboardService.SetTextAsync("Sélio — vérification locale", CancellationToken.None);
            if (Clipboard.GetText() != "Sélio — vérification locale") throw new Exception("Contenu différent.");
            return "Unicode relu après SetDataObject(copy:true).";
        });
        await Check("Presse-papiers image après libération du bitmap", async () =>
        {
            if (!allowClipboard) throw new NotSupportedException("Non exécuté : --allow-clipboard-tests requis (remplace le contenu actuel).");
            using (var image = new Bitmap(123, 67, PixelFormat.Format32bppArgb))
            {
                using var g = Graphics.FromImage(image); g.Clear(Theme.Purple);
                await ClipboardService.SetImageAsync(image, CancellationToken.None);
            }
            using var pasted = Clipboard.GetImage();
            if (pasted?.Size != new Size(123, 67)) throw new Exception("Dimensions de l'image incorrectes.");
            return "Image de 123 × 67 pixels relue après Dispose.";
        });

        var cases = new[]
        {
            ("Français clair", "Bonjour le monde. Une sélection précise.", "fr", false, 28f, 1400, 280, "Bonjour"),
            ("Anglais clair", "Hello world. Select text from any image.", "en", false, 28f, 1400, 280, "Hello"),
            ("Français sombre", "Le texte reste sur votre ordinateur.", "fr", true, 28f, 1400, 280, "texte"),
            ("Petit texte", "Petit texte et accents : été déjà", "fr", false, 10f, 900, 150, "texte"),
            ("Code", "var total = 42; // local OCR", "en", false, 24f, 1200, 200, "total"),
            ("Image 8K redimensionnée", "Bonjour monde", "fr", false, 80f, 7680, 4320, "Bonjour")
        };
        foreach (var item in cases)
        {
            await Check("OCR : " + item.Item1, async () =>
            {
                string? lang = languages.FirstOrDefault(l => l.Tag.StartsWith(item.Item3))?.Tag;
                if (lang == null && item.Item1 == "Code") lang = languages.FirstOrDefault()?.Tag;
                if (lang == null) throw new NotSupportedException($"Module {item.Item3} absent : test non réalisable.");
                using var image = new Bitmap(item.Item6, item.Item7, PixelFormat.Format32bppArgb);
                using (var graphics = Graphics.FromImage(image))
                {
                    graphics.Clear(item.Item4 ? Theme.Background : Color.White);
                    using var font = new Font(item.Item1 == "Code" ? "Consolas" : "Segoe UI", item.Item5);
                    using var brush = new SolidBrush(item.Item4 ? Theme.Cream : Color.Black);
                    graphics.DrawString(item.Item2, font, brush, 30, 30);
                }
                var document = await OcrService.RecognizeAsync(image, lang, CancellationToken.None);
                if (!document.Text.Contains(item.Item8, StringComparison.OrdinalIgnoreCase)) throw new Exception("Mot de contrôle non reconnu : " + document.Text);
                if (document.Words.Any(w => w.Bounds.Left < 0 || w.Bounds.Right > image.Width + 1 || w.Bounds.Bottom > image.Height + 1))
                    throw new Exception("Coordonnées remappées hors image.");
                return $"Moteur {lang} : {document.Text}"; // Synthetic fixture text only, never a desktop capture.
            });
        }
        await Check("OCR : zone vide", async () =>
        {
            using var blank = new Bitmap(200, 100);
            using (var g = Graphics.FromImage(blank)) g.Clear(Color.White);
            var document = await OcrService.RecognizeAsync(blank, null, CancellationToken.None);
            if (document.Words.Count != 0) throw new Exception("La zone vide a produit du texte.");
            return "Aucun mot détecté.";
        });
        await Check("OCR : annulation", async () =>
        {
            using var blank = new Bitmap(200, 100);
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            try { await OcrService.RecognizeAsync(blank, null, cancellation.Token); }
            catch (OperationCanceledException) { return "Annulation propagée."; }
            throw new Exception("L'annulation a été ignorée.");
        });
        await Check("OCR : langue indisponible", async () =>
        {
            using var blank = new Bitmap(200, 100);
            try { await OcrService.RecognizeAsync(blank, "zu-ZA", CancellationToken.None); }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Module OCR")) { return "Message explicite sans téléchargement."; }
            throw new Exception("Langue de test présente ou absence non signalée.");
        });
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        await File.WriteAllTextAsync(output, JsonSerializer.Serialize(new
        {
            time = DateTimeOffset.Now, os = Environment.OSVersion.VersionString, runtime = Environment.Version.ToString(),
            languages, failures, skipped, tests = results, monitors = Screen.AllScreens.Select(s => new { s.DeviceName, s.Bounds, s.WorkingArea, dpi = NativeMethods.DpiAt(s.Bounds.Location) })
        }, new JsonSerializerOptions { WriteIndented = true }));
        return failures == 0 ? 0 : 1;
    }
}
