using IkoboostWpf.Models;
using LibreHardwareMonitor.Hardware;

namespace IkoboostWpf.Services;

public sealed class SensorModule : IDisposable
{
    private readonly Computer _computer;
    private readonly object _sync = new();
    private List<HardwareSensorReading> _lastReadings = [];
    private DateTime _lastUpdate = DateTime.MinValue;
    private bool _disposed;
    private bool _loggedNoTemperatureSensors;

    public SensorModule()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsStorageEnabled = true,
        };

        try { _computer.Open(); }
        catch (Exception ex) { AppLog.Error("SensorModule.Open", ex); }
    }

    public IReadOnlyList<HardwareSensorReading> GetReadings(bool force = false)
    {
        lock (_sync)
        {
            if (!force && DateTime.Now - _lastUpdate < TimeSpan.FromMilliseconds(800))
                return _lastReadings;

            _lastReadings = ReadSensors();
            _lastUpdate = DateTime.Now;
            return _lastReadings;
        }
    }

    public HardwareSensorReading? Find(string type, string unit, Func<HardwareSensorReading, bool>? predicate = null) =>
        GetReadings()
            .Where(s => s.Type == type && s.Unit == unit && s.Value > 0)
            .Where(s => predicate?.Invoke(s) ?? true)
            .OrderByDescending(s => ScoreSensorName(s.Name))
            .ThenBy(s => s.Id)
            .FirstOrDefault();

    public HardwareSensorReading? FindById(string id) =>
        GetReadings()
            .FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && s.Value > 0);

    private List<HardwareSensorReading> ReadSensors()
    {
        var list = new List<HardwareSensorReading>();
        try
        {
            foreach (var hardware in _computer.Hardware)
            {
                UpdateHardware(hardware);
                ReadHardwareSensors(hardware, list);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("SensorModule.ReadSensors", ex);
        }

        if (!list.Any(s => s.Unit == "\u00B0C") && !_loggedNoTemperatureSensors)
        {
            _loggedNoTemperatureSensors = true;
            var sample = string.Join(" | ", list.Take(12)
                .Select(s => $"{s.Type}/{s.Name}/{s.Unit}/{s.Value:F1}"));
            var raw = string.Join(" | ", _computer.Hardware.Select(h =>
                $"{h.HardwareType}:{h.Name} sensors={h.Sensors.Length} sub={h.SubHardware.Length}"));
            AppLog.Warning("SensorModule", $"Aucun capteur temperature detecte. Capteurs lus: {list.Count}. {sample}. Hardware: {raw}");
        }

        return list;
    }

    private static void UpdateHardware(IHardware hardware)
    {
        try
        {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware)
                UpdateHardware(subHardware);
        }
        catch (Exception ex)
        {
            AppLog.Error("SensorModule.UpdateHardware", ex);
        }
    }

    private static void ReadHardwareSensors(IHardware hardware, List<HardwareSensorReading> list)
    {
        foreach (var sensor in hardware.Sensors)
        {
            if (sensor.Value is not { } value || value <= 0) continue;
            if (!IsSupportedSensor(sensor.SensorType)) continue;

            var type = MapHardwareType(hardware.HardwareType);
            if (type == "Other") continue;

            list.Add(new HardwareSensorReading
            {
                Id = BuildId(type, sensor),
                Type = type,
                Name = sensor.Name,
                HardwareName = hardware.Name,
                HardwareType = hardware.HardwareType.ToString(),
                Value = value,
                Unit = UnitFor(sensor.SensorType),
                Timestamp = DateTime.Now,
            });
        }

        foreach (var subHardware in hardware.SubHardware)
            ReadHardwareSensors(subHardware, list);
    }

    private static bool IsSupportedSensor(SensorType type) => type is
        SensorType.Temperature or
        SensorType.Load or
        SensorType.Data or
        SensorType.Fan or
        SensorType.Power;

    private static string MapHardwareType(HardwareType type) => type switch
    {
        HardwareType.Cpu => "CPU",
        HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel => "GPU",
        HardwareType.Memory => "RAM",
        HardwareType.Motherboard or HardwareType.SuperIO => "MOTHERBOARD",
        HardwareType.Storage => "STORAGE",
        _ => "Other",
    };

    private static string UnitFor(SensorType type) => type switch
    {
        SensorType.Temperature => "\u00B0C",
        SensorType.Load => "%",
        SensorType.Data => "GB",
        SensorType.Fan => "RPM",
        SensorType.Power => "W",
        _ => "",
    };

    private static string BuildId(string type, ISensor sensor)
    {
        var sensorKind = sensor.SensorType switch
        {
            SensorType.Temperature => "temp",
            SensorType.Load => "load",
            SensorType.Data => "data",
            SensorType.Fan => "fan",
            SensorType.Power => "power",
            _ => "sensor",
        };

        if (type == "RAM" && sensor.SensorType == SensorType.Data)
        {
            var normalizedName = NormalizeSensorName(sensor.Name);
            return $"ram-data-{normalizedName}";
        }

        return $"{type.ToLowerInvariant()}-{sensorKind}-{sensor.Index}";
    }

    private static string NormalizeSensorName(string name)
    {
        var chars = name
            .Trim()
            .ToLowerInvariant()
            .Select(c => char.IsLetterOrDigit(c) ? c : '-')
            .ToArray();

        var normalized = new string(chars);
        while (normalized.Contains("--", StringComparison.Ordinal))
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        return normalized.Trim('-');
    }

    private static int ScoreSensorName(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("package") || n.Contains("core") || n.Contains("gpu core")) return 100;
        if (n.Contains("cpu") || n.Contains("gpu")) return 80;
        if (n.Contains("hot spot") || n.Contains("hotspot")) return 70;
        return 0;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _computer.Close(); }
        catch { }
    }
}
