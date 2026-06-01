using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IkoboostWpf.Services;
using System.Collections.ObjectModel;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace IkoboostWpf.ViewModels;

public sealed partial class WingetViewModel : BaseViewModel
{
    private readonly WingetService _winget;
    private List<WingetService.AppInfo> _allApps = [];
    private bool _initialized;

    [ObservableProperty] private bool _isWingetAvailable;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusLog = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private WingetService.AppInfo? _selectedApp;

    public ObservableCollection<WingetService.AppInfo> Apps { get; } = [];
    public int TotalAppCount => _allApps.Count;
    public int VisibleAppCount => Apps.Count;
    public string WingetStatusText => IsWingetAvailable ? "winget disponible" : "liste locale";

    public WingetViewModel(WingetService winget)
    {
        _winget = winget;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        _initialized = true;
        IsWingetAvailable = _winget.IsWingetAvailable();
        await LoadAppsAsync();
    }

    [RelayCommand]
    private async Task LoadAppsAsync()
    {
        IsBusy = true;
        Apps.Clear();
        StatusLog = "Chargement des applications...";
        try
        {
            _allApps = [.. await _winget.ListInstalledAsync()];
            ApplyFilter();
            var wingetText = IsWingetAvailable ? "winget disponible" : "winget indisponible, liste locale";
            StatusLog = $"{_allApps.Count} application(s) trouvee(s). Source : registre Windows + Appx ({wingetText}).";
            OnPropertyChanged(nameof(TotalAppCount));
            OnPropertyChanged(nameof(WingetStatusText));
        }
        catch (Exception ex) { StatusLog = $"Erreur : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    private void ApplyFilter()
    {
        Apps.Clear();
        var query = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allApps
            : _allApps.Where(app =>
                app.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || app.Publisher.Contains(query, StringComparison.OrdinalIgnoreCase)
                || app.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                || app.Source.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var app in filtered.OrderBy(app => app.Name, StringComparer.CurrentCultureIgnoreCase))
            Apps.Add(app);

        OnPropertyChanged(nameof(VisibleAppCount));
    }

    [RelayCommand]
    private async Task InstallAsync(string appId)
    {
        await RunWingetOperationAsync(() => _winget.InstallAsync(appId), $"Installation de {appId}");
    }

    [RelayCommand]
    private async Task UninstallAsync()
    {
        await UninstallAppAsync(SelectedApp);
    }

    [RelayCommand]
    private async Task UninstallAppAsync(WingetService.AppInfo? app)
    {
        if (app == null) return;

        SelectedApp = app;
        var confirm = MessageBox.Show($"Desinstaller {app.Name} ?", "Confirmer",
            MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        await RunWingetOperationAsync(() => _winget.UninstallAsync(app), $"Desinstallation de {app.Name}");
        await LoadAppsAsync();
    }

    [RelayCommand]
    private async Task UpgradeAsync()
    {
        await UpgradeAppAsync(SelectedApp);
    }

    [RelayCommand]
    private async Task UpgradeAppAsync(WingetService.AppInfo? app)
    {
        if (app == null) return;

        SelectedApp = app;
        await RunWingetOperationAsync(() => _winget.UpgradeAsync(app.Id), $"Mise a jour de {app.Name}");
    }

    [RelayCommand]
    private async Task UpgradeAllAsync()
    {
        var confirm = MessageBox.Show("Mettre a jour toutes les applications ?", "Confirmer",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        await RunWingetOperationAsync(() => _winget.UpgradeAllAsync(), "Mise a jour globale");
    }

    private async Task RunWingetOperationAsync(Func<Task<string>> operation, string label)
    {
        IsBusy = true;
        StatusLog = $"{label} en cours...";
        try
        {
            var output = await operation();
            StatusLog = $"[OK] {label} termine.\n{output}";
        }
        catch (Exception ex) { StatusLog = $"[ERREUR] {label} : {ex.Message}"; }
        finally { IsBusy = false; }
    }
}
