using System.Drawing;
using System.Text.Json;
using Selio.Core;

var tests = new List<(string Name, Action Test)>();
void Test(string name, Action action) => tests.Add((name, action));
void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Attendu : {expected} ; obtenu : {actual}");
}
void True(bool value) { if (!value) throw new Exception("Condition non satisfaite."); }
RecognizedWord W(string text, int line, int index, float x, float y) => new(text, new(x, y, 30, 15), line, index);
var words = new[] { W("Bonjour", 0, 0, 10, 10), W("le", 0, 1, 45, 10), W("monde.", 0, 2, 80, 10), W("Seconde", 1, 0, 10, 35), W("ligne", 1, 1, 45, 35) };
var document = new OcrDocument(words);

Test("OCR : ordre de lecture stable", () => Equal("Bonjour le monde." + Environment.NewLine + "Seconde ligne", new OcrDocument(words.Reverse()).Text));
Test("OCR : espaces et sauts de ligne", () => Equal("Bonjour le monde." + Environment.NewLine + "Seconde ligne", document.Text));
Test("OCR : ponctuation séparée", () => Equal("Bonjour, monde.", OcrDocument.Reconstruct([W("Bonjour", 0, 0, 0, 0), W(",", 0, 1, 0, 0), W("monde", 0, 2, 0, 0), W(".", 0, 3, 0, 0)])));
Test("OCR : apostrophe française", () => Equal("l’écran", OcrDocument.Reconstruct([W("l’", 0, 0, 0, 0), W("écran", 0, 1, 0, 0)])));
Test("OCR : parenthèses", () => Equal("(texte)", OcrDocument.Reconstruct([W("(", 0, 0, 0, 0), W("texte", 0, 1, 0, 0), W(")", 0, 2, 0, 0)])));
Test("OCR : texte vide", () => Equal("", new OcrDocument([]).Text));
Test("OCR : mots vides ignorés", () => Equal("Bonjour", OcrDocument.Reconstruct([W("", 0, 0, 0, 0), W("Bonjour", 0, 1, 0, 0)])));
Test("Sélection : clic exact", () => Equal(1, document.HitTest(new PointF(50, 15))));
Test("Sélection : tolérance", () => Equal(0, document.HitTest(new PointF(8, 12), 3)));
Test("Sélection : vide éloigné", () => Equal(-1, document.HitTest(new PointF(400, 400))));
Test("Sélection : plusieurs lignes", () => Equal("le monde." + Environment.NewLine + "Seconde", OcrDocument.Reconstruct(document.SelectRange(1, 3))));
Test("Sélection : glisser inversé", () => Equal("le monde." + Environment.NewLine + "Seconde", OcrDocument.Reconstruct(document.SelectRange(3, 1))));
Test("Sélection : un mot", () => Equal("monde.", OcrDocument.Reconstruct(document.SelectRange(2, 2))));
Test("Sélection : ancre invalide", () => Equal(0, document.SelectRange(-1, 2).Count));
Test("Sélection : fin invalide", () => Equal(0, document.SelectRange(0, 5).Count));
Test("Sélection : mot le plus proche", () => Equal(4, document.NearestWord(new PointF(49, 100))));
Test("Sélection : document vide", () => Equal(-1, new OcrDocument([]).NearestWord(new PointF(0, 0))));
Test("Géométrie : rectangle normal", () => Equal(new Rectangle(10, 20, 90, 180), Geometry.DragRectangle(new(10, 20), new(100, 200), new(1920, 1080))));
Test("Géométrie : rectangle inversé", () => Equal(new Rectangle(10, 20, 90, 180), Geometry.DragRectangle(new(100, 200), new(10, 20), new(1920, 1080))));
Test("Géométrie : limites négatives", () => Equal(new Rectangle(0, 0, 100, 200), Geometry.DragRectangle(new(-40, -20), new(100, 200), new(1920, 1080))));
Test("Géométrie : débordement", () => Equal(new Rectangle(1900, 1000, 20, 80), Geometry.DragRectangle(new(1900, 1000), new(3000, 2000), new(1920, 1080))));
Test("Géométrie : rectangle nul", () => Equal(Size.Empty, Geometry.DragRectangle(new(20, 20), new(20, 20), new(1920, 1080)).Size));
Test("Moniteur : origine négative", () => Equal(new Point(100, 250), Geometry.ScreenToCapture(new(-1820, 150), new(-1920, -100, 1920, 1080))));
Test("Moniteur : coordonnées écran", () => Equal(new Point(-1820, 150), Geometry.CaptureToScreen(new(100, 250), new(-1920, -100, 1920, 1080))));
foreach (int dpi in new[] { 96, 120, 144, 168, 192, 240, 288 })
{
    int local = dpi;
    Test($"DPI {dpi} : aller-retour", () => True(Math.Abs(Geometry.PixelToDip(Geometry.DipToPixel(460, local), local) - 460) <= 0.5));
    Test($"DPI {dpi} : popup sur écran négatif", () =>
    {
        var work = new Rectangle(-3840, -100, 3840, 2100);
        var placed = Geometry.PlacePopup(work, new(Geometry.DipToPixel(460, local), Geometry.DipToPixel(344, local)), local);
        True(work.Contains(placed));
        Equal(work.Left + (work.Width - placed.Width) / 2, placed.Left);
    });
}
Test("Popup : petit espace de travail", () => Equal(new Rectangle(-200, 50, 200, 150), Geometry.PlacePopup(new(-200, 50, 200, 150), new(460, 344), 96)));
Test("OCR 4K : dimension maximale", () => Equal(new Size(2600, 1462), ImageScale.Fit(new(3840, 2160), 2600).Target));
Test("OCR : aucune mise à l'échelle inutile", () => Equal(new Size(1280, 720), ImageScale.Fit(new(1280, 720), 2600).Target));
Test("OCR : remappage des bords après arrondi", () =>
{
    var scale = ImageScale.Fit(new(3840, 2160), 2600);
    var mapped = scale.ToSource(new(0, 0, scale.Target.Width, scale.Target.Height));
    Equal(new RectangleF(0, 0, 3840, 2160), mapped);
});
Test("OCR : portrait 4K", () => Equal(new Size(1462, 2600), ImageScale.Fit(new(2160, 3840), 2600).Target));
Test("OCR : image invalide", () =>
{
    try { ImageScale.Fit(new(0, 200), 2600); throw new Exception("Exception attendue."); }
    catch (ArgumentOutOfRangeException) { }
});
Test("Géométrie : 2000 rectangles aléatoires restent bornés", () =>
{
    var random = new Random(617);
    for (int i = 0; i < 2000; i++)
    {
        var rect = Geometry.DragRectangle(new(random.Next(-400, 5000), random.Next(-400, 3000)), new(random.Next(-400, 5000), random.Next(-400, 3000)), new(3840, 2160));
        True(rect.Left >= 0 && rect.Top >= 0 && rect.Right <= 3840 && rect.Bottom <= 2160 && rect.Width >= 0 && rect.Height >= 0);
    }
});
Test("Paramètres : valeurs par défaut", () => { var s = new AppSettings(); Equal<string?>(null, s.Validate()); True(s.StartWithWindows); True(s.ShowAfterCopy); Equal("Ctrl + Maj + O", s.Shortcut.ToString()); });
Test("Paramètres : raccourci sans modificateur", () => True(new Hotkey(0, 0x4F).Validate() != null));
Test("Paramètres : Maj seule interdite", () => True(new Hotkey(4, 0x4F).Validate() != null));
Test("Paramètres : F12 réservée", () => True(new Hotkey(6, 0x7B).Validate() != null));
Test("Paramètres : touche Windows réservée", () => True(new Hotkey(8, 0x4F).Validate() != null));
Test("Paramètres : touche spéciale invalide", () => True(new Hotkey(6, 0x10).Validate() != null));
Test("Paramètres : Alt + F11", () => Equal<string?>(null, new Hotkey(1, 0x7A).Validate()));
Test("Paramètres : langue absente", () => True((new AppSettings { OcrLanguage = "en-US" }).Validate(["fr-FR"]) != null));
Test("Paramètres : langue disponible", () => Equal<string?>(null, (new AppSettings { OcrLanguage = "fr-FR" }).Validate(["fr-FR"])));
Test("Paramètres : langue mal formée", () => True((new AppSettings { OcrLanguage = "../secret" }).Validate() != null));
Test("Paramètres : version future conservée", () => True((new AppSettings { Version = 2 }).Validate() != null));
Test("Paramètres : raccourci JSON null", () => True(JsonSerializer.Deserialize<AppSettings>("{\"Shortcut\":null}")!.Validate() != null));

