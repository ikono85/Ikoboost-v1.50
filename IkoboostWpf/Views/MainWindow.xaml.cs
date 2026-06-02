using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using IkoboostWpf.Services;
using IkoboostWpf.ViewModels;

namespace IkoboostWpf.Views;

public partial class MainWindow : Window
{
    private readonly SystemMonitorService _monitor = new();
    private readonly Dictionary<string, Page> _pageCache = new();

    public MainWindow()
    {
        InitializeComponent();
        _monitor.DataUpdated += OnMonitorUpdated;
        Navigate("Dashboard");
        SetActiveButton("Dashboard");
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var tag = btn.Tag?.ToString() ?? "";
        Navigate(tag);
        SetActiveButton(tag);
    }

    private void Navigate(string tag)
    {
        UpdateHeader(tag);

        if (!_pageCache.TryGetValue(tag, out var page))
        {
            page = tag switch
            {
                "Dashboard" => new DashboardPage(new DashboardViewModel(_monitor)),
                "Hardware" => new HardwarePage(new HardwareViewModel()),
                "Network" => new NetworkPage(new NetworkViewModel()),
                "Processes" => new ProcessPage(new ProcessViewModel()),
                "Winget" => new WingetPage(new WingetViewModel()),
                "Startup" => new StartupPage(new StartupViewModel()),
                "Optimize" => new OptimizePage(new OptimizeViewModel()),
                "Settings" => new SettingsPage(new SettingsViewModel()),
                _ => null!
            };
            if (page != null) _pageCache[tag] = page;
        }
        if (page != null) ContentFrame.Navigate(page);
    }

    private void SetActiveButton(string tag)
    {
        UpdateHeader(tag);

        foreach (var btn in new[] { BtnDashboard, BtnHardware, BtnNetwork, BtnProcesses,
                                     BtnWinget, BtnStartup, BtnOptimize, BtnSettings })
        {
            btn.Tag = btn == GetButtonForTag(tag) ? "" : btn.Name.Replace("Btn", "");
            var isActive = btn == GetButtonForTag(tag);
            btn.Foreground = isActive
                ? (System.Windows.Media.Brush)Application.Current.Resources["TextPrimaryBrush"]
                : (System.Windows.Media.Brush)Application.Current.Resources["TextSecondaryBrush"];

            // Background highlight
            if (isActive)
                btn.Background = (System.Windows.Media.Brush)Application.Current.Resources["SurfaceAltBrush"];
            else
                btn.ClearValue(BackgroundProperty);
        }
    }

    private Button GetButtonForTag(string tag) => tag switch
    {
        "Dashboard" => BtnDashboard,
        "Hardware" => BtnHardware,
        "Network" => BtnNetwork,
        "Processes" => BtnProcesses,
        "Winget" => BtnWinget,
        "Startup" => BtnStartup,
        "Optimize" => BtnOptimize,
        "Settings" => BtnSettings,
        _ => BtnDashboard
    };

    private void UpdateHeader(string tag)
    {
        var (title, subtitle) = tag switch
        {
            "Dashboard" => ("Dashboard", "Vue globale du systeme"),
            "Hardware" => ("Materiel", "Capteurs temperature - ventilateurs - stockage"),
            "Network" => ("Reseau", "Debit - DNS - latence - diagnostic"),
            "Processes" => ("Processus", "Utilisation CPU - memoire - priorite"),
            "Winget" => ("Applications", "Inventaire - installation - mises a jour"),
            "Startup" => ("Demarrage", "Programmes lances au demarrage de Windows"),
            "Optimize" => ("Centre d'optimisation", "Nettoyage - maintenance - performance"),
            "Settings" => ("Parametres", "Apparence - surveillance - alertes"),
            _ => ("Ikoboost", "System Control Center")
        };

        TopTitle.Text = title;
        TopSubtitle.Text = subtitle;
    }

    private void OnMonitorUpdated()
    {
        Dispatcher.Invoke(() =>
        {
            TopCpuTemp.Text = string.IsNullOrWhiteSpace(_monitor.CpuTempText) ? "N/A" : _monitor.CpuTempText;
            TopPing.Text = _monitor.PingMs > 0 ? $"{_monitor.PingMs} ms" : "-- ms";

            var score = ComputeHealthScore();
            SidebarHealthRing.Score = score;
            var isOptimal = score >= 75;
            var isWarning = score >= 55;
            SidebarHealthLabel.Text = isOptimal ? "Optimal" : isWarning ? "Correct" : "Alerte";
            var brushKey = isOptimal ? "SuccessBrush" : isWarning ? "WarningBrush" : "ErrorBrush";
            if (Application.Current.Resources[brushKey] is Brush brush)
            {
                SidebarHealthLabel.Foreground = brush;
                SidebarHealthRing.RingBrush = brush;
            }
        });
    }

    private double ComputeHealthScore()
    {
        var cpuScore = 100 - Math.Clamp(_monitor.CpuPercent, 0, 100);
        var ramScore = 100 - Math.Clamp(_monitor.RamPercent, 0, 100);
        var tempScore = TryReadTemperature(_monitor.CpuTempText, out var temp)
            ? temp switch
            {
                >= 90 => 20,
                >= 80 => 45,
                >= 70 => 70,
                >= 60 => 85,
                _ => 95
            }
            : 80;

        return Math.Round(Math.Clamp((cpuScore + ramScore + tempScore) / 3.0, 0, 100));
    }

    private static bool TryReadTemperature(string text, out double value)
    {
        var normalized = text
            .Replace("°C", "", StringComparison.OrdinalIgnoreCase)
            .Replace("Â°C", "", StringComparison.OrdinalIgnoreCase)
            .Trim();
        return double.TryParse(normalized, out value);
    }

    private void TopOptimize_Click(object sender, RoutedEventArgs e)
    {
        Navigate("Optimize");
        SetActiveButton("Optimize");
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Maximize_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            Maximize_Click(sender, e);
            return;
        }

        DragMove();
    }

    private void VideoBackground_MediaOpened(object sender, RoutedEventArgs e)
    {
        if (sender is MediaElement me) me.Play();
    }

    private void VideoBackground_MediaEnded(object sender, RoutedEventArgs e)
    {
        if (sender is MediaElement me) { me.Position = TimeSpan.Zero; me.Play(); }
    }

    private void VideoBackground_MediaFailed(object sender, ExceptionRoutedEventArgs e) { }

    protected override void OnClosed(EventArgs e)
    {
        _monitor.DataUpdated -= OnMonitorUpdated;
        _monitor.Dispose();
        base.OnClosed(e);
    }
}
