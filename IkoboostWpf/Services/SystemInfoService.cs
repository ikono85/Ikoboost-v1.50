using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;

namespace IkoboostWpf.Services;

public sealed class SystemInfoService : IDisposable
{
    public sealed record HardwareComponent(string Category, string Name, string Reference, string Value);
    public sealed record CpuDetails(int Cores, int LogicalProcessors, int CurrentClockMhz, int MaxClockMhz);
    public sealed record GpuDetails(string Name, string DriverVersion, long AdapterRamMb);
    public sealed record BoardDetails(string Manufacturer, string Product);
    public sealed record RamDetails(string Summary);
    public sealed record MemoryDetails(long AvailableMb, long TotalMb);

    private PerformanceCounter? _cpuCounter;
    private PerformanceCounter? _ramCounter;
    private List<PerformanceCounter>? _gpuCounters;
    private bool _disposed;

    public SystemInfoService()
    {
        try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); }
        catch { /* compteur indisponible */ }

        try { _ramCounter = new PerformanceCounter("Memory", "Available MBytes"); }
        catch { /* compteur indisponible */ }
    }

    public string MachineName => Environment.MachineName;
    public string UserName => Environment.UserName;
    public string OsVersion => Environment.OSVersion.VersionString;
    public TimeSpan Uptime => TimeSpan.FromMilliseconds(Environment.TickCount64);

    public float GetCpuUsagePercent()
    {
        try { return _cpuCounter?.NextValue() ?? -1f; }
        catch { return -1f; }
    }

    public (long UsedMb, long TotalMb) GetRamUsage()
    {
        try
        {
            var details = GetMemoryDetails();
            return (details.TotalMb - details.AvailableMb, details.TotalMb);
        }
        catch { return (-1, -1); }
    }

    public MemoryDetails GetMemoryDetails()
    {
        var totalMb = GetTotalPhysicalMemoryMb();
        var availableMb = (long?)_ramCounter?.NextValue() ?? 0;
        return new MemoryDetails(availableMb, totalMb);
    }

    public async Task<CpuDetails> GetCpuDetailsAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT NumberOfCores, NumberOfLogicalProcessors, CurrentClockSpeed, MaxClockSpeed FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var cores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                    var logical = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? Environment.ProcessorCount);
                    var currentClock = Convert.ToInt32(obj["CurrentClockSpeed"] ?? 0);
                    var maxClock = Convert.ToInt32(obj["MaxClockSpeed"] ?? 0);
                    return new CpuDetails(cores, logical, currentClock, maxClock);
                }
            }
            catch { }

            return new CpuDetails(0, Environment.ProcessorCount, 0, 0);
        }, ct);
    }

    public async Task<float> GetGpuUsagePercentAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                _gpuCounters ??= CreateGpuCounters();
                if (_gpuCounters.Count == 0)
                    return -1f;

                var total = _gpuCounters.Sum(counter =>
                {
                    try { return counter.NextValue(); }
                    catch { return 0f; }
                });

                return Math.Clamp(total, 0f, 100f);
            }
            catch { return -1f; }
        }, ct);
    }

    public async Task<GpuDetails> GetGpuDetailsAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, DriverVersion, AdapterRAM FROM Win32_VideoController WHERE AdapterCompatibility IS NOT NULL");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString()?.Trim() ?? "Inconnu";
                    var driver = obj["DriverVersion"]?.ToString()?.Trim() ?? "N/A";
                    var ramMb = obj["AdapterRAM"] is null ? 0 : Convert.ToInt64(obj["AdapterRAM"]) / 1024 / 1024;
                    return new GpuDetails(name, driver, ramMb);
                }
            }
            catch { }

            return new GpuDetails("Inconnu", "N/A", 0);
        }, ct);
    }

    public async Task<BoardDetails> GetBoardDetailsAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Manufacturer, Product FROM Win32_BaseBoard");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var manufacturer = obj["Manufacturer"]?.ToString()?.Trim() ?? "N/A";
                    var product = obj["Product"]?.ToString()?.Trim() ?? "N/A";
                    return new BoardDetails(manufacturer, product);
                }
            }
            catch { }

            return new BoardDetails("N/A", "N/A");
        }, ct);
    }

    public async Task<RamDetails> GetRamDetailsAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Manufacturer, PartNumber, Speed, Capacity FROM Win32_PhysicalMemory");
                var modules = new List<string>();
                foreach (ManagementObject obj in searcher.Get())
                {
                    var manufacturer = obj["Manufacturer"]?.ToString()?.Trim();
                    var part = obj["PartNumber"]?.ToString()?.Trim();
                    var speed = obj["Speed"]?.ToString()?.Trim();
                    var capacity = obj["Capacity"] is null ? 0 : Convert.ToUInt64(obj["Capacity"]) / 1024 / 1024 / 1024;

                    var label = string.Join(" ", new[] { manufacturer, part }.Where(s => !string.IsNullOrWhiteSpace(s)));
                    if (string.IsNullOrWhiteSpace(label))
                        label = "Module RAM";

                    modules.Add($"{label} {capacity:N0} Go {speed} MHz".Trim());
                }

                return new RamDetails(modules.Count > 0 ? string.Join(" + ", modules) : "N/A");
            }
            catch { return new RamDetails("N/A"); }
        }, ct);
    }

    public async Task<IReadOnlyList<DriveInfo>> GetDrivesAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
            DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
                .ToList(), ct);
    }

    public async Task<string> GetCpuNameAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor");
                foreach (ManagementObject obj in searcher.Get())
                    return obj["Name"]?.ToString()?.Trim() ?? "Inconnu";
            }
            catch { }
            return "Inconnu";
        }, ct);
    }

    public async Task<string> GetGpuNameAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_VideoController WHERE AdapterCompatibility IS NOT NULL");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var name = obj["Name"]?.ToString()?.Trim();
                    if (!string.IsNullOrEmpty(name)) return name;
                }
            }
            catch { }
            return "Inconnu";
        }, ct);
    }

    public async Task<IReadOnlyList<HardwareComponent>> GetHardwareComponentsAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var components = new List<HardwareComponent>();
            AddWmiComponents(components, "CPU", "Win32_Processor", "Name", "ProcessorId", "MaxClockSpeed", "MHz");
            AddWmiComponents(components, "GPU", "Win32_VideoController", "Name", "PNPDeviceID", "AdapterRAM", "octets");
            AddWmiComponents(components, "Carte mere", "Win32_BaseBoard", "Product", "SerialNumber", "Manufacturer", string.Empty);
            AddWmiComponents(components, "BIOS", "Win32_BIOS", "Name", "SerialNumber", "SMBIOSBIOSVersion", string.Empty);
            AddWmiComponents(components, "RAM", "Win32_PhysicalMemory", "PartNumber", "SerialNumber", "Capacity", "octets");
            AddWmiComponents(components, "Disque", "Win32_DiskDrive", "Model", "SerialNumber", "Size", "octets");
            return components;
        }, ct);
    }

    private static void AddWmiComponents(
        ICollection<HardwareComponent> components,
        string category,
        string className,
        string nameProperty,
        string referenceProperty,
        string valueProperty,
        string valueUnit)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT * FROM {className}");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj[nameProperty]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                    name = "Inconnu";

                var reference = obj[referenceProperty]?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(reference))
                    reference = "N/A";

                var value = FormatHardwareValue(obj[valueProperty], valueUnit);
                components.Add(new HardwareComponent(category, name, reference, value));
            }
        }
        catch
        {
            components.Add(new HardwareComponent(category, "Non disponible", "N/A", "N/A"));
        }
    }

    private static string FormatHardwareValue(object? value, string unit)
    {
        if (value == null)
            return "N/A";

        if (unit == "octets" && ulong.TryParse(value.ToString(), out var bytes))
            return $"{bytes / 1024d / 1024d / 1024d:N1} Go";

        var text = value.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return "N/A";

        return string.IsNullOrWhiteSpace(unit) ? text : $"{text} {unit}";
    }

    private static List<PerformanceCounter> CreateGpuCounters()
    {
        var counters = new List<PerformanceCounter>();

        try
        {
            var category = new PerformanceCounterCategory("GPU Engine");
            foreach (var instance in category.GetInstanceNames())
            {
                if (!instance.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                    continue;

                counters.Add(new PerformanceCounter("GPU Engine", "Utilization Percentage", instance));
            }
        }
        catch { }

        return counters;
    }

    private static long GetTotalPhysicalMemoryMb()
    {
        GetPhysicallyInstalledSystemMemory(out ulong kb);
        return (long)(kb / 1024);
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cpuCounter?.Dispose();
        _ramCounter?.Dispose();
        if (_gpuCounters != null)
        {
            foreach (var counter in _gpuCounters)
                counter.Dispose();
        }
    }
}
