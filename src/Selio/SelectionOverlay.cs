using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Selio.Core;

namespace Selio;

internal sealed record SelectionResult(Rectangle? Region, string? Text);

/// <summary>
/// Color-keyed overlay: the desktop remains live and exactly unchanged outside the outlines.
/// Windows passes clicks through color-keyed pixels, so a short-lived mouse hook owns input
/// ONLY while this form is open. No keyboard hook, no screen bitmap, no timer.
/// </summary>
internal sealed class SelectionOverlay : Form
{
    private readonly Rectangle _monitor;
    private readonly OcrDocument? _document;
    private readonly TaskCompletionSource<SelectionResult?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly NativeMethods.HookProc _callback;
    private nint _hook;
    private readonly Matrix _textTransform = new();
    private readonly Matrix _inverseTransform = new();
    private Point? _start;
    private Point _current;
    private int _anchor = -1, _end = -1, _hover = -1;
    private bool _finishing;
    public Task<SelectionResult?> Completion => _completion.Task;

    public SelectionOverlay(Rectangle monitor, OcrDocument? document = null)
    {
        _monitor = monitor;
        _document = document;
        _callback = MouseHook;
        Text = document == null ? "Sélio — Sélectionner une zone · Échap pour annuler" : "Sélio — Sélectionner des mots · Échap pour annuler";
        AccessibleName = Text;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = Program.UiTestMode;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Bounds = monitor;
        BackColor = TransparencyKey = Color.Fuchsia;
        TopMost = true;
        DoubleBuffered = true;
        KeyPreview = true;
        Cursor = document == null ? Cursors.Cross : Cursors.IBeam;
        if (document?.Angle is { } angle && Math.Abs(angle) > 0.01)
        {
            _textTransform.RotateAt((float)angle, new PointF(monitor.Width / 2f, monitor.Height / 2f));
            _inverseTransform.RotateAt(-(float)angle, new PointF(monitor.Width / 2f, monitor.Height / 2f));
        }
        Deactivate += (_, _) => Finish(null);
    }

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; if (!Program.UiTestMode) cp.ExStyle |= NativeMethods.WsExToolWindow; return cp; }
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        Bounds = _monitor; // Exact native bounds, including negative origins.
        NativeMethods.ShowWindow(Handle, 5);
        NativeMethods.SetForegroundWindow(Handle);
        Activate();
        _hook = NativeMethods.SetWindowsHookEx(14, _callback, NativeMethods.GetModuleHandle(null), 0);
        if (_hook == 0)
        {
            _completion.TrySetException(new Win32Exception(Marshal.GetLastWin32Error(), "Impossible d'ouvrir la sélection de souris."));
            Finish(null);
        }
        else NativeMethods.SetCursor(Cursor.Handle);
    }

    private nint MouseHook(int code, nint message, nint data)
    {
        if (code < 0 || _finishing) return NativeMethods.CallNextHookEx(_hook, code, message, data);
        try
        {
            var mouse = Marshal.PtrToStructure<NativeMethods.MouseData>(data);
            var point = Geometry.ScreenToCapture(new Point(mouse.Point.X, mouse.Point.Y), _monitor);
            int kind = (int)message;
            NativeMethods.SetCursor(Cursor.Handle);
            // Queue work; never OCR, block, or perform clipboard work inside the native callback.
            if (kind is NativeMethods.WmMouseMove or NativeMethods.WmLeftDown or NativeMethods.WmLeftUp or NativeMethods.WmRightDown)
                BeginInvoke(() => HandleMouse(kind, point));
            return 1; // Suppress clicks/wheel in the background application during this interaction.
        }
        catch { Finish(null); return NativeMethods.CallNextHookEx(_hook, code, message, data); }
    }

    private PointF ToTextPoint(Point point)
    {
        PointF[] points = [point];
        _inverseTransform.TransformPoints(points);
        return points[0];
    }

    private void HandleMouse(int message, Point point)
    {
        if (_finishing) return;
        _current = point;
        if (message == NativeMethods.WmRightDown) { Finish(null); return; }
        if (message == NativeMethods.WmLeftDown)
        {
            if (!new Rectangle(Point.Empty, Size).Contains(point)) { Finish(null); return; }
            _start = point;
            if (_document != null) _anchor = _end = _document.HitTest(ToTextPoint(point), 6);
        }
        else if (message == NativeMethods.WmMouseMove && _document != null)
        {
            _hover = _document.HitTest(ToTextPoint(point), 4);
            if (_start.HasValue && _anchor >= 0) _end = _document.NearestWord(ToTextPoint(point));
        }
        else if (message == NativeMethods.WmLeftUp && _start.HasValue)
        {
            if (_document == null)
            {
                var rectangle = Geometry.DragRectangle(_start.Value, point, ClientSize);
                if (rectangle.Width >= 4 && rectangle.Height >= 4) { Finish(new(rectangle, null)); return; }
                System.Media.SystemSounds.Beep.Play();
            }
            else if (_anchor >= 0)
            {
                _end = _document.NearestWord(ToTextPoint(point));
                string text = OcrDocument.Reconstruct(_document.SelectRange(_anchor, _end));
                Finish(new(null, text));
                return;
            }
            _start = null;
        }
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        // No DrawImage, fill, opacity adjustment, or screenshot backdrop here.
        using var dark = new Pen(Theme.Background, 4);
        using var accent = new Pen(Theme.Coral, 2);
        if (_document == null)
        {
            if (_start is { } start)
            {
                var rectangle = Geometry.DragRectangle(start, _current, ClientSize);
                if (rectangle.Width > 0 && rectangle.Height > 0)
                {
                    e.Graphics.DrawRectangle(dark, rectangle);
                    e.Graphics.DrawRectangle(accent, rectangle);
                }
            }
        }
        else
        {
            e.Graphics.Transform = _textTransform;
            var selected = _document.SelectRange(_anchor, _end);
            if (selected.Count == 0 && _hover >= 0) selected = [_document.Words[_hover]];
            foreach (var word in selected)
            {
                var r = word.Bounds;
                r.Inflate(2, 2);
                e.Graphics.DrawRectangle(dark, r.X, r.Y, r.Width, r.Height);
                e.Graphics.DrawRectangle(accent, r.X, r.Y, r.Width, r.Height);
            }
        }
    }

    public void Finish(SelectionResult? result)
    {
        if (_finishing) return;
        _finishing = true;
        RemoveHook();
        _completion.TrySetResult(result);
        Close();
    }

    private void RemoveHook()
    {
        if (_hook != 0) { NativeMethods.UnhookWindowsHookEx(_hook); _hook = 0; }
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Finish(null); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg is 0x007E or 0x001F) { Finish(null); return; } // display changed / cancel mode
        base.WndProc(ref m);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        RemoveHook();
        _completion.TrySetResult(null);
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        RemoveHook();
        if (disposing) { _textTransform.Dispose(); _inverseTransform.Dispose(); }
        base.Dispose(disposing);
    }
}
