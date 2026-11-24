using Microsoft.Win32;

namespace Selio;

internal static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static void Apply(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, true)
            ?? throw new InvalidOperationException("Impossible d'accéder au démarrage Windows de votre compte.");
        if (enabled)
        {
            string path = Environment.ProcessPath ?? throw new InvalidOperationException("Chemin de Sélio introuvable.");
            if (!Path.GetFileName(path).Equals("Selio.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Lancez Selio.exe pour activer le démarrage Windows.");
            key.SetValue("Selio", $"\"{path}\" --startup", RegistryValueKind.String);
        }
        else key.DeleteValue("Selio", false);
    }
}
