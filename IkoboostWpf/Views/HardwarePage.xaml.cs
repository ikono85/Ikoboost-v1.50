using IkoboostWpf.Services;
using IkoboostWpf.ViewModels;
using System.Windows.Controls;

namespace IkoboostWpf.Views;

public partial class HardwarePage : Page
{
    private readonly HardwareViewModel _vm;

    public HardwarePage(HardwareTemperatureService temps, SystemInfoService sysInfo)
    {
        _vm = new HardwareViewModel(temps, sysInfo);
        InitializeComponent();
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.InitializeAsync();
        Unloaded += (_, _) => _vm.Pause();
    }
}
