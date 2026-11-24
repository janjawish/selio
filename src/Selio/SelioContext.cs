using System.Diagnostics;
using Selio.Core;

namespace Selio;

internal sealed class SelioContext : ApplicationContext
{
    private readonly HotkeyWindow _hotkey = new();
    private readonly PopupForm _popup = new();
    private readonly NotifyIcon _tray;
    private readonly ContextMenuStrip _menu;
    private readonly SettingsStore _store;
    private readonly bool _testSession;
    private AppSettings _settings;
    private string? _startupWarning;
    private SettingsForm? _settingsForm;
    private CancellationTokenSource? _operation;
    private SelectionOverlay? _overlay;
    private Task? _pending;
    private CaptureAction _lastAction;
    private Screen? _screen;
    private bool _quitting;
    internal double LastOcrMilliseconds { get; private set; }
    internal PopupForm Popup => _popup;

    public SelioContext(string[] args)
    {
        _testSession = args.Contains("--test-session") || args.Contains("--benchmark");
        string directory = _testSession ? Path.Combine(AppContext.BaseDirectory, "test-settings")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Selio");
        _store = new SettingsStore(directory);
        var loaded = _store.Load();
        _settings = loaded.Settings;
        _startupWarning = loaded.Warning;
        if (!_testSession && loaded.MaySave)
        {
            try
            {
                StartupService.Apply(_settings.StartWithWindows);
                if (!File.Exists(_store.FilePath)) _store.Save(_settings);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException)
            { _startupWarning = "Démarrage automatique non configuré : " + ex.Message; }
        }
        _menu = new ContextMenuStrip { BackColor = Theme.Surface, ForeColor = Theme.Cream };
        _menu.Items.Add("Ouvrir Sélio", null, (_, _) => ShowHome());
        _menu.Items.Add("Ouvrir les paramètres", null, (_, _) => OpenSettings());
        _menu.Items.Add(new ToolStripSeparator());
        _menu.Items.Add("Quitter Sélio", null, (_, _) => Quit());
        _tray = new NotifyIcon { Icon = Theme.Icon, Text = "Sélio · Le texte, partout.", ContextMenuStrip = _menu, Visible = true };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) Toggle(); };
        _hotkey.Invoked += Toggle;
        _hotkey.ShowRequested += ShowHome;
        _hotkey.QuitRequested += Quit;
        if (!_hotkey.TryRegister(_settings.Shortcut))
        {
            _startupWarning = $"Le raccourci {_settings.Shortcut} est déjà utilisé. Choisissez-en un autre dans les paramètres (icône Sélio près de l'horloge).";
            _tray.ShowBalloonTip(5000, "Raccourci Sélio indisponible", _startupWarning, ToolTipIcon.Warning);
        }
        _popup.ActionRequested += action => StartAction(action);
        _popup.SettingsRequested += OpenSettings;
        _popup.CopyRequested += () => _pending = CopyTextAsync();
        _popup.RetryRequested += () => StartAction(_lastAction);
        _popup.CancelRequested += Cancel;
        // Create the dispatcher handle, but do not show a window or initialize OCR on startup.
        _ = _popup.Handle;
        if (args.Contains("--show")) _popup.BeginInvoke(ShowHome);
        int benchmark = Array.IndexOf(args, "--benchmark");
        if (benchmark >= 0 && benchmark + 1 < args.Length)
            _popup.BeginInvoke(async () => { await BenchmarkRunner.RunAsync(this, args[benchmark + 1]); Quit(); });
    }

    private void Toggle()
    {
        if (_quitting) return;
        if (_operation != null) { Cancel(); _popup.Dismiss(); return; }
        if (_settingsForm is { IsDisposed: false }) { _settingsForm.Activate(); return; }
        if (_popup.Visible) _popup.Dismiss(); else ShowHome();
    }

    internal void ShowHome()
    {
        if (_quitting || _operation != null) return;
        _screen = Screen.FromPoint(Cursor.Position);
        _popup.ShowHome($"Échap pour fermer · {_settings.Shortcut}");
        if (_startupWarning is { } warning)
        {
            _popup.ShowMessage("Un réglage demande votre attention", warning, false);
            _startupWarning = null;
        }
        _popup.Present(_screen);
    }

    private void StartAction(CaptureAction action)
    {
        if (_operation != null || _quitting) return;
        _pending = ExecuteAsync(action);
    }

    internal async Task<bool> ExecuteAsync(CaptureAction action, Rectangle? automatedRegion = null)
    {
        if (_operation != null || _quitting) return false;
        _lastAction = action;
        _screen = Screen.FromPoint(Cursor.Position);
        using var cancel = new CancellationTokenSource();
        _operation = cancel;
        var cancellation = cancel.Token;
        Bitmap? capture = null;
        Bitmap? crop = null;
        try
        {
            _popup.Hide();
            // Let DWM retire the launcher before the single snapshot.
            await Task.Delay(100, cancellation);
            NativeMethods.DwmFlush();
            capture = CaptureService.Capture(_screen.Bounds);
            if (action == CaptureAction.Words)
            {
                _popup.ShowProgress("Lecture du texte…", "Analyse locale de la capture.\nVous pourrez ensuite cliquer sur un mot ou glisser sur plusieurs lignes.");
                _popup.Present(_screen);
                var watch = Stopwatch.StartNew();
                var document = await OcrService.RecognizeAsync(capture, _settings.OcrLanguage, cancellation);
                LastOcrMilliseconds = watch.Elapsed.TotalMilliseconds;
                capture.Dispose(); capture = null;
                cancellation.ThrowIfCancellationRequested();
                if (document.Words.Count == 0)
                {
                    _popup.ShowMessage("Aucun texte détecté", "Essayez une zone plus lisible, agrandissez le texte ou choisissez une autre langue OCR.");
                    return false;
                }
                _popup.Hide();
                var selection = await SelectAsync(document, cancellation);
                cancellation.ThrowIfCancellationRequested();
                if (selection?.Text is not { Length: > 0 } text) return false;
                _popup.ShowResult(text);
            }
            else
            {
                var selection = await SelectAsync(null, cancellation, automatedRegion);
                cancellation.ThrowIfCancellationRequested();
                if (selection?.Region is not { } rectangle) return false;
                crop = CaptureService.Crop(capture, rectangle);
                capture.Dispose(); capture = null;
                if (action == CaptureAction.ImageRegion)
                {
                    await ClipboardService.SetImageAsync(crop, cancellation);
                    _popup.ShowMessage("Image copiée", $"La zone de {crop.Width} × {crop.Height} pixels est dans le presse-papiers.\nCollez-la avec Ctrl + V dans l'application de votre choix.");
                    if (!_settings.ShowAfterCopy) { _popup.Dismiss(); return true; }
                }
                else
                {
                    _popup.ShowProgress("Extraction du texte…", "Reconnaissance Windows en cours.\nLe contenu reste sur votre ordinateur.");
                    _popup.Present(_screen);
                    var watch = Stopwatch.StartNew();
                    var document = await OcrService.RecognizeAsync(crop, _settings.OcrLanguage, cancellation);
                    LastOcrMilliseconds = watch.Elapsed.TotalMilliseconds;
                    cancellation.ThrowIfCancellationRequested();
                    if (document.Words.Count == 0) _popup.ShowMessage("Aucun texte détecté", "Cette zone ne contient pas de texte lisible. Essayez une autre sélection ou une autre langue.");
                    else _popup.ShowResult(document.Text);
                }
            }
            _popup.Present(_screen);
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex)
        {
            if (!_quitting && !cancellation.IsCancellationRequested)
            {
                _popup.ShowMessage("L'opération n'a pas abouti", ex.Message);
                _popup.Present(_screen);
            }
            return false;
        }
        finally
        {
            _overlay?.Finish(null);
            _overlay?.Dispose(); _overlay = null;
            crop?.Dispose(); capture?.Dispose();
            _operation = null;
            if (cancellation.IsCancellationRequested && !_quitting) _popup.Dismiss();
        }
    }

    private async Task<SelectionResult?> SelectAsync(OcrDocument? document, CancellationToken cancellation, Rectangle? automatedRegion = null)
    {
        using var overlay = new SelectionOverlay(_screen!.Bounds, document);
        _overlay = overlay;
        overlay.Show();
        using var registration = cancellation.Register(() =>
        {
            if (!overlay.IsDisposed && overlay.IsHandleCreated) overlay.BeginInvoke(() => overlay.Finish(null));
        });
        if (automatedRegion is { } region)
        {
            // Opt-in benchmark only: real overlay lifetime, deterministic selection, no injected input.
            await Task.Delay(120, cancellation);
            overlay.Finish(new SelectionResult(region, null));
        }
        try { return await overlay.Completion; }
        finally { if (_overlay == overlay) _overlay = null; }
    }

    internal async Task CopyTextAsync()
    {
        string text = _popup.CopyText;
        if (string.IsNullOrWhiteSpace(text) || _operation != null || _quitting) return;
        using var cancellation = new CancellationTokenSource();
        _operation = cancellation;
        _popup.SetCopyBusy(true);
        try
        {
            await ClipboardService.SetTextAsync(text, cancellation.Token);
            _popup.SetFooter("Texte copié · Collez-le avec Ctrl + V");
            if (!_settings.ShowAfterCopy) { _popup.SetCopyBusy(false); _popup.Dismiss(); }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _popup.SetFooter(ex.Message); }
        finally { _operation = null; if (!_popup.IsDisposed) _popup.SetCopyBusy(false); }
    }

    private void OpenSettings()
    {
        if (_operation != null || _quitting) return;
        _popup.Hide();
        if (_settingsForm is { IsDisposed: false }) { _settingsForm.Activate(); return; }
        _settingsForm = new SettingsForm(_settings, SaveSettings, Quit);
        _settingsForm.Show();
    }

    private string? SaveSettings(AppSettings next)
    {
        if (!_hotkey.TryRegister(next.Shortcut)) return $"Le raccourci {next.Shortcut} est déjà utilisé. L'ancien raccourci reste actif.";
        try
        {
            if (!_testSession) StartupService.Apply(next.StartWithWindows);
            _store.Save(next);
            _settings = next;
            return null;
        }
        catch (Exception ex)
        {
            _hotkey.TryRegister(_settings.Shortcut);
            try { if (!_testSession) StartupService.Apply(_settings.StartWithWindows); } catch { /* Report the initial failure. */ }
            return "Les modifications n'ont pas été enregistrées : " + ex.Message;
        }
    }

    private void Cancel() { _operation?.Cancel(); _overlay?.Finish(null); }

    internal void Recover(Exception ex)
    {
        Cancel();
        if (!_quitting)
        {
            _popup.ShowMessage("Une erreur est survenue", ex.Message, false);
            _popup.Present(_screen ?? Screen.FromPoint(Cursor.Position));
        }
    }

    internal async void Quit()
    {
        if (_quitting) return;
        _quitting = true;
        Cancel();
        _popup.Hide();
        _settingsForm?.Close();
        _tray.Visible = false;
        if (_pending != null) { try { await _pending; } catch { } }
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cancel();
            _hotkey.Dispose();
            _overlay?.Dispose();
            _settingsForm?.Dispose();
            _popup.Dispose();
            _tray.Dispose();
            _menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
