using System.Collections.Generic;
using System.Management;
using IkoboostWpf.Models;

namespace IkoboostWpf.Services;

public sealed class HardwareService
{
    public List<SensorItem> GetSensors()
    {
        var list = new List<SensorItem>();

        AddCpu(list);
        AddGpu(list);
        AddMemory(list);
        AddMotherboard(list);
        AddLibreHardwareSensors(list);
        AddFans(list);
        AddDisks(list);
        AddOperatingSystem(list);
        AddThermalZones(list);
        EnsureTemperatureRows(list);

        return list;
    }

    private static void AddLibreHardwareSensors(List<SensorItem> list)
    {
        try
        {
            var readings = SensorModuleProvider.Shared.GetReadings(force: true);
            foreach (var reading in readings)
            {
                var category = reading.Type switch
                {
                    "RAM" => "Memoire",
                    "MOTHERBOARD" => "Carte mere",
                    "STORAGE" => "Stockage",
                    _ => reading.Type,
                };

                var level = LevelFor(reading);
                list.Add(new SensorItem
                {
                    Category = category,
                    Name = DisplaySensorName(reading),
                    Reference = reading.HardwareName,
                    Value = FormatSensorValue(reading),
                    Level = level,
                    ValuePercent = PercentFor(reading),
                });
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("HardwareService.AddLibreHardwareSensors", ex);
        }
    }

    private static string DisplaySensorName(HardwareSensorReading reading)
    {
        if (reading.Unit == "\u00B0C" && reading.Type == "CPU") return reading.Name.Contains("package", StringComparison.OrdinalIgnoreCase) ? "Package" : reading.Name;
        if (reading.Unit == "\u00B0C" && reading.Type == "GPU") return reading.Name;
        if (reading.Unit == "%") return $"{reading.Name} (charge)";
        if (reading.Unit == "RPM") return reading.Name;
        if (reading.Unit == "W") return $"{reading.Name} (puissance)";
        return reading.Name;
    }

    private static string FormatSensorValue(HardwareSensorReading reading) => reading.Unit switch
    {
        "\u00B0C" => $"{reading.Value:F0} \u00B0C",
        "%" => $"{reading.Value:F1} %",
        "GB" => $"{reading.Value:F1} Go",
        "RPM" => $"{reading.Value:F0} RPM",
        "W" => $"{reading.Value:F1} W",
        _ => $"{reading.Value:F1} {reading.Unit}".Trim()
    };

    private static string LevelFor(HardwareSensorReading reading)
    {
        if (reading.Unit == "\u00B0C")
            return reading.Value > 85 ? "Critical" : reading.Value > 70 ? "Warning" : "Normal";
        if (reading.Unit == "%")
            return reading.Value > 95 ? "Critical" : reading.Value > 80 ? "Warning" : "Normal";
        return "Normal";
    }

    private static double PercentFor(HardwareSensorReading reading) => reading.Unit switch
    {
        "\u00B0C" => Math.Clamp(reading.Value, 0, 100),
        "%" => Math.Clamp(reading.Value, 0, 100),
        "RPM" => Math.Clamp(reading.Value / 3000.0 * 100.0, 0, 100),
        "W" => Math.Clamp(reading.Value / 250.0 * 100.0, 0, 100),
        "GB" => Math.Clamp(reading.Value / 64.0 * 100.0, 0, 100),
        _ => 0
    };

    private static void EnsureTemperatureRows(List<SensorItem> list)
    {
        EnsureTemperatureRow(list, "CPU", "Temperature", "Package CPU");
        EnsureTemperatureRow(list, "GPU", "Temperature", "Core GPU");
        EnsureTemperatureRow(list, "Carte mere", "Temperature", "Capteur carte mere");
    }

    private static void EnsureTemperatureRow(List<SensorItem> list, string category, string name, string fallbackReference)
    {
        if (list.Any(i => i.Category == category && i.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;

        var source = list.FirstOrDefault(i => i.Category == category && IsTemperatureValue(i.Value));
        if (source != null)
        {
            list.Add(new SensorItem
            {
                Category = category,
                Name = name,
                Reference = string.IsNullOrWhiteSpace(source.Reference) ? fallbackReference : source.Reference,
                Value = source.Value,
                Level = source.Level,
                ValuePercent = source.ValuePercent
            });
            return;
        }

        list.Add(new SensorItem
        {
            Category = category,
            Name = name,
            Reference = fallbackReference,
            Value = "N/A",
            Level = "Normal",
            ValuePercent = 0
        });
    }

    private static bool IsTemperatureValue(string value) =>
        value.Contains("\u00B0C", StringComparison.OrdinalIgnoreCase)
        || value.Contains(" C", StringComparison.OrdinalIgnoreCase);

    public string GetStatusMessage()
    {
        if (!new System.Security.Principal.WindowsPrincipal(
                System.Security.Principal.WindowsIdentity.GetCurrent())
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator))
            return "Certains capteurs necessitent les droits administrateur. Relancez en tant qu'administrateur pour plus de donnees.";
        return "";
    }

    private static void AddCpu(List<SensorItem> list)
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (ManagementObject obj in s.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim() ?? "Processeur";
                list.Add(new SensorItem
                {
                    Category = "CPU",
                    Name = "Package",
                    Reference = name,
                    Value = $"{obj["NumberOfCores"]} coeurs / {obj["NumberOfLogicalProcessors"]} threads",
                    Level = "Normal",
                    ValuePercent = 100
                });
                list.Add(new SensorItem
                {
                    Category = "CPU",
                    Name = "Frequence",
                    Reference = "Actuelle",
                    Value = $"{obj["CurrentClockSpeed"]} MHz",
                    Level = "Normal",
                    ValuePercent = PercentFromValues(obj["CurrentClockSpeed"], obj["MaxClockSpeed"])
                });
                list.Add(new SensorItem
                {
                    Category = "CPU",
                    Name = "Frequence max",
                    Reference = "Turbo / base max",
                    Value = $"{obj["MaxClockSpeed"]} MHz",
                    Level = "Normal",
                    ValuePercent = 100
                });
                list.Add(new SensorItem
                {
                    Category = "CPU",
                    Name = "Socket",
                    Reference = name,
                    Value = obj["SocketDesignation"]?.ToString() ?? "N/A",
                    Level = "Normal"
                });
            }
        }
        catch { }
    }

