using System.Windows.Controls;
using IkoboostWpf.ViewModels;

namespace IkoboostWpf.Views;

public partial class WingetPage : Page
{
    public WingetPage(WingetViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
