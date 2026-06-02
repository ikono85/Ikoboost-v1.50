using System.Windows.Controls;
using IkoboostWpf.ViewModels;

namespace IkoboostWpf.Views;

public partial class NetworkPage : Page
{
    public NetworkPage(NetworkViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
