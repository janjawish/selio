using System.Text.Json;

namespace Selio.Core;

public sealed record Hotkey(uint Modifiers = 6, uint Key = 0x4F)
{
    public string? Validate()
    {
        if ((Modifiers & ~7u) != 0 || (Modifiers & 3) == 0)
            return "Choisissez au moins Ctrl ou Alt, éventuellement avec Maj.";
        if (!(Key is >= 0x41 and <= 0x5A or >= 0x30 and <= 0x39 or >= 0x70 and <= 0x7A))
            return "Choisissez une lettre, un chiffre ou F1 à F11 (F12 est réservé).";
        return null;
    }

    public override string ToString()
    {
        var parts = new List<string>();
        if ((Modifiers & 2) != 0) parts.Add("Ctrl");
        if ((Modifiers & 1) != 0) parts.Add("Alt");
        if ((Modifiers & 4) != 0) parts.Add("Maj");
        parts.Add(Key >= 0x70 ? $"F{Key - 0x70 + 1}" : ((char)Key).ToString());
        return string.Join(" + ", parts);
    }
}

public sealed record AppSettings
{
    public int Version { get; init; } = 1;
    public Hotkey Shortcut { get; init; } = new();
    public string? OcrLanguage { get; init; }
    public bool StartWithWindows { get; init; } = true;
    public bool ShowAfterCopy { get; init; } = true;

    public string? Validate(IEnumerable<string>? availableLanguages = null)
    {
        if (Version != 1) return "Cette version du fichier de paramètres n'est pas prise en charge.";
        if (Shortcut is null) return "Le raccourci est manquant.";
        string? shortcutError = Shortcut.Validate();
        if (shortcutError != null) return shortcutError;
        if (OcrLanguage is { Length: > 80 } || OcrLanguage is { Length: 0 })
            return "La langue OCR est invalide.";
        if (OcrLanguage != null && !System.Text.RegularExpressions.Regex.IsMatch(OcrLanguage, "^[a-zA-Z]{2,3}(-[a-zA-Z0-9]{2,8})*$"))
            return "La langue OCR est invalide.";
        if (OcrLanguage != null && availableLanguages != null &&
            !availableLanguages.Contains(OcrLanguage, StringComparer.OrdinalIgnoreCase))
            return "Le module OCR de cette langue n'est pas installé dans Windows.";
        return null;
    }
}

public sealed class SettingsStore(string directory)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string FilePath { get; } = Path.Combine(directory, "settings.json");

    public (AppSettings Settings, string? Warning, bool MaySave) Load()
    {
        if (!File.Exists(FilePath)) return (new(), null, true);
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options);
            if (settings is null || settings.Validate() is { })
                return (new(), "Paramètres invalides : réglages temporaires par défaut. Le fichier existant est conservé.", false);
            return (settings, null, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return (new(), "Lecture des paramètres impossible. Le fichier existant est conservé.", false);
        }
    }

    public void Save(AppSettings settings)
    {
        if (settings.Validate() is { } error) throw new ArgumentException(error);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        string temporary = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
            File.Move(temporary, FilePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
