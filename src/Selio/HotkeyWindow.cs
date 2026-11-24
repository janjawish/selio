using Selio.Core;

namespace Selio;

internal sealed class HotkeyWindow : NativeWindow, IDisposable
{
    internal const string WindowTitle = "Selio.MessageWindow.v1";
    private int _id = 100;
    private Hotkey? _current;
    public event Action? Invoked;
    public event Action? ShowRequested;
    public event Action? QuitRequested;

    public HotkeyWindow()
    {
        // Hidden top-level window permits discovery by a second instance; no polling or pipe thread.
        CreateHandle(new CreateParams { Caption = WindowTitle, ExStyle = NativeMethods.WsExToolWindow });
    }

    public bool TryRegister(Hotkey shortcut)
    {
        if (shortcut.Validate() != null) return false;
        if (_current == shortcut) return true;
        int nextId = _id == 100 ? 101 : 100;
        if (!NativeMethods.RegisterHotKey(Handle, nextId, shortcut.Modifiers | 0x4000, shortcut.Key)) return false;
        if (_current != null) NativeMethods.UnregisterHotKey(Handle, _id);
        _id = nextId;
        _current = shortcut;
        return true;
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == NativeMethods.WmHotkey) { Invoked?.Invoke(); return; }
        if (m.Msg == NativeMethods.WmAppShow) { ShowRequested?.Invoke(); return; }
        if (m.Msg == NativeMethods.WmAppQuit) { QuitRequested?.Invoke(); return; }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        if (_current != null) NativeMethods.UnregisterHotKey(Handle, _id);
        _current = null;
        DestroyHandle();
        GC.SuppressFinalize(this);
    }
}
