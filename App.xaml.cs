using System.Windows;

namespace CodexQuota;

public partial class App : System.Windows.Application
{
    private MainWindow? _main;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var settings = AppSettings.Load();
        _main = new MainWindow(settings);
        _main.Show();
        if (settings.FirstRun)
            _main.OpenLogin();
        await _main.StartAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _main?.Dispose();
        base.OnExit(e);
    }
}
