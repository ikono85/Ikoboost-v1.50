using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IkoboostWpf.Models;
using IkoboostWpf.Services;

namespace IkoboostWpf.ViewModels;

public partial class ProcessViewModel : ObservableObject
{
    private readonly ProcessService _service = new();
    private readonly DispatcherTimer _autoTimer;
    private bool _autoRefresh;

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private ObservableCollection<ProcessItem> _processes = [];
    [ObservableProperty] private ProcessItem? _selectedProcess;
    [ObservableProperty] private string _autoRefreshButtonText = "Auto : Off";

    public ProcessViewModel()
    {
        _autoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _autoTimer.Tick += (_, _) => Refresh();
        Refresh();
        _autoRefresh = true;
        _autoTimer.Start();
        AutoRefreshButtonText = "Auto : On";
    }

    partial void OnSearchTextChanged(string value) => Refresh();

    private void Refresh() =>
        Processes = new ObservableCollection<ProcessItem>(_service.GetProcesses(SearchText));

    [RelayCommand]
    private void ToggleAutoRefresh()
    {
        _autoRefresh = !_autoRefresh;
        if (_autoRefresh) { _autoTimer.Start(); AutoRefreshButtonText = "Auto : On"; }
        else { _autoTimer.Stop(); AutoRefreshButtonText = "Auto : Off"; }
    }

    public string KillProcess(int pid)
    {
        var msg = _service.KillProcess(pid);
        Refresh();
        return msg;
    }

    public string SetPriority(int pid, ProcessPriorityClass priority)
    {
        var msg = _service.SetPriority(pid, priority);
        Refresh();
        return msg;
    }
}
