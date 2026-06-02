using System.Windows.Controls;
using IkoboostWpf.ViewModels;

namespace IkoboostWpf.Views;

public partial class SettingsPage : Page
{
    public SettingsPage(SettingsViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
