using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace CodexQuota;

public partial class MainWindow : Window, IDisposable
{
    private readonly AppSettings _settings;
    private readonly UsageClient _client = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly Forms.NotifyIcon _tray;
    private bool _editing;

    public MainWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        Left = settings.Left; Top = settings.Top; Opacity = settings.Opacity; Topmost = settings.AlwaysOnTop;
        Card.Background = (SolidColorBrush)new BrushConverter().ConvertFromString(settings.Background)!;
        Card.MouseLeftButtonDown += (_, e) => { if (_editing) { DragMove(); SavePosition(); } };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _tray = new Forms.NotifyIcon { Icon = SystemIcons.Application, Visible = true, Text = "Codex-Kontingent" };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Bei ChatGPT anmelden", null, (_, _) => new LoginWindow(this).ShowDialog());
        menu.Items.Add("Jetzt aktualisieren", null, async (_, _) => await RefreshAsync());
        menu.Items.Add("Bearbeitungsmodus", null, (_, _) => ToggleEdit());
        menu.Items.Add("Einstellungen", null, (_, _) => new SettingsWindow(_settings, this).ShowDialog());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => Application.Current.Shutdown());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => ToggleEdit();
        IsVisibleChanged += (_, _) => { if (IsVisible) Hide(); };
    }

    public async Task StartAsync()
    {
        Hide();
        SetClickThrough(_settings.ClickThrough);
        _timer.Start();
        await RefreshAsync();
    }

    public void SetSessionCookies(string cookieHeader) => _client.SetCookieHeader(cookieHeader);

    private async Task RefreshAsync()
    {
        try
        {
            var usage = await _client.GetUsageAsync();
            FiveHourText.Text = usage.FiveHour is null ? "–" : $"{usage.FiveHour.RemainingPercent:0}%";
            WeekText.Text = usage.Week is null ? "–" : $"{usage.Week.RemainingPercent:0}%";
            StatusText.Text = $"Reset: {FormatReset(usage.FiveHour?.ResetAt ?? usage.Week?.ResetAt)} · {DateTime.Now:HH:mm}";
        }
        catch (NotAuthenticatedException) { StatusText.Text = "Anmeldung erforderlich"; }
        catch (Exception ex) { StatusText.Text = $"Fehler: {ex.Message}"; }
    }

    private static string FormatReset(DateTimeOffset? time) => time is null ? "unbekannt" : time.Value.LocalDateTime.ToString("dd.MM. HH:mm");

    private void ToggleEdit()
    {
        _editing = !_editing;
        StatusText.Text = _editing ? "Bearbeitungsmodus – ziehen zum Verschieben" : "Position gespeichert";
        if (!_editing) SavePosition();
        SetClickThrough(!_editing && _settings.ClickThrough);
    }

    private void SavePosition() { _settings.Left = Left; _settings.Top = Top; _settings.Save(); }

    public void ApplySettings()
    {
        Opacity = _settings.Opacity; Topmost = _settings.AlwaysOnTop;
        Card.Background = (SolidColorBrush)new BrushConverter().ConvertFromString(_settings.Background)!;
        SetClickThrough(!_editing && _settings.ClickThrough);
    }

    private void SetClickThrough(bool enabled)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, enabled ? style | Native.WS_EX_TRANSPARENT | Native.WS_EX_LAYERED : style & ~Native.WS_EX_TRANSPARENT);
    }

    public void Dispose() { _timer.Stop(); _tray.Visible = false; _tray.Dispose(); _client.Dispose(); }
}
