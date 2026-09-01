using Microsoft.Web.WebView2.Core;
using System.Windows;

namespace CodexQuota;

public partial class LoginWindow : Window
{
    private readonly OAuthLoginService _oauth = new();

    public LoginWindow(Window owner)
    {
        InitializeComponent();
        Owner = owner;
        Browser.NavigationStarting += Browser_NavigationStarting;
        Loaded += async (_, _) =>
        {
            try
            {
                await Browser.EnsureCoreWebView2Async();
                Browser.Source = _oauth.CreateAuthorizationUri();
            }
            catch (Exception ex) { ShowError(ex); }
        };
        Closed += (_, _) => _oauth.Dispose();
    }

    private async void Browser_NavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        if (!e.Uri.StartsWith("http://localhost:1455/auth/callback", StringComparison.OrdinalIgnoreCase)) return;
        e.Cancel = true;
        try
        {
            CompleteButton.IsEnabled = false;
            Status.Text = "Anmeldung wird übernommen …";
            var credentials = await _oauth.CompleteAsync(new Uri(e.Uri));
            if (Owner is not MainWindow main) throw new InvalidOperationException("Hauptfenster nicht verfügbar.");
            main.SetBearerCredentials(credentials.AccessToken, credentials.AccountId, credentials.RefreshToken);
            main.LoginCompleted();
            Status.Text = "Verbunden. Dieses Fenster kann geschlossen werden.";
            await Task.Delay(400);
            Hide();
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void Cancel(object sender, RoutedEventArgs e) => Close();
    private void ShowError(Exception ex)
    {
        CompleteButton.IsEnabled = true;
        Status.Text = "Anmeldung fehlgeschlagen";
        System.Windows.MessageBox.Show(this, ex.Message, "CodexQuota – Anmeldung", MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}
