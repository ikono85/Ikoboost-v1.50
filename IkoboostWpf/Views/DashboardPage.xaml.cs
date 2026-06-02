using System.Windows.Controls;
using IkoboostWpf.ViewModels;

namespace IkoboostWpf.Views;

public partial class DashboardPage : Page
{
    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
