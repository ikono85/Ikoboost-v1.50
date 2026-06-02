using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IkoboostWpf.Models;
using IkoboostWpf.Services;

namespace IkoboostWpf.ViewModels;

public partial class StartupViewModel : ObservableObject
{
    private readonly StartupService _service = new();
    private List<StartupItem> _allItems = [];

    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x38, 0xD9, 0x96));
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(0xF5, 0xB5, 0x3D));
    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xFB, 0x5E, 0x63));
    private static readonly Brush GreenBg = new SolidColorBrush(Color.FromArgb(0x22, 0x38, 0xD9, 0x96));
    private static readonly Brush AmberBg = new SolidColorBrush(Color.FromArgb(0x22, 0xF5, 0xB5, 0x3D));
    private static readonly Brush RedBg = new SolidColorBrush(Color.FromArgb(0x22, 0xFB, 0x5E, 0x63));

    [ObservableProperty] private int _enabledCount;
    [ObservableProperty] private int _disabledCount;
    [ObservableProperty] private string _startupImpactText = "~0s";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _statusLog = "";
    [ObservableProperty] private ObservableCollection<StartupItem> _items = [];
    [ObservableProperty] private StartupItem? _selectedItem;

    public StartupViewModel() => LoadCommand.Execute(null);

    partial void OnSearchTextChanged(string value) => FilterItems();

    [RelayCommand]
    private void Load()
    {
        _allItems = _service.GetStartupItems();
        foreach (var item in _allItems)
            DecorateImpact(item);

        EnabledCount = _allItems.Count(i => i.IsEnabled);
        DisabledCount = _allItems.Count(i => !i.IsEnabled);
        StartupImpactText = $"~{Math.Max(1, EnabledCount * 2)}s";
        FilterItems();
        StatusLog = $"{_allItems.Count} element(s) de demarrage charges.";
    }

    [RelayCommand]
    private void ToggleSelected()
    {
        if (SelectedItem == null) return;
        StatusLog = _service.ToggleItem(SelectedItem);
        Load();
    }

    [RelayCommand]
    private void ToggleItem(StartupItem? item)
    {
        if (item == null) return;
        SelectedItem = item;
        StatusLog = _service.SetItemEnabled(item, item.IsEnabled);
        Load();
    }

    private void FilterItems()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allItems
            : _allItems.Where(i =>
                i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                i.Source.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        Items = new ObservableCollection<StartupItem>(filtered);
    }

    private static void DecorateImpact(StartupItem item)
    {
        var text = $"{item.Name} {item.Command}".ToLowerInvariant();
        if (text.Contains("discord") || text.Contains("teams"))
        {
            item.Impact = "Eleve";
            item.ImpactBrush = Red;
            item.ImpactBackground = RedBg;
        }
        else if (text.Contains("steam") || text.Contains("spotify") || text.Contains("onedrive"))
        {
            item.Impact = "Moyen";
            item.ImpactBrush = Amber;
            item.ImpactBackground = AmberBg;
        }
        else
        {
            item.Impact = "Faible";
            item.ImpactBrush = Green;
            item.ImpactBackground = GreenBg;
        }
    }
}
