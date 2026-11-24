using Selio.Core;

namespace Selio;

internal sealed class SettingsForm : Form
{
    private readonly CheckBox _ctrl = new() { Text = "Ctrl", AutoSize = true };
    private readonly CheckBox _alt = new() { Text = "Alt", AutoSize = true };
    private readonly CheckBox _shift = new() { Text = "Maj", AutoSize = true };
    private readonly ComboBox _key = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 90, AccessibleName = "Touche du raccourci" };
    private readonly ComboBox _language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "Langue OCR préférée" };
    private readonly CheckBox _startup = new() { Text = "Démarrer Sélio avec Windows", AutoSize = true };
    private readonly CheckBox _afterCopy = new() { Text = "Afficher la popup après copie", AutoSize = true };
    private readonly Label _error = Theme.Label("", 9);
    private readonly Func<AppSettings, string?> _save;
    private readonly AppSettings _original;
    private readonly IReadOnlyList<OcrLanguage> _languages;

    public SettingsForm(AppSettings settings, Func<AppSettings, string?> save, Action quit)
    {
        SuspendLayout();
        _original = settings;
        _save = save;
        Text = "Paramètres — Sélio";
        Icon = Theme.Icon;
        BackColor = Theme.Background;
        ForeColor = Theme.Cream;
        Font = new Font(Theme.FontFamily, 10);
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(490, 370);
        KeyPreview = true;

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22, 18, 22, 18), ColumnCount = 1, RowCount = 9 };
        foreach (int height in new[] { 55, 24, 34, 24, 33, 30, 30, 50, 42 })
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        var header = new Panel { Dock = DockStyle.Fill };
        header.Controls.Add(new PictureBox { Image = Theme.Logo, SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(0, 0, 43, 43) });
        var heading = Theme.Label("Sélio · Paramètres", 17, true);
        heading.SetBounds(56, 0, 355, 30);
        header.Controls.Add(heading);
        var slogan = Theme.Label("Le texte, partout. Tout reste sur ce PC.", 9);
        slogan.SetBounds(57, 34, 380, 22);
        slogan.ForeColor = Theme.Secondary;
        header.Controls.Add(slogan);
        root.Controls.Add(header, 0, 0);
        var shortcutLabel = Theme.Label("Raccourci global", 10, true);
        shortcutLabel.Dock = DockStyle.Fill;
        root.Controls.Add(shortcutLabel, 0, 1);
        var hotkey = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _ctrl.Checked = (settings.Shortcut.Modifiers & 2) != 0;
        _alt.Checked = (settings.Shortcut.Modifiers & 1) != 0;
        _shift.Checked = (settings.Shortcut.Modifiers & 4) != 0;
        hotkey.Controls.AddRange([_ctrl, _alt, _shift, _key]);
        for (char c = 'A'; c <= 'Z'; c++) _key.Items.Add(c.ToString());
        for (char c = '0'; c <= '9'; c++) _key.Items.Add(c.ToString());
        for (int i = 1; i <= 11; i++) _key.Items.Add($"F{i}");
        _key.SelectedItem = settings.Shortcut.Key >= 0x70 ? $"F{settings.Shortcut.Key - 0x70 + 1}" : ((char)settings.Shortcut.Key).ToString();
        root.Controls.Add(hotkey, 0, 2);
        var languageLabel = Theme.Label("Langue OCR préférée", 10, true);
        languageLabel.Dock = DockStyle.Fill;
        root.Controls.Add(languageLabel, 0, 3);
        try { _languages = OcrService.Languages(); }
        catch { _languages = []; }
        _language.Items.Add(new OcrLanguage("", "Automatique (langues Windows installées)"));
        foreach (var language in _languages) _language.Items.Add(language);
        _language.SelectedIndex = 0;
        for (int i = 1; i < _language.Items.Count; i++)
            if (_language.Items[i] is OcrLanguage installed && installed.Tag.Equals(settings.OcrLanguage, StringComparison.OrdinalIgnoreCase)) _language.SelectedIndex = i;
        if (_languages.Count == 0) _error.Text = "Aucun module OCR installé. Ajoutez une langue OCR dans les paramètres Windows.";
        else if (settings.OcrLanguage != null && _language.SelectedIndex == 0) _error.Text = "La langue enregistrée n'est plus installée. Choisissez une langue disponible.";
        root.Controls.Add(_language, 0, 4);
        _startup.Checked = settings.StartWithWindows;
        _afterCopy.Checked = settings.ShowAfterCopy;
        root.Controls.Add(_startup, 0, 5);
        root.Controls.Add(_afterCopy, 0, 6);
        _error.Dock = DockStyle.Fill;
        _error.ForeColor = Theme.Coral;
        root.Controls.Add(_error, 0, 7);
        var buttons = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, Margin = Padding.Empty };
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 19));
        buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        var apply = new SelioButton("Enregistrer les modifications") { Dock = DockStyle.Fill, Primary = true, Font = new Font(Theme.FontFamily, 9) };
        var cancel = new SelioButton("Annuler") { Dock = DockStyle.Fill, Font = new Font(Theme.FontFamily, 9) };
        var exit = new SelioButton("Quitter Sélio") { Dock = DockStyle.Fill, Font = new Font(Theme.FontFamily, 9) };
        apply.Click += (_, _) => Save();
        cancel.Click += (_, _) => Close();
        exit.Click += (_, _) => quit();
        buttons.Controls.Add(apply, 0, 0);
        buttons.Controls.Add(cancel, 1, 0);
        buttons.Controls.Add(exit, 2, 0);
        root.Controls.Add(buttons, 0, 8);
        Controls.Add(root);
        AcceptButton = apply;
        CancelButton = cancel;
        ResumeLayout(false);
        PerformLayout();
    }

    private void Save()
    {
        string key = _key.SelectedItem?.ToString() ?? "O";
        uint keyCode = key.Length > 1 ? (uint)(0x6F + int.Parse(key[1..])) : key[0];
        uint modifiers = (_ctrl.Checked ? 2u : 0) | (_alt.Checked ? 1u : 0) | (_shift.Checked ? 4u : 0);
        string? language = (_language.SelectedItem as OcrLanguage)?.Tag;
        var candidate = _original with
        {
            Shortcut = new Hotkey(modifiers, keyCode), OcrLanguage = string.IsNullOrEmpty(language) ? null : language,
            StartWithWindows = _startup.Checked, ShowAfterCopy = _afterCopy.Checked
        };
        var error = candidate.Validate(_languages.Select(l => l.Tag)) ?? _save(candidate);
        if (error != null) { _error.Text = error; return; }
        Close();
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Theme.ApplyWindow(this); }
    protected override void OnShown(EventArgs e) { base.OnShown(e); NativeMethods.ShowWindow(Handle, 5); Activate(); }
}
