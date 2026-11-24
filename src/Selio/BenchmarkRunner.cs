using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Selio;

internal static class BenchmarkRunner
{
    private static Rectangle PointAtFixture(FixtureForm fixture)
    {
        var screen = Screen.FromControl(fixture);
        Point origin = fixture.PointToScreen(new Point(28, 30));
        Cursor.Position = new Point(origin.X + 10, origin.Y + 10);
        return new Rectangle(origin.X - screen.Bounds.X, origin.Y - screen.Bounds.Y, 800, 185);
    }

    private static string Normalize(string text) => new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    public static async Task RunAsync(SelioContext context, string output)
    {
        Directory.CreateDirectory(output);
        var invariant = CultureInfo.InvariantCulture;
        void Phase(string name) => File.WriteAllText(Path.Combine(output, "phase.json"), JsonSerializer.Serialize(new { phase = name, time = DateTimeOffset.Now }));
        var rows = new List<string> { "cycle,phase,popup_ms,ocr_ms,total_ms,working_set_bytes,private_bytes,gdi_objects,user_objects,success" };
        try
        {
            Phase("stabilisation_initiale");
            await Task.Delay(TimeSpan.FromSeconds(15));
            Phase("repos");
            await Task.Delay(TimeSpan.FromMinutes(5));
            using var fixture = new FixtureForm { TopMost = true };
            fixture.Show();
            fixture.Activate();
            await Task.Delay(500);
            for (int cycle = 1; cycle <= 50; cycle++)
            {
                string phase = cycle <= 20 ? "usage" : "stabilite";
                if (cycle == 1 || cycle == 21) Phase(phase);
                fixture.Activate();
                var region = PointAtFixture(fixture);
                var total = Stopwatch.StartNew();
                var open = Stopwatch.StartNew();
                context.ShowHome();
                context.Popup.Refresh();
                NativeMethods.DwmFlush();
                double popup = open.Elapsed.TotalMilliseconds;
                await Task.Delay(60);
                bool success = await context.ExecuteAsync(CaptureAction.TextRegion, region);
                var text = context.Popup.CopyText;
                success &= Normalize(text) == Normalize(string.Join(' ', FixtureForm.Lines.Take(3)));
                if (success)
                {
                    await context.CopyTextAsync();
                    success = Clipboard.GetText() == text;
                }
                context.Popup.Dismiss();
                using var process = Process.GetCurrentProcess(); process.Refresh();
                rows.Add(string.Join(',', cycle, phase, popup.ToString("F3", invariant), context.LastOcrMilliseconds.ToString("F3", invariant),
                    total.Elapsed.TotalMilliseconds.ToString("F3", invariant), process.WorkingSet64, process.PrivateMemorySize64,
                    NativeMethods.GetGuiResources(process.Handle, 0), NativeMethods.GetGuiResources(process.Handle, 1), success));
                await Task.Delay(120);
                if (cycle == 20)
                {
                    fixture.Hide(); Phase("repos_apres_20"); await Task.Delay(TimeSpan.FromSeconds(30)); fixture.Show();
                }
            }
            fixture.Close();
            Phase("repos_apres_50");
            await Task.Delay(TimeSpan.FromSeconds(30));
            await File.WriteAllLinesAsync(Path.Combine(output, "cycles.csv"), rows);
            Phase("termine");
        }
        catch (Exception ex)
        {
            await File.WriteAllLinesAsync(Path.Combine(output, "cycles.csv"), rows);
            await File.WriteAllTextAsync(Path.Combine(output, "error.txt"), ex.Message);
            Phase("erreur");
        }
    }
}
