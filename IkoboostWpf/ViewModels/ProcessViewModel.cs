using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IkoboostWpf.Services;
using IkoboostWpf.Views;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;

namespace IkoboostWpf.ViewModels;

public sealed partial class ProcessViewModel : BaseViewModel
{
    private readonly ProcessService _processService;
    private List<ProcessService.ProcessInfo> _allProcesses = [];
    private DispatcherTimer? _autoRefreshTimer;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isAutoRefreshPaused;
    [ObservableProperty] private ProcessService.ProcessInfo? _selectedProcess;

    public ObservableCollection<ProcessService.ProcessInfo> Processes { get; } = [];
    public string AutoRefreshButtonText => IsAutoRefreshPaused ? "Reprendre actualisation" : "Pause actualisation";

    public ProcessViewModel(ProcessService processService)
    {
        _processService = processService;
    }

    public void StartAutoRefresh()
    {
        if (_autoRefreshTimer == null)
        {
            _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _autoRefreshTimer.Tick += async (_, _) =>
            {
                if (!IsAutoRefreshPaused)
                    await RefreshAsync();
            };
        }
        _autoRefreshTimer.Start();
    }

    public void StopAutoRefresh()
    {
        _autoRefreshTimer?.Stop();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            _allProcesses = [.. await _processService.GetProcessesAsync()];
            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur lors du chargement des processus : {ex.Message}", "Ikoboost", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally { IsLoading = false; }
    }

    private void ApplyFilter()
    {
        Processes.Clear();
        var filter = SearchText?.Trim().ToLowerInvariant() ?? string.Empty;
        var filtered = string.IsNullOrEmpty(filter)
            ? _allProcesses
            : _allProcesses.Where(p => p.Name.Contains(filter, StringComparison.OrdinalIgnoreCase));

        foreach (var p in filtered.OrderByDescending(p => p.CpuPercent).ThenByDescending(p => p.MemoryMb))
            Processes.Add(p);
    }

    [RelayCommand]
    private async Task KillProcessAsync()
    {
        if (SelectedProcess == null) return;

        var confirmed = ConfirmationDialog.Show(
            title: "Terminer le processus",
            message: $"Terminer « {SelectedProcess.Name} » (PID {SelectedProcess.Pid}) ?\nCette action est irréversible.",
            confirmLabel: "Terminer",
            isDanger: true);
        if (!confirmed) return;

        try
        {
            await _processService.KillProcessAsync(SelectedProcess.Pid);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Impossible de terminer le processus : {ex.Message}", "Ikoboost", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task SetPriorityAsync(string priorityName)
    {
        if (SelectedProcess == null) return;
        if (!Enum.TryParse<ProcessPriorityClass>(priorityName, out var priority)) return;

        try
        {
            await _processService.SetPriorityAsync(SelectedProcess.Pid, priority);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Erreur lors du changement de priorité : {ex.Message}", "Ikoboost", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task RefreshProcessesAsync() => await RefreshAsync();

    [RelayCommand]
    private void ToggleAutoRefresh()
        => IsAutoRefreshPaused = !IsAutoRefreshPaused;

    partial void OnIsAutoRefreshPausedChanged(bool value)
        => OnPropertyChanged(nameof(AutoRefreshButtonText));
}
