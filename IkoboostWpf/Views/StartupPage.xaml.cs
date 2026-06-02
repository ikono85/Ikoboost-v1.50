using System.Windows.Controls;
using IkoboostWpf.ViewModels;

namespace IkoboostWpf.Views;

public partial class StartupPage : Page
{
    public StartupPage(StartupViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
