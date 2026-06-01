using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IkoboostWpf.Services;

namespace IkoboostWpf.ViewModels;

public sealed partial class SettingsViewModel : BaseViewModel
{
    private readonly SettingsService _settingsService;
    private bool _isLoading;

    [ObservableProperty] private string _theme = "Dark";
    [ObservableProperty] private string _language = "Français";
    [ObservableProperty] private int _refreshIntervalSeconds = 2;
    [ObservableProperty] private float _tempAlertCelsius = 85f;
    [ObservableProperty] private bool _minimizeToTray = true;
    [ObservableProperty] private bool _alertNetwork = true;
    [ObservableProperty] private bool _alertStorage = true;

    public IReadOnlyList<string> Themes { get; } = ["Dark", "Light", "Cybertek", "Video"];
    public IReadOnlyList<string> Languages { get; } =
    [
        "Français",
        "English",
        "Español",
        "Português (Brasil)",
        "Deutsch",
        "العربية",
        "Русский",
        "中文简体",
        "日本語"
    ];
    public IReadOnlyList<int> RefreshIntervals { get; } = [1, 2, 5, 10];

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        Load();
    }

    private void Load()
    {
        _isLoading = true;
        var s = _settingsService.Load();
        Theme = s.Theme == "Cyberpunk" ? "Cybertek" : s.Theme;
        Language = ToLanguageLabel(s.Language);
        RefreshIntervalSeconds = s.RefreshIntervalSeconds;
        TempAlertCelsius = s.TempAlertCelsius;
        MinimizeToTray = s.MinimizeToTray;
        AlertNetwork = s.AlertNetwork;
        AlertStorage = s.AlertStorage;
        _isLoading = false;
    }

    partial void OnThemeChanged(string value)
    {
        SaveIfReady();
    }

    partial void OnLanguageChanged(string value) => SaveIfReady();
    partial void OnRefreshIntervalSecondsChanged(int value) => SaveIfReady();
    partial void OnTempAlertCelsiusChanged(float value) => SaveIfReady();
    partial void OnMinimizeToTrayChanged(bool value) => SaveIfReady();
    partial void OnAlertNetworkChanged(bool value) => SaveIfReady();
    partial void OnAlertStorageChanged(bool value) => SaveIfReady();

    private void SaveIfReady()
    {
        if (!_isLoading)
            Save();
    }

    [RelayCommand]
    private void Save()
    {
        var existing = _settingsService.Load();
        _settingsService.Save(new AppSettings
        {
            Theme = Theme,
            Language = ToLanguageCode(Language),
            AccentTheme = existing.AccentTheme,
            DiscoverySource = existing.DiscoverySource,
            OnboardingCompleted = existing.OnboardingCompleted,
            RefreshIntervalSeconds = RefreshIntervalSeconds,
            TempAlertCelsius = TempAlertCelsius,
            MinimizeToTray = MinimizeToTray,
            AlertNetwork = AlertNetwork,
            AlertStorage = AlertStorage
        });
        App.ApplyTheme(Theme);
        App.ApplyLanguage(ToLanguageCode(Language));
    }

    [RelayCommand]
    private void Reset()
    {
        var defaults = new AppSettings();
        Theme = defaults.Theme;
        Language = ToLanguageLabel(defaults.Language);
        RefreshIntervalSeconds = defaults.RefreshIntervalSeconds;
        TempAlertCelsius = defaults.TempAlertCelsius;
        MinimizeToTray = defaults.MinimizeToTray;
        AlertNetwork = defaults.AlertNetwork;
        AlertStorage = defaults.AlertStorage;
        Save();
    }

    private static string ToLanguageLabel(string code)
        => code switch
        {
            "en" => "English",
            "es" => "Español",
            "pt-BR" => "Português (Brasil)",
            "de" => "Deutsch",
            "ar" => "العربية",
            "ru" => "Русский",
            "zh-Hans" => "中文简体",
            "ja" => "日本語",
            _ => "Français"
        };

    private static string ToLanguageCode(string label)
        => label switch
        {
            "English" => "en",
            "Español" => "es",
            "Português (Brasil)" => "pt-BR",
            "Deutsch" => "de",
            "العربية" => "ar",
            "Русский" => "ru",
            "中文简体" => "zh-Hans",
            "日本語" => "ja",
            _ => "fr"
        };
}
