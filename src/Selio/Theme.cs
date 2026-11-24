using System.Drawing.Drawing2D;
using System.Reflection;
using System.ComponentModel;

namespace Selio;

internal static class Theme
{
    public static readonly Color Purple = ColorTranslator.FromHtml("#722F99");
    public static readonly Color Secondary = ColorTranslator.FromHtml("#C5C1C1");
    public static readonly Color Cream = ColorTranslator.FromHtml("#FEE7C8");
    public static readonly Color Coral = ColorTranslator.FromHtml("#FF9292");
    public static readonly Color Background = ColorTranslator.FromHtml("#241B29");
    public static readonly Color Surface = ColorTranslator.FromHtml("#34283A");
    public static readonly Color Border = ColorTranslator.FromHtml("#594761");
    public static readonly string FontFamily = ChooseFont();
    public static readonly Bitmap Logo = LoadLogo();
    public static readonly Icon Icon = LoadIcon();

    private static string ChooseFont()
    {
        using var probe = new Font("Segoe UI Variable", 10);
        return probe.Name.Equals("Segoe UI Variable", StringComparison.OrdinalIgnoreCase) ? probe.Name : "Segoe UI";
    }
    private static Bitmap LoadLogo()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Selio.Logo.png")!;
        using var image = new Bitmap(stream);
        return new Bitmap(image);
    }
    private static Icon LoadIcon()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Selio.Logo.ico")!;
        using var icon = new Icon(stream, 32, 32);
        return (Icon)icon.Clone();
    }

    public static GraphicsPath Round(RectangleF rect, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(rect.Left, rect.Top, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Top, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.Left, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void ApplyWindow(Form form)
    {
        int round = 2, dark = 1;
        NativeMethods.DwmSetWindowAttribute(form.Handle, 33, ref round, sizeof(int));
        NativeMethods.DwmSetWindowAttribute(form.Handle, 20, ref dark, sizeof(int));
    }

    public static Label Label(string text, float size = 10, bool bold = false) => new()
    {
        Text = text, AutoSize = false, ForeColor = Cream, BackColor = Color.Transparent,
        Font = new Font(FontFamily, size, bold ? FontStyle.Bold : FontStyle.Regular),
        UseMnemonic = false
    };
}

internal sealed class SelioButton : Button
{
    [DefaultValue("")] public string Subtitle { get; set; } = "";
    [DefaultValue(false)] public bool Primary { get; set; }
    private bool _hover;

    public SelioButton(string text, string subtitle = "")
    {
        Text = text;
        Subtitle = subtitle;
        AccessibleName = text;
        AccessibleDescription = subtitle;
        AccessibleRole = AccessibleRole.PushButton;
        Font = new Font(Theme.FontFamily, 10, FontStyle.Bold);
        Cursor = Cursors.Hand;
        UseMnemonic = false;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
    protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        float scale = DeviceDpi / 96f;
        e.Graphics.Clear(Theme.Background);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var shape = Theme.Round(new RectangleF(1, 1, Width - 3, Height - 3), 8 * scale);
        using var fill = new SolidBrush(Primary ? Theme.Purple : _hover ? Color.FromArgb(70, 53, 78) : Theme.Surface);
        using var border = new Pen(Focused ? Theme.Coral : _hover ? Theme.Secondary : Theme.Border, Focused ? 2 * scale : 1);
        e.Graphics.FillPath(fill, shape);
        e.Graphics.DrawPath(border, shape);
        int inset = (int)(13 * scale);
        Color foreground = Enabled ? Theme.Cream : Theme.Secondary;
        var flags = TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
        if (Subtitle.Length == 0)
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(inset, 0, Width - inset * 2, Height), foreground, flags);
        else
        {
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(inset, (int)(2 * scale), Width - inset * 2, Height / 2), foreground, flags);
            using var small = new Font(Font.FontFamily, Font.Size * 0.82f);
            TextRenderer.DrawText(e.Graphics, Subtitle, small, new Rectangle(inset, Height / 2 - (int)(1 * scale), Width - inset * 2, Height / 2), Theme.Secondary, flags);
        }
    }
}
