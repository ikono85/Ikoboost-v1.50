using IkoboostWpf.Services;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using DrawingIcon = System.Drawing.Icon;
using DrawingSystemIcons = System.Drawing.SystemIcons;
using Forms = System.Windows.Forms;

namespace IkoboostWpf.Views;

public partial class MainWindow : Window
{
    private const double StorageAlertPercent = 90.0;
    private static readonly TimeSpan AlertsInterval = TimeSpan.FromSeconds(30);
    private static readonly string VideoAssetPath = Path.Combine(AppContext.BaseDirectory, "Assets", "270507_small.mp4");

    private readonly SystemInfoService _sysInfo = new();
    private readonly NetworkService _network = new(new HttpClient());
    private readonly HardwareTemperatureService _temps = new();
    private readonly ProcessService _processes = new();
    private readonly WingetService _winget = new();
    private readonly StartupService _startup = new();
    private readonly OptimizationService _optimization = new();
    private readonly SettingsService _settings = new();
    private readonly DispatcherTimer _alertsTimer;
    private readonly DispatcherTimer _videoLoopTimer;
    private Uri? _videoSource;
    private bool _isLoopingVideo;

    private Forms.NotifyIcon? _trayIcon;
    private bool _isExitRequested;
    private bool _isCheckingAlerts;
    private bool _networkAlertActive;
    private readonly HashSet<string> _storageAlertedDrives = [];
    private readonly Dictionary<string, Page> _pageCache = [];
    private string _currentTag = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        _alertsTimer = new DispatcherTimer { Interval = AlertsInterval };
        _alertsTimer.Tick += async (_, _) => await CheckAlertsAsync();
        _videoLoopTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _videoLoopTimer.Tick += (_, _) => KeepVideoLooping();
        Loaded += OnLoaded;
        StateChanged += OnStateChanged;
        Closed += OnClosed;
        App.ThemeChanged += OnThemeChanged;
        App.LanguageChanged += OnLanguageChanged;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeTrayIcon();
        UpdateVideoBackground(_settings.Load().Theme);
        NavigateTo("Dashboard");
        _alertsTimer.Start();
        await CheckAlertsAsync();
    }

    private void OnThemeChanged(string theme)
        => Dispatcher.Invoke(() => UpdateVideoBackground(theme));

    private void OnLanguageChanged(string language)
        => Dispatcher.Invoke(RefreshTrayMenu);

    private void UpdateVideoBackground(string theme)
    {
        var enabled = string.Equals(theme, "Video", StringComparison.OrdinalIgnoreCase);
        VideoBackground.Opacity = enabled ? 1 : 0;
        VideoOverlay.Opacity = enabled ? 1 : 0;

        if (!enabled)
        {
            StopVideoBackground();
            return;
        }

        if (!File.Exists(VideoAssetPath))
            return;

        _videoSource = new Uri(VideoAssetPath, UriKind.Absolute);
        StartVideoBackground();
    }

    private void VideoBackground_MediaOpened(object sender, RoutedEventArgs e)
    {
        VideoBackground.Position = TimeSpan.Zero;
        VideoBackground.Play();
        _videoLoopTimer.Start();
    }

    private void VideoBackground_MediaEnded(object sender, RoutedEventArgs e)
    {
        LoopVideoBackground();
    }

    private void VideoBackground_MediaFailed(object? sender, ExceptionRoutedEventArgs e)
    {
        StopVideoBackground();
        VideoBackground.Opacity = 0;
        VideoOverlay.Opacity = 0;
    }

    private void StartVideoBackground()
    {
        if (_videoSource == null)
            return;

        _isLoopingVideo = false;
        VideoBackground.Stop();
        VideoBackground.Source = _videoSource;
        VideoBackground.Position = TimeSpan.Zero;
        VideoBackground.Play();
        _videoLoopTimer.Start();
    }

    private void StopVideoBackground()
    {
        _videoLoopTimer.Stop();
        VideoBackground.Stop();
        VideoBackground.Source = null;
        _isLoopingVideo = false;
    }

    private void KeepVideoLooping()
    {
        if (VideoBackground.Opacity <= 0 || _videoSource == null)
            return;

        if (!VideoBackground.NaturalDuration.HasTimeSpan)
        {
            VideoBackground.Play();
            return;
        }

        var duration = VideoBackground.NaturalDuration.TimeSpan;
        if (duration > TimeSpan.Zero && VideoBackground.Position >= duration - TimeSpan.FromMilliseconds(250))
            LoopVideoBackground();
    }

    private void LoopVideoBackground()
    {
        if (_videoSource == null || _isLoopingVideo)
            return;

        _isLoopingVideo = true;
        _videoLoopTimer.Stop();
        VideoBackground.Stop();
        VideoBackground.Position = TimeSpan.Zero;
        VideoBackground.Play();

        Dispatcher.BeginInvoke(() =>
        {
            if (_videoSource != null && VideoBackground.Opacity > 0)
            {
                VideoBackground.Position = TimeSpan.Zero;
                VideoBackground.Play();
                _videoLoopTimer.Start();
            }

            _isLoopingVideo = false;
        }, DispatcherPriority.Send);
    }

    private void InitializeTrayIcon()
    {
        if (_trayIcon != null)
            return;

        _trayIcon = new Forms.NotifyIcon
        {
            Icon = DrawingIcon.ExtractAssociatedIcon(Environment.ProcessPath ?? Forms.Application.ExecutablePath) ?? DrawingSystemIcons.Application,
            Text = "Ikoboost",
            Visible = true
        };
        RefreshTrayMenu();
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
    }

    private void RefreshTrayMenu()
    {
        if (_trayIcon == null)
            return;

        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add(LocalizationService.Get("Tray.Open"), null, (_, _) => RestoreFromTray());
        menu.Items.Add(LocalizationService.Get("Tray.Quit"), null, (_, _) =>
        {
            _isExitRequested = true;
            Close();
        });
        _trayIcon.ContextMenuStrip?.Dispose();
        _trayIcon.ContextMenuStrip = menu;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized || !_settings.Load().MinimizeToTray)
            return;

        HideToTray();
    }

    private void RestoreFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!_isExitRequested && _settings.Load().MinimizeToTray)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnClosing(e);
    }

    private void HideToTray()
    {
        InitializeTrayIcon();
        if (_trayIcon != null)
            _trayIcon.Visible = true;

        WindowState = WindowState.Normal;
        ShowInTaskbar = false;
        Hide();
        ShowTrayTip("Ikoboost", LocalizationService.Get("Tray.StillRunning"));
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string tag)
            NavigateTo(tag);
    }

    private void NavigateTo(string tag)
    {
        if (_currentTag == tag) return;
        _currentTag = tag;

        foreach (var btn in new[] { BtnDashboard, BtnHardware, BtnNetwork, BtnProcesses, BtnWinget, BtnStartup, BtnOptimize, BtnSettings })
            btn.Style = (Style)FindResource("NavButtonStyle");

        var active = tag switch
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
        active.Style = (Style)FindResource("NavButtonActiveStyle");

        if (!_pageCache.TryGetValue(tag, out var page))
        {
            page = tag switch
            {
                "Dashboard" => new DashboardPage(_sysInfo, _network, _temps, _settings),
                "Hardware" => new HardwarePage(_temps, _sysInfo),
                "Network" => new NetworkPage(_network, _optimization),
                "Processes" => new ProcessPage(_processes),
                "Winget" => new WingetPage(_winget),
                "Startup" => new StartupPage(_startup),
                "Optimize" => new OptimizePage(_optimization, _network),
                "Settings" => new SettingsPage(_settings),
                _ => null
            };
            if (page != null)
                _pageCache[tag] = page;
        }

        if (page == null) return;

        ContentFrame.Opacity = 0;
        ContentFrame.Navigate(page);
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
        ContentFrame.BeginAnimation(OpacityProperty, fade);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        App.ThemeChanged -= OnThemeChanged;
        App.LanguageChanged -= OnLanguageChanged;
        StopVideoBackground();
        _alertsTimer.Stop();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        _sysInfo.Dispose();
        _network.Dispose();
        _temps.Dispose();
    }

    private async Task CheckAlertsAsync()
    {
        if (_isCheckingAlerts)
            return;

        _isCheckingAlerts = true;
        try
        {
            var settings = _settings.Load();

            if (settings.AlertNetwork)
                await CheckNetworkAlertAsync();
            else
                _networkAlertActive = false;

            if (settings.AlertStorage)
                await CheckStorageAlertsAsync();
            else
                _storageAlertedDrives.Clear();
        }
        finally
        {
            _isCheckingAlerts = false;
        }
    }

    private async Task CheckNetworkAlertAsync()
    {
        var adapter = _network.GetActiveAdapterInfo();
        var pingMs = adapter == null ? -1 : await _network.PingAsync();

        if (adapter != null && pingMs >= 0)
        {
            _networkAlertActive = false;
            return;
        }

        if (_networkAlertActive)
            return;

        _networkAlertActive = true;
        ShowTrayTip(LocalizationService.Get("Tray.NetworkAlert"), LocalizationService.Get("Tray.NetworkAlertBody"));
    }

    private async Task CheckStorageAlertsAsync()
    {
        var drives = await _sysInfo.GetDrivesAsync();
        foreach (var drive in drives)
        {
            if (drive.TotalSize <= 0)
                continue;

            var usedPercent = (double)(drive.TotalSize - drive.AvailableFreeSpace) / drive.TotalSize * 100.0;
            if (usedPercent < StorageAlertPercent)
            {
                _storageAlertedDrives.Remove(drive.Name);
                continue;
            }

            if (!_storageAlertedDrives.Add(drive.Name))
                continue;

            ShowTrayTip(
                LocalizationService.Get("Tray.StorageAlert"),
                string.Format(LocalizationService.Get("Tray.StorageAlertBody"), drive.Name, usedPercent, FormatBytes(drive.AvailableFreeSpace)));
        }
    }

    private void ShowTrayTip(string title, string text)
    {
        InitializeTrayIcon();
        _trayIcon?.ShowBalloonTip(5000, title, text, Forms.ToolTipIcon.Warning);
    }

    private static string FormatBytes(long bytes)
        => $"{bytes / 1024d / 1024d / 1024d:N1} Go";
}
