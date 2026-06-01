using IkoboostWpf.Services;
using IkoboostWpf.ViewModels;
using System.Windows.Controls;

namespace IkoboostWpf.Views;

public partial class SettingsPage : Page
{
    public SettingsPage(SettingsService settings)
    {
        var vm = new SettingsViewModel(settings);
        InitializeComponent();
        DataContext = vm;
        Unloaded += (_, _) => vm.Dispose();
    }
}
