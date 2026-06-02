using System.Windows.Controls;
using IkoboostWpf.ViewModels;

namespace IkoboostWpf.Views;

public partial class OptimizePage : Page
{
    public OptimizePage(OptimizeViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    private void Tab_Checked(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is OptimizeViewModel vm && sender is RadioButton { Tag: string tab })
            vm.SelectedOptimizationTab = tab;
    }
}
