using IkoboostWpf.Services;
using IkoboostWpf.ViewModels;
using System.Windows.Controls;

namespace IkoboostWpf.Views;

public partial class OptimizePage : Page
{
    public OptimizePage(OptimizationService optimization, NetworkService network)
    {
        var vm = new OptimizationViewModel(optimization, network);
        InitializeComponent();
        DataContext = vm;
        Unloaded += (_, _) => vm.Dispose();
    }
}
