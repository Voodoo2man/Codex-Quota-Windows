using System.Windows;

namespace CodexQuota;

public partial class LoginWindow : Window
{
    public LoginWindow(Window owner)
    {
        InitializeComponent(); Owner = owner;
        Loaded += async (_, _) => { await Browser.EnsureCoreWebView2Async(); Browser.Source = new Uri("https://chatgpt.com/"); };
    }

    private async void Complete(object sender, RoutedEventArgs e)
    {
        if (Browser.CoreWebView2 is null) return;
        var cookies = await Browser.CoreWebView2.CookieManager.GetCookiesAsync("https://chatgpt.com/");
        var header = string.Join("; ", cookies.Select(c => $"{c.Name}={c.Value}"));
        if (Owner is MainWindow main) main.SetSessionCookies(header);
        DialogResult = true;
        Close();
    }
}
