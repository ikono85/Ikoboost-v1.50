using System.IO;
using System.Text.Json;

namespace IkoboostWpf.Services;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string Language { get; set; } = "fr";
    public string AccentTheme { get; set; } = "midnight";
    public string DiscoverySource { get; set; } = string.Empty;
    public bool OnboardingCompleted { get; set; }
    public int RefreshIntervalSeconds { get; set; } = 2;
    public float TempAlertCelsius { get; set; } = 85f;
    public bool MinimizeToTray { get; set; } = true;
    public bool AlertNetwork { get; set; } = true;
    public bool AlertStorage { get; set; } = true;
}

public sealed class SettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Ikoboost");
    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public event EventHandler<AppSettings>? SettingsChanged;

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new AppSettings();
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            return Normalize(settings);
        }
        catch { return new AppSettings(); }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(SettingsDir);
        var tmp = SettingsPath + ".tmp";
        var normalized = Normalize(settings);
        var json = JsonSerializer.Serialize(normalized, JsonOptions);
        File.WriteAllText(tmp, json);
        File.Move(tmp, SettingsPath, overwrite: true);
        SettingsChanged?.Invoke(this, normalized);
    }

    private static AppSettings Normalize(AppSettings settings)
    {
        var normalized = new AppSettings
        {
            Theme = settings.Theme is "Dark" or "Light" or "Cybertek" or "Cyberpunk" or "Video"
                ? settings.Theme
                : "Dark",
            Language = settings.Language is "fr" or "en" or "es" or "pt-BR" or "de" or "ar" or "ru" or "zh-Hans" or "ja"
                ? settings.Language
                : "fr",
            AccentTheme = settings.AccentTheme is "midnight" or "ocean" or "forest" or "sunset" or "violet" or "rose"
                ? settings.AccentTheme
                : "midnight",
            DiscoverySource = settings.DiscoverySource ?? string.Empty,
            OnboardingCompleted = settings.OnboardingCompleted,
            RefreshIntervalSeconds = settings.RefreshIntervalSeconds is >= 1 and <= 60
                ? settings.RefreshIntervalSeconds
                : 2,
            TempAlertCelsius = float.IsFinite(settings.TempAlertCelsius) && settings.TempAlertCelsius is >= 40f and <= 120f
                ? settings.TempAlertCelsius
                : 85f,
            MinimizeToTray = settings.MinimizeToTray,
            AlertNetwork = settings.AlertNetwork,
            AlertStorage = settings.AlertStorage
        };

        return normalized;
    }
}
