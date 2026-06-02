using System.IO;
using System.Text.Json;

namespace IkoboostWpf.Services;

public sealed class SettingsService
{
    private static readonly string _path = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Ikoboost", "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path)) ?? new();
        }
        catch { }
        return new();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}

public class AppSettings
{
    public string Theme { get; set; } = "Sombre";
    public string Language { get; set; } = "Français";
    public int RefreshIntervalSeconds { get; set; } = 1;
    public int TempAlertCelsius { get; set; } = 85;
    public bool AlertNetwork { get; set; } = true;
    public bool AlertStorage { get; set; } = true;
    public bool MinimizeToTray { get; set; } = false;
}
