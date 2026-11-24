namespace Selio;

/// <summary>Explicit test surface. Never opened during ordinary use.</summary>
internal sealed class FixtureForm : Form
{
    internal static readonly string[] Lines = [
        "Sélio — Le texte, partout.",
        "Bonjour ! Ceci est un test de reconnaissance.",
        "Le français conserve ses accents : été, élève, déjà.",
        "Hello world. Select words across several lines.",
        "Local OCR. No account. No network.",
        "var total = 42; // exemple de code"
    ];

    public FixtureForm()
    {
        Text = "Sélio — Document de test OCR";
        ClientSize = new Size(1060, 700);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 16);
        DoubleBuffered = true;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        NativeMethods.ShowWindow(Handle, 5);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var heading = new Font("Segoe UI", 25, FontStyle.Bold);
        using var body = new Font("Segoe UI", 18);
        for (int i = 0; i < Lines.Length; i++)
            e.Graphics.DrawString(Lines[i], i == 0 ? heading : body, Brushes.Black, 35, 35 + i * 65);
        using var dark = new SolidBrush(Theme.Background);
        e.Graphics.FillRectangle(dark, 25, 460, ClientSize.Width - 50, 160);
        using var cream = new SolidBrush(Theme.Cream);
        e.Graphics.DrawString("Texte sur fond sombre. Une image, des mots.", body, cream, 40, 485);
        using var small = new Font("Segoe UI", 10);
        e.Graphics.DrawString("Petit texte : précision, sélection, presse-papiers.", small, cream, 40, 550);
    }
}
