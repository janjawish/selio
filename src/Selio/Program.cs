namespace Selio;

internal static class Program
{
    internal static bool UiTestMode { get; private set; }
    [STAThread]
    private static int Main(string[] args)
    {
        UiTestMode = args.Contains("--ui-test");
        ApplicationConfiguration.Initialize();
        if (args.Contains("--fixture")) { Application.Run(new FixtureForm()); return 0; }
        if (args.Contains("--verify")) return Verification.Run(args);
        if (args.Contains("--quit"))
        {
            var existing = NativeMethods.FindWindow(null, HotkeyWindow.WindowTitle);
            if (existing != 0) NativeMethods.PostMessage(existing, NativeMethods.WmAppQuit, 0, 0);
            return 0;
        }
        using var mutex = new Mutex(true, @"Local\Selio.Instance.v1", out bool first);
        if (!first)
        {
            var window = NativeMethods.FindWindow(null, HotkeyWindow.WindowTitle);
            if (window != 0)
            {
                NativeMethods.GetWindowThreadProcessId(window, out uint process);
                NativeMethods.AllowSetForegroundWindow(process);
                NativeMethods.PostMessage(window, NativeMethods.WmAppShow, 0, 0);
            }
            return 0;
        }
        try
        {
            using var context = new SelioContext(args);
            Application.ThreadException += (_, e) => context.Recover(e.Exception);
            Application.Run(context);
            return 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Sélio n'a pas pu démarrer.\n\n{ex.Message}", "Sélio", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
        finally { mutex.ReleaseMutex(); }
    }
}
