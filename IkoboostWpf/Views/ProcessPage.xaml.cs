using IkoboostWpf.Services;
using IkoboostWpf.ViewModels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace IkoboostWpf.Views;

public partial class ProcessPage : Page
{
    private readonly ProcessViewModel _vm;

    public ProcessPage(ProcessService processService)
    {
        _vm = new ProcessViewModel(processService);
        InitializeComponent();
        DataContext = _vm;
        Loaded += async (_, _) =>
        {
            _vm.StartAutoRefresh();
            await _vm.RefreshAsync();
        };
        Unloaded += (_, _) => _vm.StopAutoRefresh();
    }

    private void ProcessesGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row == null)
            return;

        row.IsSelected = true;
        ProcessesGrid.SelectedItem = row.Item;
    }

    private void ProcessesGrid_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        var menu = new ContextMenu();

        var killItem = new MenuItem { Header = FindTextResource("Processes.Kill", "Terminer") };
        killItem.Click += KillMenuItem_Click;
        menu.Items.Add(killItem);
        menu.Items.Add(new Separator());

        var priorityMenu = new MenuItem { Header = FindTextResource("Processes.ChangePriority", "Regler la priorite") };
        AddPriorityItem(priorityMenu, "Idle", "Idle");
        AddPriorityItem(priorityMenu, FindTextResource("Processes.Low", "Basse"), "BelowNormal");
        AddPriorityItem(priorityMenu, FindTextResource("Processes.Normal", "Normale"), "Normal");
        AddPriorityItem(priorityMenu, FindTextResource("Processes.AboveNormal", "Au-dessus normale"), "AboveNormal");
        AddPriorityItem(priorityMenu, FindTextResource("Processes.High", "Haute"), "High");
        menu.Items.Add(priorityMenu);

        e.Row.ContextMenu = menu;
    }

    private void AddPriorityItem(MenuItem parent, string header, string tag)
    {
        var item = new MenuItem { Header = header, Tag = tag };
        item.Click += PriorityMenuItem_Click;
        parent.Items.Add(item);
    }

    private async void KillMenuItem_Click(object sender, RoutedEventArgs e)
        => await _vm.KillProcessCommand.ExecuteAsync(null);

    private async void PriorityMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem item && item.Tag is string tag)
            await _vm.SetPriorityCommand.ExecuteAsync(tag);
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T parent)
                return parent;

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private string FindTextResource(string key, string fallback)
        => TryFindResource(key)?.ToString() ?? fallback;
}
