using System.Collections.Generic;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IkoboostWpf.Services;

namespace IkoboostWpf.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _service = new();
    private AppSettings _settings;

    [ObservableProperty] private string _theme = "Sombre (Ikoboost OS)";
    [ObservableProperty] private string _language = "Français";
    [ObservableProperty] private int _refreshIntervalSeconds = 1;
    [ObservableProperty] private int _tempAlertCelsius = 85;
    [ObservableProperty] private bool _alertNetwork = true;
    [ObservableProperty] private bool _alertStorage = true;
    [ObservableProperty] private bool _minimizeToTray;

    public List<string> Themes { get; } = ["Sombre (Ikoboost OS)", "Clair"];
    public List<string> Languages { get; } = ["Français", "English"];
    public List<int> RefreshIntervals { get; } = [1, 2, 5, 10];
    public List<Brush> AccentColors { get; } =
    [
        new SolidColorBrush(Color.FromRgb(0x2F, 0xE6, 0xF2)),
        new SolidColorBrush(Color.FromRgb(0x9E, 0x7B, 0xFF)),
        new SolidColorBrush(Color.FromRgb(0x4D, 0x8D, 0xF6)),
        new SolidColorBrush(Color.FromRgb(0x38, 0xD9, 0x96)),
        new SolidColorBrush(Color.FromRgb(0xF5, 0xB5, 0x3D)),
    ];

    public SettingsViewModel()
    {
        _settings = _service.Load();
        Theme = NormalizeTheme(_settings.Theme);
        Language = NormalizeLanguage(_settings.Language);
        RefreshIntervalSeconds = _settings.RefreshIntervalSeconds;
        TempAlertCelsius = _settings.TempAlertCelsius;
        AlertNetwork = _settings.AlertNetwork;
        AlertStorage = _settings.AlertStorage;
        MinimizeToTray = _settings.MinimizeToTray;
    }

    [RelayCommand]
    private void Save()
    {
        _settings.Theme = Theme;
        _settings.Language = Language;
        _settings.RefreshIntervalSeconds = RefreshIntervalSeconds;
        _settings.TempAlertCelsius = TempAlertCelsius;
        _settings.AlertNetwork = AlertNetwork;
        _settings.AlertStorage = AlertStorage;
        _settings.MinimizeToTray = MinimizeToTray;
        _service.Save(_settings);
    }

    [RelayCommand]
    private void Reset()
    {
        _settings = new AppSettings();
        Theme = NormalizeTheme(_settings.Theme);
        Language = NormalizeLanguage(_settings.Language);
        RefreshIntervalSeconds = _settings.RefreshIntervalSeconds;
        TempAlertCelsius = _settings.TempAlertCelsius;
        AlertNetwork = _settings.AlertNetwork;
        AlertStorage = _settings.AlertStorage;
        MinimizeToTray = _settings.MinimizeToTray;
    }

    private static string NormalizeTheme(string theme) =>
        string.Equals(theme, "Sombre", StringComparison.OrdinalIgnoreCase)
            ? "Sombre (Ikoboost OS)"
            : theme;

    private static string NormalizeLanguage(string language) =>
        language.Contains("Fran", StringComparison.OrdinalIgnoreCase) ? "Français" : language;
}