    private static void AddGpu(List<SensorItem> list)
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (ManagementObject obj in s.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim() ?? "";
                if (name.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase)) continue;

                long vram = Convert.ToInt64(obj["AdapterRAM"] ?? 0L);
                list.Add(new SensorItem
                {
                    Category = "GPU",
                    Name = "Core",
                    Reference = name,
                    Value = vram > 0 ? $"{vram / 1024 / 1024} Mo VRAM" : "N/A",
                    Level = "Normal",
                    ValuePercent = 70
                });
                list.Add(new SensorItem
                {
                    Category = "GPU",
                    Name = "Pilote",
                    Reference = name,
                    Value = obj["DriverVersion"]?.ToString() ?? "N/A",
                    Level = "Normal"
                });
            }
        }
        catch { }
    }

    private static void AddMemory(List<SensorItem> list)
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
            int slot = 1;
            foreach (ManagementObject obj in s.Get())
            {
                long cap = Convert.ToInt64(obj["Capacity"] ?? 0L);
                list.Add(new SensorItem
                {
                    Category = "Memoire",
                    Name = $"Slot {slot++}",
                    Reference = obj["Manufacturer"]?.ToString()?.Trim() ?? "",
                    Value = $"{cap / 1024 / 1024 / 1024} Go @ {obj["Speed"]} MHz",
                    Level = "Normal",
                    ValuePercent = 100
                });
            }
        }
        catch { }
    }

    private static void AddMotherboard(List<SensorItem> list)
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            foreach (ManagementObject obj in s.Get())
            {
                list.Add(new SensorItem
                {
                    Category = "Carte mere",
                    Name = "Modele",
                    Reference = $"{obj["Manufacturer"]} {obj["Product"]}".Trim(),
                    Value = obj["SerialNumber"]?.ToString() ?? "N/A",
                    Level = "Normal"
                });
            }
        }
        catch { }
    }

    private static void AddFans(List<SensorItem> list)
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT * FROM Win32_Fan");
            int index = 1;
            foreach (ManagementObject obj in s.Get())
            {
                var speed = obj["DesiredSpeed"]?.ToString();
                list.Add(new SensorItem
                {
                    Category = "Ventilateurs",
                    Name = obj["Name"]?.ToString()?.Trim() ?? $"Ventilateur #{index}",
                    Reference = obj["DeviceID"]?.ToString() ?? "",
                    Value = string.IsNullOrWhiteSpace(speed) ? "N/A" : $"{speed} RPM",
                    Level = "Normal",
                    ValuePercent = PercentFromText(speed, 3000)
                });
                index++;
            }
        }
        catch { }
    }

    private static void AddDisks(List<SensorItem> list)
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT * FROM Win32_DiskDrive");
            foreach (ManagementObject obj in s.Get())
            {
                long size = Convert.ToInt64(obj["Size"] ?? 0L);
                list.Add(new SensorItem
                {
                    Category = "Stockage",
                    Name = obj["Model"]?.ToString()?.Trim() ?? "Disque",
                    Reference = $"Interface: {obj["InterfaceType"]}",
                    Value = size > 0 ? $"{size / 1024 / 1024 / 1024} Go" : "N/A",
                    Level = "Normal",
                    ValuePercent = 100
                });
            }
        }
        catch { }

        try
        {
            foreach (var drive in System.IO.DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var pct = drive.TotalSize > 0
                    ? (drive.TotalSize - drive.AvailableFreeSpace) * 100.0 / drive.TotalSize
                    : 0;
                list.Add(new SensorItem
                {
                    Category = "Stockage",
                    Name = drive.Name.TrimEnd('\\'),
                    Reference = string.IsNullOrWhiteSpace(drive.VolumeLabel) ? drive.DriveFormat : drive.VolumeLabel,
                    Value = $"{pct:F0}% utilises",
                    Level = pct > 92 ? "Critical" : pct > 80 ? "Warning" : "Normal",
                    ValuePercent = pct
                });
            }
        }
        catch { }
    }

    private static void AddOperatingSystem(List<SensorItem> list)
    {
        try
        {
            using var s = new ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
            foreach (ManagementObject obj in s.Get())
            {
                list.Add(new SensorItem
                {
                    Category = "Systeme",
                    Name = "OS",
                    Reference = obj["Caption"]?.ToString() ?? "",
                    Value = obj["Version"]?.ToString() ?? "",
                    Level = "Normal"
                });
                list.Add(new SensorItem
                {
                    Category = "Systeme",
                    Name = "Architecture",
                    Reference = "",
                    Value = obj["OSArchitecture"]?.ToString() ?? "",
                    Level = "Normal"
                });
            }
        }
        catch { }
    }

    private static void AddThermalZones(List<SensorItem> list)
    {
        try
        {
            using var s = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
            int i = 1;
            foreach (ManagementObject obj in s.Get())
            {
                var t = (Convert.ToDouble(obj["CurrentTemperature"]) - 2732) / 10.0;
                var level = t > 85 ? "Critical" : t > 70 ? "Warning" : "Normal";
                list.Add(new SensorItem
                {
                    Category = i == 1 ? "CPU" : "Temperatures",
                    Name = i == 1 ? "Temperature package" : $"Zone thermique {i}",
                    Reference = "ACPI",
                    Value = $"{t:F1} C",
                    Level = level,
                    ValuePercent = Math.Clamp(t, 0, 100)
                });
                i++;
            }
        }
        catch { }
    }

    private static double PercentFromValues(object? currentValue, object? maxValue)
    {
        if (!double.TryParse(currentValue?.ToString(), out var current)) return 0;
        if (!double.TryParse(maxValue?.ToString(), out var max) || max <= 0) return 0;
        return Math.Clamp(current / max * 100.0, 0, 100);
    }

    private static double PercentFromText(string? text, double max)
    {
        if (string.IsNullOrWhiteSpace(text) || max <= 0) return 0;
        var match = System.Text.RegularExpressions.Regex.Match(text, @"\d+([.,]\d+)?");
        if (!match.Success) return 0;
        if (!double.TryParse(match.Value.Replace(',', '.'),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var value)) return 0;
        return Math.Clamp(value / max * 100.0, 0, 100);
    }
}
