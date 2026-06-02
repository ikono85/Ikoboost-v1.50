using System.Windows.Controls;
using IkoboostWpf.ViewModels;

namespace IkoboostWpf.Views;

public partial class HardwarePage : Page
{
    public HardwarePage(HardwareViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
