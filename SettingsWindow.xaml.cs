using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WpfButton = System.Windows.Controls.Button;

namespace CodexQuota;

public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly MainWindow _main;
    private readonly Slider _brightnessSlider;
    private WpfButton? _loginButton;
    private WpfButton? _logoutButton;

    public SettingsWindow(AppSettings settings, MainWindow main)
    {
        InitializeComponent();
        _settings = settings;
        _main = main;
        System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
        {
            CaptionHeight = 38,
            ResizeBorderThickness = new Thickness(8),
            CornerRadius = new CornerRadius(12),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false
        });
        SourceInitialized += (_, _) => Native.ApplyWindows11Frame(this);
        if (Content is Grid settingsRoot && settingsRoot.ColumnDefinitions.Count > 1)
        {
            settingsRoot.ClipToBounds = true;
            settingsRoot.ColumnDefinitions[0].Width = new GridLength(0);
            foreach (var child in settingsRoot.Children)
                if (child is UIElement element && Grid.GetColumn(element) == 0 && Grid.GetRow(element) == 1)
                    element.Visibility = Visibility.Collapsed;
            ConfigureCaptionBar(settingsRoot);
        }
        if (ConnectionDot.Parent is Grid connectionGrid)
        {
            connectionGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            foreach (var button in connectionGrid.Children.OfType<WpfButton>())
            {
                if (button.Content?.ToString() == "Bei ChatGPT anmelden")
                {
                    _loginButton = button;
                    button.Click += OpenLogin;
                }
            }
            _logoutButton = new WpfButton { Content = "Abmelden", Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Top, Visibility = Visibility.Collapsed };
            _logoutButton.Click += (_, _) => { _main.Logout(); _ = UpdateConnectionAsync(); };
            Grid.SetColumn(_logoutButton, 2);
            connectionGrid.Children.Add(_logoutButton);
        }
        Autostart.IsChecked = settings.StartWithWindows;
        ClickThrough.IsChecked = settings.ClickThrough;
        AlwaysOnTopCheckBox.IsChecked = settings.AlwaysOnTop;
        EditModeCheckBox.IsChecked = main.IsEditing;
        OpacitySlider.Value = settings.Opacity;
        _brightnessSlider = new Slider { Minimum = 0.35, Maximum = 1.65, Value = settings.Brightness, TickFrequency = 0.05, IsSnapToTickEnabled = true, Margin = new Thickness(0, 4, 0, 12) };
        if (OpacitySlider.Parent is StackPanel appearancePanel)
        {
            var opacityIndex = appearancePanel.Children.IndexOf(OpacitySlider);
            appearancePanel.Children.Insert(opacityIndex + 1, new TextBlock { Text = "Helligkeit", Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(85, 85, 85)), FontSize = 12 });
            appearancePanel.Children.Insert(opacityIndex + 2, _brightnessSlider);
            foreach (var swatch in FindVisualChildren<WpfButton>(appearancePanel))
                if (swatch.Tag is string)
                    swatch.Style = (Style)FindResource("ColorSwatchButton");
        }
        BackgroundColorTextBox.Text = settings.Background;
        BackgroundColorTextBox.Visibility = Visibility.Collapsed;
        Loaded += async (_, _) => { ApplySwatchStyles(); await UpdateConnectionAsync(); };
        OpacitySlider.ValueChanged += (_, _) => ApplyLiveSettings();
        _brightnessSlider.ValueChanged += (_, _) => ApplyLiveSettings();
        Autostart.Checked += (_, _) => ApplyLiveSettings(); Autostart.Unchecked += (_, _) => ApplyLiveSettings();
        ClickThrough.Checked += (_, _) => ApplyLiveSettings(); ClickThrough.Unchecked += (_, _) => ApplyLiveSettings();
        AlwaysOnTopCheckBox.Checked += (_, _) => ApplyLiveSettings(); AlwaysOnTopCheckBox.Unchecked += (_, _) => ApplyLiveSettings();
        EditModeCheckBox.Checked += (_, _) => ApplyLiveSettings(); EditModeCheckBox.Unchecked += (_, _) => ApplyLiveSettings();
        BackgroundColorTextBox.TextChanged += (_, _) => ApplyLiveSettings();
    }

    private void OpenLogin(object sender, RoutedEventArgs e) => _main.OpenLogin();


    private void ApplyLiveSettings()
    {
        if (!IsLoaded) return;
        _settings.StartWithWindows = Autostart.IsChecked == true;
        _settings.ClickThrough = ClickThrough.IsChecked == true;
        _settings.AlwaysOnTop = AlwaysOnTopCheckBox.IsChecked == true;
        _main.SetEditMode(EditModeCheckBox.IsChecked == true);
        _settings.Opacity = OpacitySlider.Value;
        _settings.Brightness = _brightnessSlider.Value;
        try { _ = new BrushConverter().ConvertFromString(BackgroundColorTextBox.Text); _settings.Background = BackgroundColorTextBox.Text; }
        catch { return; }
        _settings.Save();
        StartupManager.SetEnabled(_settings.StartWithWindows);
        _main.ApplySettings();
    }

    private async Task UpdateConnectionAsync()
    {
        ConnectionText.Text = "Wird geprüft …";
        ConnectionDot.Fill = System.Windows.Media.Brushes.Goldenrod;
        var result = await _main.CheckConnectionAsync();
        ConnectionText.Text = result.Message;
        ConnectionDot.Fill = result.Connected ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.IndianRed;
        if (_loginButton is not null)
            _loginButton.Visibility = result.Connected ? Visibility.Collapsed : Visibility.Visible;
        if (_logoutButton is not null)
            _logoutButton.Visibility = result.Connected ? Visibility.Visible : Visibility.Collapsed;
    }

    public Task RefreshConnectionAsync() => UpdateConnectionAsync();

    private async void CheckConnection(object sender, RoutedEventArgs e) => await UpdateConnectionAsync();

    private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if (sender is WpfButton button && button.Tag is string color)
            BackgroundColorTextBox.Text = color;
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) yield return match;
            foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }

    private void ApplySwatchStyles()
    {
        if (OpacitySlider.Parent is not StackPanel appearancePanel) return;
        foreach (var swatch in FindVisualChildren<WpfButton>(appearancePanel))
            if (swatch.Tag is string)
                swatch.Style = (Style)FindResource("ColorSwatchButton");
    }

    private void Caption_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
        if (e.ClickCount == 2) ToggleMaximize();
        else DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e) => ToggleMaximize();

    private void ToggleMaximize()
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        MaximizeButton.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ConfigureCaptionBar(Grid root)
    {
        var caption = root.Children.OfType<Border>().FirstOrDefault(border => Grid.GetColumnSpan(border) > 1);
        if (caption is null) return;
        caption.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(23, 23, 23));

        var bar = new Grid();
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition());
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = System.Windows.VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
        left.Children.Add(new TextBlock { Text = "‹", FontSize = 25, Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175)), Margin = new Thickness(0, -2, 18, 0), VerticalAlignment = VerticalAlignment.Center });
        left.Children.Add(new TextBlock { Text = "☰", FontSize = 17, Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(209, 213, 219)), Margin = new Thickness(0, 0, 24, 0), VerticalAlignment = VerticalAlignment.Center });
        left.Children.Add(new TextBlock { Text = "Einstellungen", FontSize = 12, Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 231, 235)), VerticalAlignment = VerticalAlignment.Center });
        left.Children.Clear();
        left.Children.Add(new TextBlock { Text = "CodexQuota - Einstellungen", FontSize = 12, Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(229, 231, 235)), VerticalAlignment = System.Windows.VerticalAlignment.Center });
        Grid.SetColumn(left, 0);
        bar.Children.Add(left);

        var search = new Border { Width = 320, Height = 30, CornerRadius = new CornerRadius(15), Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(42, 42, 42)), BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(65, 65, 65)), BorderThickness = new Thickness(1), HorizontalAlignment = System.Windows.HorizontalAlignment.Center, VerticalAlignment = System.Windows.VerticalAlignment.Center };
        search.Child = new TextBlock { Text = "⌕  Einstellung suchen", Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(161, 161, 170)), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
        Grid.SetColumn(search, 1);
        search.Visibility = Visibility.Collapsed;
        bar.Children.Add(search);

        var right = new StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, VerticalAlignment = System.Windows.VerticalAlignment.Center };
        var minimize = CreateCaptionButton("—");
        minimize.Click += Minimize_Click;
        right.Children.Add(minimize);
        MaximizeButton = CreateCaptionButton("□");
        MaximizeButton.Click += Maximize_Click;
        right.Children.Add(MaximizeButton);
        var close = CreateCaptionButton("×");
        close.Click += Close_Click;
        right.Children.Add(close);
        Grid.SetColumn(right, 2);
        bar.Children.Add(right);

        caption.Child = bar;
    }

    private WpfButton CreateCaptionButton(string content)
    {
        var glyph = content switch
        {
            "—" => "\uE921",
            "□" => "\uE922",
            "×" => "\uE8BB",
            _ => content
        };
        var button = new WpfButton { Content = glyph, Style = (Style)FindResource(glyph == "\uE8BB" ? "CaptionCloseButton" : "CaptionButton") };
        System.Windows.Shell.WindowChrome.SetIsHitTestVisibleInChrome(button, true);
        return button;
    }

    private void SettingsRoot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (sender is Grid root)
            ApplyRootClip(root);
    }

    private static void ApplyRootClip(Grid root) => root.Clip = new RectangleGeometry(new Rect(0, 0, root.ActualWidth, root.ActualHeight), 14, 14);
}
