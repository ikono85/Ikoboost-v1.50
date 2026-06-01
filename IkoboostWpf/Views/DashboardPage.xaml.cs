using IkoboostWpf.Services;
using IkoboostWpf.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace IkoboostWpf.Views;

public partial class DashboardPage : Page
{
    private readonly DashboardViewModel _vm;
    private readonly SettingsService _settings;

    public DashboardPage(SystemInfoService sysInfo, NetworkService network, HardwareTemperatureService temps, SettingsService settings)
    {
        _settings = settings;
        var s = settings.Load();
        _vm = new DashboardViewModel(sysInfo, network, temps, s.RefreshIntervalSeconds);

        // Register BoolToVisibility converter
        Resources.Add("BoolToVisConverter", new BooleanToVisibilityConverter());

        InitializeComponent();
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.InitializeAsync();
        Unloaded += (_, _) => _vm.Pause();
        _settings.SettingsChanged += OnSettingsChanged;
    }

    private void OnSettingsChanged(object? sender, AppSettings settings)
        => Dispatcher.Invoke(() => _vm.SetRefreshInterval(settings.RefreshIntervalSeconds));
}
