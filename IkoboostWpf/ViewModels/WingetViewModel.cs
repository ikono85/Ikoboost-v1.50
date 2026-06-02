using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IkoboostWpf.Models;
using IkoboostWpf.Services;

namespace IkoboostWpf.ViewModels;

public partial class WingetViewModel : ObservableObject
{
    private readonly WingetService _service = new();
    private List<AppItem> _allApps = [];

    [ObservableProperty] private int _visibleAppCount;
    [ObservableProperty] private int _totalAppCount;
    [ObservableProperty] private string _wingetStatusText = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _statusLog = "";
    [ObservableProperty] private ObservableCollection<AppItem> _apps = [];
    [ObservableProperty] private AppItem? _selectedApp;

    public WingetViewModel()
    {
        WingetStatusText = "chargement rapide";
        _ = CheckWingetAsync();
        LoadAppsCommand.Execute(null);
    }

    partial void OnSearchTextChanged(string value) => FilterApps();

    [RelayCommand]
    private async Task LoadApps()
    {
        StatusLog = "Chargement...";
        _allApps = await _service.GetInstalledAppsAsync(new Progress<string>(s => StatusLog = s));
        TotalAppCount = _allApps.Count;
        FilterApps();
        StatusLog = $"{TotalAppCount} applications chargees.";
    }

    [RelayCommand]
    private async Task UpgradeAll()
    {
        StatusLog = "Mise a jour de toutes les applications...";
        StatusLog = await _service.UpgradeAllAsync();
    }

    [RelayCommand]
    private async Task Uninstall()
    {
        if (SelectedApp == null) return;
        StatusLog = $"Desinstallation de {SelectedApp.Name}...";
        StatusLog = await _service.UninstallAppAsync(SelectedApp.Id);
        await LoadApps();
    }

    [RelayCommand]
    private async Task UpgradeApp(AppItem? app)
    {
        if (app == null) return;
        StatusLog = $"Mise a jour de {app.Name}...";
        StatusLog = await _service.UpgradeAppAsync(app.Id);
    }

    [RelayCommand]
    private async Task UninstallApp(AppItem? app)
    {
        if (app == null) return;
        StatusLog = $"Desinstallation de {app.Name}...";
        StatusLog = await _service.UninstallAppAsync(app.Id);
        await LoadApps();
    }

    private void FilterApps()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allApps
            : _allApps.Where(a =>
                a.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                a.Id.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        Apps = new ObservableCollection<AppItem>(filtered);
        VisibleAppCount = Apps.Count;
    }

    private async Task CheckWingetAsync()
    {
        var available = await Task.Run(_service.IsWingetAvailable);
        WingetStatusText = available ? "winget disponible" : "winget non trouve";
    }
}
