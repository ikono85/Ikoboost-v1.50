using IkoboostWpf.Services;
using IkoboostWpf.ViewModels;
using System.Windows.Controls;

namespace IkoboostWpf.Views;

public partial class StartupPage : Page
{
    private readonly StartupViewModel _vm;

    public StartupPage(StartupService startup)
    {
        InitializeComponent();
        _vm = new StartupViewModel(startup);
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.LoadAsync();
        Unloaded += (_, _) => _vm.Dispose();
    }
}
