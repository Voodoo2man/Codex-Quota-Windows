using System.Windows;

namespace CodexQuota;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings; private readonly MainWindow _main;
    public SettingsWindow(AppSettings settings, MainWindow main)
    {
        InitializeComponent(); _settings = settings; _main = main;
        Autostart.IsChecked = settings.StartWithWindows; ClickThrough.IsChecked = settings.ClickThrough; Topmost.IsChecked = settings.AlwaysOnTop; OpacitySlider.Value = settings.Opacity; Background.Text = settings.Background;
    }
    private void Save(object sender, RoutedEventArgs e)
    {
        _settings.StartWithWindows = Autostart.IsChecked == true; _settings.ClickThrough = ClickThrough.IsChecked == true; _settings.AlwaysOnTop = Topmost.IsChecked == true; _settings.Opacity = OpacitySlider.Value; _settings.Background = Background.Text; _settings.Save(); _main.ApplySettings(); Close();
        StartupManager.SetEnabled(_settings.StartWithWindows);
    }
}
