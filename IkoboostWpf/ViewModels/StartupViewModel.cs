using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IkoboostWpf.Services;
using System.Collections.ObjectModel;

namespace IkoboostWpf.ViewModels;

public sealed partial class StartupViewModel : BaseViewModel
{
    private readonly StartupService _startup;
    private List<StartupItemViewModel> _allItems = [];
    private CancellationTokenSource? _cts;

    [ObservableProperty] private StartupItemViewModel? _selectedItem;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _statusLog = "Chargement des programmes de démarrage...";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private int _enabledCount;
    [ObservableProperty] private int _disabledCount;

    public ObservableCollection<StartupItemViewModel> Items { get; } = [];

    public StartupViewModel(StartupService startup)
    {
        _startup = startup;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy)
            return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        IsBusy = true;
        StatusLog = "Analyse des programmes de démarrage...";

        try
        {
            var entries = await _startup.GetEntriesAsync(_cts.Token);
            _allItems = entries.Select(e => new StartupItemViewModel(e, ToggleEntryAsync)).ToList();
            ApplyFilter();
            StatusLog = $"{_allItems.Count} programme(s) trouvé(s).";
        }
        catch (OperationCanceledException)
        {
            StatusLog = "Analyse annulée.";
        }
        catch (Exception ex)
        {
            StatusLog = $"Erreur pendant la lecture du démarrage : {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ToggleSelectedAsync()
    {
        if (SelectedItem == null)
            return;

        await ToggleEntryAsync(SelectedItem, !SelectedItem.IsEnabled);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var query = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allItems
            : _allItems.Where(item =>
                item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                item.Source.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                item.Command.Contains(query, StringComparison.CurrentCultureIgnoreCase));

        Items.Clear();
        foreach (var item in filtered)
            Items.Add(item);

        EnabledCount = _allItems.Count(item => item.IsEnabled);
        DisabledCount = _allItems.Count(item => !item.IsEnabled);
    }

    private async Task ToggleEntryAsync(StartupItemViewModel item, bool enabled)
    {
        if (IsBusy || !item.CanToggle)
            return;

        IsBusy = true;
        StatusLog = enabled
            ? $"Activation de {item.Name} au démarrage..."
            : $"Désactivation de {item.Name} au démarrage...";

        try
        {
            await _startup.SetEnabledAsync(item.Entry, enabled);
            StatusLog = enabled
                ? $"{item.Name} sera lancé au prochain démarrage."
                : $"{item.Name} ne sera plus lancé au prochain démarrage.";
            IsBusy = false;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            StatusLog = $"Impossible de modifier {item.Name} : {ex.Message}";
            item.RefreshFromEntry();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public override void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        base.Dispose();
    }
}

public sealed partial class StartupItemViewModel : ObservableObject
{
    private readonly Func<StartupItemViewModel, bool, Task> _toggle;

    public StartupService.StartupEntry Entry { get; private set; }
    public string Name => Entry.Name;
    public string Command => Entry.Command;
    public string Source => Entry.Source;
    public bool CanToggle => Entry.CanToggle;

    [ObservableProperty] private bool _isEnabled;

    public StartupItemViewModel(StartupService.StartupEntry entry, Func<StartupItemViewModel, bool, Task> toggle)
    {
        Entry = entry;
        _toggle = toggle;
        _isEnabled = entry.IsEnabled;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (value == Entry.IsEnabled)
            return;

        _ = _toggle(this, value);
    }

    public void RefreshFromEntry()
    {
        IsEnabled = Entry.IsEnabled;
    }
}
