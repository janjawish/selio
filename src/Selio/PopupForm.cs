using Selio.Core;

namespace Selio;

internal enum CaptureAction { Words, TextRegion, ImageRegion }

internal sealed class PopupForm : Form
{
    private readonly PictureBox _logo = new() { Image = Theme.Logo, SizeMode = PictureBoxSizeMode.Zoom };
    private readonly Label _name = Theme.Label("Sélio", 19, true);
    private readonly Label _slogan = Theme.Label("Le texte, partout.", 9);
    private readonly Label _heading = Theme.Label("Que veux-tu récupérer ?", 11, true);
    private readonly Label _footer = Theme.Label("Échap pour fermer", 8.5f);
    private readonly SelioButton _close = new("Fermer");
    private readonly Panel _body = new();
    private TextBox? _preview;
    public bool Busy { get; private set; }
    public event Action<CaptureAction>? ActionRequested;
    public event Action? SettingsRequested;
    public event Action? CopyRequested;
    public event Action? RetryRequested;
    public event Action? CancelRequested;
    public event Action? Dismissed;

    public PopupForm()
    {
        SuspendLayout();
        Text = "Sélio";
        AccessibleName = "Sélio — Le texte, partout.";
        Icon = Theme.Icon;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = Program.UiTestMode;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;
        BackColor = Theme.Background;
        ForeColor = Theme.Cream;
        Font = new Font(Theme.FontFamily, 10);
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        KeyPreview = true;
        ClientSize = new Size(460, 344);
        _slogan.ForeColor = _footer.ForeColor = Theme.Secondary;
        _close.Font = new Font(Theme.FontFamily, 8.5f);
        _close.Click += (_, _) => Dismiss();
        Controls.AddRange([_logo, _name, _slogan, _heading, _body, _footer, _close]);
        // Inspection tools may activate their permission UI before taking a screenshot.
        // The explicit UI-test switch keeps only the launcher open through that handoff.
        Deactivate += (_, _) => { if (!Program.UiTestMode && !Busy && Visible) Dismiss(); };
        ShowHome();
        ResumeLayout(false);
        PerformLayout();
    }

    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; if (!Program.UiTestMode) cp.ExStyle |= NativeMethods.WsExToolWindow; return cp; }
    }

    private int S(int value) => Geometry.DipToPixel(value, DeviceDpi);
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.ApplyWindow(this); }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var pen = new Pen(Theme.Border);
        using var path = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), S(11));
        e.Graphics.DrawPath(pen, path);
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        if (_logo == null) return;
        _logo.SetBounds(S(18), S(16), S(40), S(40));
        _name.SetBounds(S(69), S(12), S(200), S(30));
        _slogan.SetBounds(S(70), S(43), S(230), S(20));
        _close.SetBounds(ClientSize.Width - S(90), S(18), S(72), S(30));
        _heading.SetBounds(S(18), S(76), ClientSize.Width - S(36), S(27));
        _body.SetBounds(S(18), S(106), ClientSize.Width - S(36), ClientSize.Height - S(142));
        _footer.SetBounds(S(18), ClientSize.Height - S(27), ClientSize.Width - S(36), S(20));
    }

    private void ClearBody()
    {
        _preview = null;
        foreach (Control control in _body.Controls.Cast<Control>().ToArray()) control.Dispose();
        _body.Controls.Clear();
    }

    public void ShowHome(string? hint = null)
    {
        Busy = false;
        ClearBody();
        _heading.Text = "Que veux-tu récupérer ?";
        _footer.Text = hint ?? "Échap pour fermer · Ctrl + Maj + O";
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Margin = Padding.Empty };
        for (int i = 0; i < 4; i++) layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        AddAction(layout, "Sélectionner du texte", "Choisir des mots directement à l'écran", () => ActionRequested?.Invoke(CaptureAction.Words));
        AddAction(layout, "Extraire le texte d'une zone", "Encadrer une partie de l'écran", () => ActionRequested?.Invoke(CaptureAction.TextRegion));
        AddAction(layout, "Copier une zone en image", "Capturer une partie de l'écran", () => ActionRequested?.Invoke(CaptureAction.ImageRegion));
        AddAction(layout, "Ouvrir les paramètres", "Raccourci, langue et démarrage", () => SettingsRequested?.Invoke());
        _body.Controls.Add(layout);
    }

    private static void AddAction(TableLayoutPanel layout, string title, string subtitle, Action action)
    {
        var button = new SelioButton(title, subtitle) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 4), TabIndex = layout.Controls.Count };
        button.Click += (_, _) => action();
        layout.Controls.Add(button);
    }

    public void ShowProgress(string title, string description)
    {
        ClearBody();
        Busy = true;
        _heading.Text = title;
        _footer.Text = "Échap pour annuler";
        var descriptionLabel = Theme.Label(description, 11);
        descriptionLabel.Dock = DockStyle.Fill;
        descriptionLabel.Padding = new Padding(10, 20, 10, 10);
        _body.Controls.Add(descriptionLabel);
    }

    public void ShowResult(string text)
    {
        ClearBody();
        Busy = false;
        _heading.Text = "Votre texte est prêt";
        _footer.Text = "Ctrl + C pour copier · Échap pour fermer";
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, S(43)));
        _preview = new TextBox
        {
            Multiline = true, AcceptsReturn = true, ScrollBars = ScrollBars.Vertical,
            BackColor = Theme.Surface, ForeColor = Theme.Cream, BorderStyle = BorderStyle.FixedSingle,
            Dock = DockStyle.Fill, Text = text, Font = new Font(Theme.FontFamily, 10),
            AccessibleName = "Aperçu du texte, modifiable", TabIndex = 0
        };
        layout.Controls.Add(_preview);
        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Margin = new Padding(0, 6, 0, 0) };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 54));
        var copy = new SelioButton("Copier le texte") { Primary = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 5, 0) };
        copy.Click += (_, _) => CopyRequested?.Invoke();
        var retry = new SelioButton("Refaire une sélection") { Dock = DockStyle.Fill, Margin = Padding.Empty };
        retry.Click += (_, _) => RetryRequested?.Invoke();
        buttons.Controls.Add(copy);
        buttons.Controls.Add(retry);
        layout.Controls.Add(buttons);
        _body.Controls.Add(layout);
    }

    public string CopyText => _preview is null ? "" : _preview.SelectionLength > 0 ? _preview.SelectedText : _preview.Text;

    public void ShowMessage(string title, string message, bool retry = true)
    {
        ClearBody();
        Busy = false;
        _heading.Text = title;
        _footer.Text = "Échap pour fermer";
        var description = Theme.Label(message, 10);
        description.Dock = DockStyle.Fill;
        description.Padding = new Padding(3, 8, 3, 8);
        _body.Controls.Add(description);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = S(40), WrapContents = false };
        var back = new SelioButton("Revenir à l'accueil") { Width = S(195), Height = S(34) };
        back.Click += (_, _) => ShowHome();
        buttons.Controls.Add(back);
        if (retry)
        {
            var again = new SelioButton("Refaire une capture") { Width = S(200), Height = S(34) };
            again.Click += (_, _) => RetryRequested?.Invoke();
            buttons.Controls.Add(again);
        }
        _body.Controls.Add(buttons);
    }

    public void SetFooter(string text) => _footer.Text = text;
    public void SetCopyBusy(bool busy) { Busy = busy; _body.Enabled = !busy; }

    public void Present(Screen screen)
    {
        // Put the handle on the chosen monitor before using its DPI.
        Location = screen.WorkingArea.Location;
        int dpi = NativeMethods.DpiAt(new Point(screen.Bounds.Left + 1, screen.Bounds.Top + 1));
        Bounds = Geometry.PlacePopup(screen.WorkingArea, new Size(Geometry.DipToPixel(460, dpi), Geometry.DipToPixel(344, dpi)), dpi);
        Show();
        NativeMethods.ShowWindow(Handle, 5);
        NativeMethods.SetForegroundWindow(Handle);
        Activate();
        SelectNextControl(null, true, true, true, false);
    }

    public void Dismiss()
    {
        if (Busy) CancelRequested?.Invoke();
        Hide();
        // Clear sensitive text when the interaction ends; there is no history.
        ShowHome();
        Dismissed?.Invoke();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.Escape) { Dismiss(); return true; }
        if (keyData == (Keys.Control | Keys.C) && _preview != null && !Busy)
        { CopyRequested?.Invoke(); return true; }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Dismiss(); }
        base.OnFormClosing(e);
    }
}
