using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IkoboostWpf.Models;
using IkoboostWpf.Services;

namespace IkoboostWpf.ViewModels;

public partial class HardwareViewModel : ObservableObject
{
    private readonly HardwareService _service = new();

    private static readonly Brush Cyan = new SolidColorBrush(Color.FromRgb(0x2F, 0xE6, 0xF2));
    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x38, 0xD9, 0x96));
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(0xF5, 0xB5, 0x3D));
    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xFB, 0x5E, 0x63));
    private static readonly Brush Blue = new SolidColorBrush(Color.FromRgb(0x4D, 0x8D, 0xF6));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x8F, 0xA0, 0xB2));
    private static readonly Brush GreenBg = new SolidColorBrush(Color.FromArgb(0x22, 0x38, 0xD9, 0x96));
    private static readonly Brush AmberBg = new SolidColorBrush(Color.FromArgb(0x22, 0xF5, 0xB5, 0x3D));
    private static readonly Brush RedBg = new SolidColorBrush(Color.FromArgb(0x22, 0xFB, 0x5E, 0x63));
    private static readonly Brush CyanBg = new SolidColorBrush(Color.FromArgb(0x18, 0x2F, 0xE6, 0xF2));

    [ObservableProperty] private string _statusMessage = "";
    [ObservableProperty] private string _sensorStatus = "";
    [ObservableProperty] private string _sensorCountText = "";
    [ObservableProperty] private ObservableCollection<SensorItem> _sensors = [];
    [ObservableProperty] private ObservableCollection<HardwareSummaryCard> _summaryCards = [];
    [ObservableProperty] private ObservableCollection<HardwareSensorGroup> _sensorGroups = [];

    public HardwareViewModel()
    {
        StatusMessage = _service.GetStatusMessage();
        RefreshCommand.Execute(null);
    }

    [RelayCommand]
    private void Refresh()
    {
        var items = _service.GetSensors();
        foreach (var item in items)
            Decorate(item);

        Sensors = new ObservableCollection<SensorItem>(items);
        SensorGroups = BuildGroups(items);
        SummaryCards = BuildSummaryCards(items);
        SensorCountText = $"{items.Count} capteurs";
        SensorStatus = items.Any(i => IsLibreHardwareSensor(i)) ? "LHM/WMI connecte" : "WMI connecte";
    }

    [RelayCommand]
    private void OpenProductPage(SensorItem? item)
    {
        if (item == null || string.IsNullOrEmpty(item.ProductUrl)) return;
        try { Process.Start(new ProcessStartInfo(item.ProductUrl) { UseShellExecute = true }); }
        catch (Exception ex) { AppLog.Error("HardwareViewModel.OpenProductPage", ex); }
    }

    private static ObservableCollection<HardwareSensorGroup> BuildGroups(IEnumerable<SensorItem> items)
    {
        var order = new[] { "CPU", "GPU", "Carte mere", "Memoire", "Ventilateurs", "Stockage", "Systeme", "Temperatures" };
        var groups = new ObservableCollection<HardwareSensorGroup>();

        foreach (var name in order)
        {
            var groupItems = items.Where(i => i.Category == name).ToList();
            if (groupItems.Count == 0) continue;
            groups.Add(new HardwareSensorGroup
            {
                Name = DisplayCategory(name),
                Items = new ObservableCollection<SensorItem>(groupItems)
            });
        }

        foreach (var extra in items.Select(i => i.Category).Distinct().Where(c => !order.Contains(c)))
        {
            groups.Add(new HardwareSensorGroup
            {
                Name = DisplayCategory(extra),
                Items = new ObservableCollection<SensorItem>(items.Where(i => i.Category == extra))
            });
        }

        return groups;
    }

    private static ObservableCollection<HardwareSummaryCard> BuildSummaryCards(IReadOnlyCollection<SensorItem> items)
    {
        var cards = new ObservableCollection<HardwareSummaryCard>();
        var cpu = items.FirstOrDefault(i => i.Category == "CPU" && IsTemperature(i))
            ?? items.FirstOrDefault(i => i.Category == "CPU");
        var gpu = items.FirstOrDefault(i => i.Category == "GPU" && IsTemperature(i))
            ?? items.FirstOrDefault(i => i.Category == "GPU");
        var board = items.FirstOrDefault(i => i.Category == "Carte mere");
        var drive = items.Where(i => i.Category == "Stockage")
            .OrderByDescending(i => i.Level == "Critique")
            .ThenByDescending(i => i.Level == "Avertissement")
            .ThenByDescending(i => i.ValuePercent)
            .FirstOrDefault();

        cards.Add(CreateCard("CPU Package", cpu, "\uE950", Cyan));
        cards.Add(CreateCard("GPU Core", gpu, "\uE7F4", Green));
        cards.Add(CreateCard("Carte mere", board, "\uE964", Amber));
        cards.Add(CreateCard("Stockage", drive, "\uE8B7", Red));
        return cards;
    }

    private static HardwareSummaryCard CreateCard(string title, SensorItem? item, string glyph, Brush fallback)
    {
        var value = item?.Value ?? "N/A";
        var unit = "";
        if (TryParseNumber(value, out var n))
        {
            value = n % 1 == 0 ? $"{n:F0}" : $"{n:F1}";
            unit = IsTemperature(item) ? "C" : UnitFromValue(item?.Value ?? "");
        }

        return new HardwareSummaryCard
        {
            Title = title,
            Value = value,
            Unit = unit,
            Subtitle = item?.Reference ?? "Non detecte",
            Glyph = glyph,
            AccentBrush = item?.AccentBrush ?? fallback,
            BackgroundBrush = item?.LevelBackground ?? CyanBg
        };
    }

    private static void Decorate(SensorItem item)
    {
        item.Level = item.Level switch
        {
            "Critical" => "Critique",
            "Warning" => "Avertissement",
            "Normal" => "Normal",
            _ => item.Level
        };

        if (item.ValuePercent <= 0)
            item.ValuePercent = InferPercent(item);

        item.AccentBrush = item.Level switch
        {
            "Critique" => Red,
            "Avertissement" => Amber,
            _ => IsTemperature(item) ? TemperatureBrush(item.Value) : Green
        };
        item.LevelBrush = item.Level switch
        {
            "Critique" => Red,
            "Avertissement" => Amber,
            _ => Green
        };
        item.LevelBackground = item.Level switch
        {
            "Critique" => RedBg,
            "Avertissement" => AmberBg,
            _ => GreenBg
        };
    }

    private static double InferPercent(SensorItem item)
    {
        if (!TryParseNumber(item.Value, out var number)) return 0;
        if (IsTemperature(item)) return Math.Clamp(number, 0, 100);
        if (item.Value.Contains("RPM", StringComparison.OrdinalIgnoreCase)) return Math.Clamp(number / 3000 * 100, 0, 100);
        if (item.Value.Contains("%", StringComparison.OrdinalIgnoreCase)) return Math.Clamp(number, 0, 100);
        return 0;
    }

    private static Brush TemperatureBrush(string value)
    {
        if (!TryParseNumber(value, out var t)) return Muted;
        return t > 85 ? Red : t > 70 ? Amber : Green;
    }

    private static bool IsTemperature(SensorItem? item)
    {
        if (item == null) return false;
        return item.Name.Contains("temp", StringComparison.OrdinalIgnoreCase)
            || item.Value.Contains(" C", StringComparison.OrdinalIgnoreCase)
            || item.Value.Contains("\u00B0C", StringComparison.OrdinalIgnoreCase)
            || item.Value.Contains("°", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLibreHardwareSensor(SensorItem item) =>
        IsTemperature(item)
        || item.Value.Contains("RPM", StringComparison.OrdinalIgnoreCase)
        || item.Value.Contains(" W", StringComparison.OrdinalIgnoreCase)
        || item.Name.Contains("charge", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseNumber(string text, out double value)
    {
        value = 0;
        var match = System.Text.RegularExpressions.Regex.Match(text, @"-?\d+([.,]\d+)?");
        return match.Success && double.TryParse(match.Value.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private static string UnitFromValue(string value)
    {
        if (value.Contains("RPM", StringComparison.OrdinalIgnoreCase)) return "RPM";
        if (value.Contains("%", StringComparison.OrdinalIgnoreCase)) return "%";
        if (value.Contains("MHz", StringComparison.OrdinalIgnoreCase)) return "MHz";
        if (value.Contains("Go", StringComparison.OrdinalIgnoreCase)) return "Go";
        if (value.Contains("Mo", StringComparison.OrdinalIgnoreCase)) return "Mo";
        return "";
    }

    private static string DisplayCategory(string category) => category switch
    {
        "Carte mere" => "CARTE MERE",
        "Memoire" => "MEMOIRE",
        "Systeme" => "SYSTEME",
        "Temperatures" => "TEMPERATURES",
        _ => category.ToUpperInvariant()
    };
}
