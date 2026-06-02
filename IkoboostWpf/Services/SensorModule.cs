using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using IkoboostWpf.Models;

namespace IkoboostWpf.Services;

// Reads hardware sensors using only Windows-native APIs (WMI ACPI + nvidia-smi).
// No third-party dependencies required.
public sealed class SensorModule : IDisposable
{
    private readonly object _sync = new();
    private List<HardwareSensorReading> _lastReadings = [];
    private DateTime _lastUpdate = DateTime.MinValue;
    private bool _loggedNoTemperatureSensors;
    private readonly string? _nvidiaSmiPath = FindNvidiaSmi();

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
        GetReadings().FirstOrDefault(s => s.Id.Equals(id, StringComparison.OrdinalIgnoreCase) && s.Value > 0);

    private List<HardwareSensorReading> ReadSensors()
    {
        var list = new List<HardwareSensorReading>();
        ReadCpuTemperatures(list);
        ReadGpuNvidia(list);
        ReadGpuLoadWmi(list);
        ReadRamUsage(list);

        if (!list.Any(s => s.Unit == "°C") && !_loggedNoTemperatureSensors)
        {
            _loggedNoTemperatureSensors = true;
            AppLog.Warning("SensorModule", $"Aucun capteur temperature detecte. {list.Count} capteurs lus.");
        }
        return list;
    }

    private static void ReadCpuTemperatures(List<HardwareSensorReading> list)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
            int index = 0;
            foreach (ManagementObject obj in searcher.Get())
            {
                var raw = Convert.ToDouble(obj["CurrentTemperature"]);
                var celsius = (raw - 2732.0) / 10.0;
                if (celsius is <= 0 or >= 130) continue;
                list.Add(new HardwareSensorReading
                {
                    Id = $"cpu-temp-{index}",
                    Type = "CPU",
                    Name = index == 0 ? "CPU Package" : $"Zone {index + 1}",
                    HardwareName = "ACPI",
                    HardwareType = "Cpu",
                    Value = celsius,
                    Unit = "°C",
                    Timestamp = DateTime.Now,
                });
                index++;
            }
        }
        catch (Exception ex) { AppLog.Error("SensorModule.ReadCpuTemperatures", ex); }
    }

    private void ReadGpuNvidia(List<HardwareSensorReading> list)
    {
        if (_nvidiaSmiPath == null) return;
        try
        {
            var psi = new ProcessStartInfo(_nvidiaSmiPath,
                "--query-gpu=temperature.gpu,utilization.gpu,memory.used,memory.total --format=csv,noheader,nounits")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var proc = Process.Start(psi);
            if (proc == null) return;
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(3000);

            var parts = output.Split(',');
            if (parts.Length < 2) return;

            if (double.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var temp) && temp > 0)
            {
                list.Add(new HardwareSensorReading
                {
                    Id = "gpu-temp-0",
                    Type = "GPU",
                    Name = "GPU Core",
                    HardwareName = "NVIDIA GPU",
                    HardwareType = "GpuNvidia",
                    Value = temp,
                    Unit = "°C",
                    Timestamp = DateTime.Now,
                });
            }

            if (double.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var load) && load >= 0)
            {
                list.Add(new HardwareSensorReading
                {
                    Id = "gpu-load-0",
                    Type = "GPU",
                    Name = "GPU Core",
                    HardwareName = "NVIDIA GPU",
                    HardwareType = "GpuNvidia",
                    Value = load,
                    Unit = "%",
                    Timestamp = DateTime.Now,
                });
            }
        }
        catch (Exception ex) { AppLog.Error("SensorModule.ReadGpuNvidia", ex); }
    }

    // Fallback GPU load via Windows performance counters (works for AMD/Intel GPU too)
    private static void ReadGpuLoadWmi(List<HardwareSensorReading> list)
    {
        if (list.Any(s => s.Id == "gpu-load-0")) return;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT UtilizationPercentage FROM Win32_PerfFormattedData_GPUPerformanceCounters_GPUEngine WHERE EngineType LIKE '%3D%'");
            double maxLoad = 0;
            foreach (ManagementObject obj in searcher.Get())
            {
                var val = Convert.ToDouble(obj["UtilizationPercentage"] ?? 0);
                if (val > maxLoad) maxLoad = val;
            }
            if (maxLoad > 0)
            {
                list.Add(new HardwareSensorReading
                {
                    Id = "gpu-load-0",
                    Type = "GPU",
                    Name = "GPU Core",
                    HardwareName = "GPU",
                    HardwareType = "Gpu",
                    Value = maxLoad,
                    Unit = "%",
                    Timestamp = DateTime.Now,
                });
            }
        }
        catch { }
    }

    private static void ReadRamUsage(List<HardwareSensorReading> list)
    {
        try
        {
            using var obj = new ManagementObject("win32_operatingsystem=@");
            obj.Get();
            var totalKb = Convert.ToDouble(obj["TotalVisibleMemorySize"]);
            var freeKb = Convert.ToDouble(obj["FreePhysicalMemory"]);
            var usedGb = (totalKb - freeKb) / 1024.0 / 1024.0;
            var freeGb = freeKb / 1024.0 / 1024.0;

            list.Add(new HardwareSensorReading
            {
                Id = "ram-data-memory-used",
                Type = "RAM",
                Name = "Memory Used",
                HardwareName = "RAM",
                HardwareType = "Memory",
                Value = usedGb,
                Unit = "GB",
                Timestamp = DateTime.Now,
            });
            list.Add(new HardwareSensorReading
            {
                Id = "ram-data-memory-available",
                Type = "RAM",
                Name = "Memory Available",
                HardwareName = "RAM",
                HardwareType = "Memory",
                Value = freeGb,
                Unit = "GB",
                Timestamp = DateTime.Now,
            });
        }
        catch (Exception ex) { AppLog.Error("SensorModule.ReadRamUsage", ex); }
    }

    private static string? FindNvidiaSmi()
    {
        var paths = new[]
        {
            @"C:\Windows\System32\nvidia-smi.exe",
            @"C:\Program Files\NVIDIA Corporation\NVSMI\nvidia-smi.exe",
        };
        return paths.FirstOrDefault(File.Exists);
    }

    private static int ScoreSensorName(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("package") || n.Contains("core") || n.Contains("gpu core")) return 100;
        if (n.Contains("cpu") || n.Contains("gpu")) return 80;
        return 0;
    }

    public void Dispose() { }
}