string temp = Path.Combine(AppContext.BaseDirectory, "Selio.Tests." + Guid.NewGuid().ToString("N"));
Test("JSON : écriture et relecture", () =>
{
    var store = new SettingsStore(temp);
    var expected = new AppSettings { StartWithWindows = false, OcrLanguage = "fr-FR", Shortcut = new Hotkey(3, 0x41) };
    store.Save(expected); Equal(expected, store.Load().Settings); True(store.Load().MaySave);
});
Test("JSON : fichier corrompu préservé", () =>
{
    var store = new SettingsStore(temp); File.WriteAllText(store.FilePath, "not JSON");
    var result = store.Load(); True(!result.MaySave); True(result.Warning != null); Equal("not JSON", File.ReadAllText(store.FilePath));
});
Test("JSON : absence de fichiers temporaires", () => Equal(0, Directory.GetFiles(temp, "*.tmp").Length));

int failed = 0;
foreach (var test in tests)
{
    try { test.Test(); Console.WriteLine($"PASS {test.Name}"); }
    catch (Exception ex) { failed++; Console.WriteLine($"FAIL {test.Name} : {ex}"); }
}
if (Directory.Exists(temp)) Directory.Delete(temp, true);
Console.WriteLine($"\n{tests.Count - failed}/{tests.Count} tests réussis ; {failed} échec(s).");
return failed == 0 ? 0 : 1;
