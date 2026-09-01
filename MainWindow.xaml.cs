using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Forms = System.Windows.Forms;

namespace CodexQuota;

public partial class MainWindow : Window, IDisposable
{
    private readonly AppSettings _settings;
    private readonly UsageClient _client = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(30) };
    private readonly Forms.NotifyIcon _tray;
    private readonly Icon _trayIcon;
    private LoginWindow? _login;
    private SettingsWindow? _settingsWindow;
    private bool _editing;

    public MainWindow(AppSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        Left = settings.Left; Top = settings.Top; Topmost = settings.AlwaysOnTop;
        Width = settings.OverlayWidth > 0 ? settings.OverlayWidth : 430;
        Height = settings.OverlayHeight > 0 ? settings.OverlayHeight : 205;
        ApplyGlassBackground();
        Card.SizeChanged += (_, _) => UpdateCardClip();
        Loaded += (_, _) => UpdateCardClip();
        Card.MouseLeftButtonDown += (_, e) => { if (_editing) { DragMove(); SavePosition(); } };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _trayIcon = TrayIconFactory.Create();
        _tray = new Forms.NotifyIcon { Icon = _trayIcon, Visible = true, Text = "Codex-Kontingent" };
        var menu = new Forms.ContextMenuStrip
        {
            Renderer = new TrayMenuRenderer(),
            ShowImageMargin = false,
            ShowCheckMargin = false,
            BackColor = System.Drawing.Color.FromArgb(32, 32, 32),
            ForeColor = System.Drawing.Color.White,
            Padding = new Forms.Padding(0, 6, 0, 6),
            Font = new System.Drawing.Font("Segoe UI", 10F)
        };
        AddMenuHeading(menu, "CodexQuota");
        menu.Items.Add("Kontingent aktualisieren", null, async (_, _) => await RefreshAsync());
        menu.Items.Add("Einstellungen", null, (_, _) => OpenSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        AddMenuHeading(menu, "Anzeige");
        var editModeItem = new Forms.ToolStripMenuItem("Bearbeitungsmodus aktivieren");
        editModeItem.Click += (_, _) => SetEditMode(!IsEditing);
        menu.Items.Add(editModeItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => System.Windows.Application.Current.Shutdown());
        menu.Opening += (_, _) => editModeItem.Text = IsEditing
            ? "Bearbeitungsmodus deaktivieren"
            : "Bearbeitungsmodus aktivieren";
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => OpenSettings();
    }

    private static void AddMenuHeading(Forms.ContextMenuStrip menu, string text)
    {
        var heading = new Forms.ToolStripLabel(text)
        {
            ForeColor = System.Drawing.Color.FromArgb(156, 163, 175),
            Font = new System.Drawing.Font("Segoe UI", 9F),
            Padding = new Forms.Padding(20, 4, 20, 3),
            AutoSize = true,
            Enabled = false
        };
        menu.Items.Add(heading);
    }

    public async Task StartAsync()
    {
        Show();
        Activate();
        SetClickThrough(_settings.ClickThrough);
        await RestoreCredentialsAsync();
        _timer.Start();
        await RefreshAsync();
    }

    public void SetSessionCookies(string cookieHeader) => _client.SetCookieHeader(cookieHeader);
    public void SetBearerCredentials(string accessToken, string? accountId, string? refreshToken = null)
    {
        _client.SetBearerCredentials(accessToken, accountId);
        CredentialStore.Save(new OAuthCredentials(accessToken, accountId, refreshToken));
    }

    public void Logout()
    {
        _client.ClearAuthentication();
        CredentialStore.Delete();
        _ = RefreshAsync();
    }

    private async Task RestoreCredentialsAsync()
    {
        var stored = CredentialStore.Load();
        if (stored is null) return;
        _client.SetBearerCredentials(stored.AccessToken, stored.AccountId);
        try
        {
            await _client.GetUsageAsync();
        }
        catch (NotAuthenticatedException) when (!string.IsNullOrWhiteSpace(stored.RefreshToken))
        {
            var refreshed = await OAuthLoginService.RefreshAsync(stored);
            SetBearerCredentials(refreshed.AccessToken, refreshed.AccountId, refreshed.RefreshToken);
        }
    }

    public void OpenLogin()
    {
        if (_login is not null)
        {
            _login.Show();
            _login.Activate();
            return;
        }
        _login = new LoginWindow(this);
        _login.Closed += (_, _) => _login = null;
        _login.Show();
    }

    public void OpenSettings()
    {
        if (_settingsWindow is not null) { _settingsWindow.Activate(); return; }
        _settingsWindow = new SettingsWindow(_settings, this);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    public void SetBrowserSession(CoreWebView2 browser)
    {
        _client.SetBrowserFetcher(async () =>
        {
            var script = """
                (() => {
                  try {
                    const r = new XMLHttpRequest();
                    r.open('GET', 'https://chatgpt.com/backend-api/wham/usage', false);
                    r.withCredentials = true;
                    r.setRequestHeader('Accept', 'application/json');
                    r.send();
                    return JSON.stringify({ status: r.status, body: r.responseText });
                  } catch (e) {
                    return JSON.stringify({ status: 0, body: String(e) });
                  }
                })()
                """;
            var encoded = await browser.ExecuteScriptAsync(script);
            var wrapper = encoded.TrimStart().StartsWith("\"")
                ? (System.Text.Json.JsonSerializer.Deserialize<string>(encoded) ?? "")
                : encoded;
            using var result = System.Text.Json.JsonDocument.Parse(wrapper);
            if (result.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object ||
                !result.RootElement.TryGetProperty("status", out var statusElement))
                throw new HttpRequestException("Die WebView2-Antwort enthält keinen HTTP-Status.");
            var status = statusElement.GetInt32();
            if (status is 401 or 403) throw new NotAuthenticatedException();
            if (status == 0) throw new HttpRequestException(result.RootElement.TryGetProperty("body", out var error) ? error.GetString() : "Browseranfrage fehlgeschlagen.");
            if (status < 200 || status >= 300) throw new HttpRequestException($"ChatGPT antwortete mit HTTP {status}.");
            if (!result.RootElement.TryGetProperty("body", out var body))
                throw new HttpRequestException("ChatGPT lieferte keinen Nutzungsdaten-Response.");
            return body.GetString() ?? "{}";
        });
    }

    public void LoginCompleted()
    {
        _settings.FirstRun = false;
        _settings.Save();
        _ = RefreshAsync();
        if (_settingsWindow is not null)
            _ = _settingsWindow.RefreshConnectionAsync();
    }

    public async Task<(bool Connected, string Message)> CheckConnectionAsync()
    {
        try
        {
            await _client.GetUsageAsync();
            return (true, "Verbunden");
        }
        catch (NotAuthenticatedException)
        {
            return (false, "Nicht verbunden");
        }
        catch (Exception ex)
        {
            return (false, $"Fehler: {ex.Message}");
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            var usage = await _client.GetUsageAsync();
            FiveHourText.Text = usage.FiveHour is null ? "–" : $"{usage.FiveHour.RemainingPercent:0}%";
            FiveHourBar.Value = usage.FiveHour is null ? 0 : 100 - usage.FiveHour.RemainingPercent;
            WeekText.Text = usage.Week is null ? "–" : $"{usage.Week.RemainingPercent:0}%";
            WeekBar.Value = usage.Week is null ? 0 : 100 - usage.Week.RemainingPercent;
            FiveHourResetText.Text = $"Reset {FormatReset(usage.FiveHour?.ResetAt)}";
            WeekResetText.Text = $"Reset {FormatReset(usage.Week?.ResetAt)}";
            LastUpdatedText.Text = DateTime.Now.ToString("HH:mm:ss");
            StatusText.Text = "";
        }
        catch (NotAuthenticatedException) { StatusText.Text = "Anmeldung erforderlich"; }
        catch (Exception ex) { StatusText.Text = $"Fehler: {ex.Message}"; }
    }

    private static string FormatReset(DateTimeOffset? time) => time is null ? "unbekannt" : time.Value.LocalDateTime.ToString("dd.MM. HH:mm");

    public bool IsEditing => _editing;

    public void SetEditMode(bool enabled)
    {
        _editing = enabled;
        StatusText.Text = "";
        EditHint.Visibility = Visibility.Collapsed;
        RightResizeEdge.Visibility = _editing ? Visibility.Visible : Visibility.Collapsed;
        BottomResizeEdge.Visibility = _editing ? Visibility.Visible : Visibility.Collapsed;
        CornerResizeEdge.Visibility = _editing ? Visibility.Visible : Visibility.Collapsed;
        if (!_editing) SavePosition();
        SetClickThrough(!_editing && _settings.ClickThrough);
    }

    private void ResizeEdge_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        if (!_editing) return;
        var thumb = (FrameworkElement)sender;
        var tag = thumb.Tag?.ToString();
        if (tag is "Right" or "Corner")
            Width = Math.Max(220, Width + e.HorizontalChange);
        if (tag is "Bottom" or "Corner")
            Height = Math.Max(120, Height + e.VerticalChange);
        _settings.OverlayWidth = Width;
        _settings.OverlayHeight = Height;
        _settings.Save();
    }

    private void SavePosition() { _settings.Left = Left; _settings.Top = Top; _settings.Save(); }

    public void ApplySettings()
    {
        Topmost = _settings.AlwaysOnTop;
        if (!_editing)
        {
            Width = _settings.OverlayWidth;
            Height = _settings.OverlayHeight;
        }
        ApplyGlassBackground();
        SetClickThrough(!_editing && _settings.ClickThrough);
    }

    private void ApplyGlassBackground()
    {
        System.Windows.Media.Color color;
        try { color = ((SolidColorBrush)new BrushConverter().ConvertFromString(_settings.Background)!).Color; }
        catch { color = System.Windows.Media.Color.FromRgb(32, 35, 42); }
        var strength = Math.Clamp(_settings.Opacity, 0, 1);
        var brightness = Math.Clamp(_settings.Brightness, 0.35, 1.65);
        var alpha = (byte)(strength * 255);
        var red = (byte)Math.Clamp(color.R * brightness, 0, 255);
        var green = (byte)Math.Clamp(color.G * brightness, 0, 255);
        var blue = (byte)Math.Clamp(color.B * brightness, 0, 255);
        var light = System.Windows.Media.Color.FromArgb(alpha, (byte)Math.Min(255, red + 28), (byte)Math.Min(255, green + 28), (byte)Math.Min(255, blue + 28));
        var dark = System.Windows.Media.Color.FromArgb(alpha, red, green, blue);
        Card.Background = new LinearGradientBrush(light, dark, 90);
    }

    private void UpdateCardClip() => Card.Clip = new RectangleGeometry(new Rect(0, 0, Card.ActualWidth, Card.ActualHeight), 18, 18);

    private void ResponsiveLayoutChanged(object sender, SizeChangedEventArgs e)
    {
        var twoColumns = e.NewSize.Width >= 620;
        QuotaGrid.Columns = twoColumns ? 2 : 1;
        var requiredHeight = twoColumns ? 155 : 250;
        var isLegacyWideHeight = twoColumns && Height <= 205 && Height > requiredHeight;
        var isLegacyStackedHeight = !twoColumns && Height <= 270 && Height > requiredHeight;
        if (Height < requiredHeight || isLegacyWideHeight || isLegacyStackedHeight)
        {
            Height = requiredHeight;
            _settings.OverlayHeight = Height;
            _settings.Save();
        }
        if (QuotaGrid.Children.Count >= 2)
        {
            if (QuotaGrid.Children[0] is FrameworkElement first)
                first.Margin = twoColumns ? new Thickness(0, 0, 6, 0) : new Thickness(0);
            if (QuotaGrid.Children[1] is FrameworkElement second)
                second.Margin = twoColumns ? new Thickness(6, 0, 0, 0) : new Thickness(0, 8, 0, 0);
        }
    }

    private void SetClickThrough(bool enabled)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var style = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, enabled ? style | Native.WS_EX_TRANSPARENT | Native.WS_EX_LAYERED : style & ~Native.WS_EX_TRANSPARENT);
    }

    public void Dispose() { _timer.Stop(); _tray.Visible = false; _tray.Dispose(); _trayIcon.Dispose(); _client.Dispose(); }
}
