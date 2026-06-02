using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows.Threading;
using IkoboostWpf.Models;

namespace IkoboostWpf.Services;

public sealed class SystemMonitorService : IDisposable
{
    private readonly PerformanceCounter? _cpuCounter;
    private readonly DispatcherTimer _timer;
    private readonly SensorModule _sensorModule = SensorModuleProvider.Shared;
    private NetworkInterface? _activeAdapter;
    private long _prevBytesIn, _prevBytesOut;
    private DateTime _nextPingAt = DateTime.MinValue;
    private bool _isPinging;
    private bool _loggedTemperatureUnavailable;
    private const int HistorySize = 60;

    public double CpuPercent { get; private set; }
    public double RamPercent { get; private set; }
    public double RamUsedMb { get; private set; }
    public double RamTotalMb { get; private set; }
    public double GpuPercent { get; private set; }
    public double NetworkInMbps { get; private set; }
    public double NetworkOutMbps { get; private set; }
    public string NetworkAdapterName { get; private set; } = "";
    public string NetworkAddress { get; private set; } = "";
    public string NetworkSpeed { get; private set; } = "";
    public string NetworkDns { get; private set; } = "";
    public int PingMs { get; private set; }

    public string CpuTempText { get; private set; } = "N/A";
    public string GpuTempText { get; private set; } = "N/A";
    public string MoboTempText { get; private set; } = "N/A";
    public string CpuTempDiagnostic { get; private set; } = "Capteur CPU non detecte";

    public Queue<double> CpuHistory { get; } = new();
    public Queue<double> RamHistory { get; } = new();
    public Queue<double> GpuHistory { get; } = new();
    public Queue<double> NetworkInHistory { get; } = new();

    public string CpuName { get; private set; } = "";
    public string GpuName { get; private set; } = "";
    public string GpuDriver { get; private set; } = "";
    public string GpuMemory { get; private set; } = "";
    public string RamReference { get; private set; } = "";
    public string MotherboardReference { get; private set; } = "";
    public string CpuDetails { get; private set; } = "";
    public string CpuCurrentClockMhz { get; private set; } = "";
    public string CpuMaxClockMhz { get; private set; } = "";
    public string CpuTemperature { get; private set; } = "";
    public string Uptime { get; private set; } = "";

    public event Action? DataUpdated;

    public SystemMonitorService()
    {
        try { _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); }
        catch { }

        LoadStaticInfo();
        DetectNetworkAdapter();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();
    }

    private void LoadStaticInfo()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                CpuName = obj["Name"]?.ToString()?.Trim() ?? "";
                CpuMaxClockMhz = obj["MaxClockSpeed"]?.ToString() ?? "";
                CpuCurrentClockMhz = obj["CurrentClockSpeed"]?.ToString() ?? "";
                CpuDetails = $"{obj["NumberOfCores"]} cœurs / {obj["NumberOfLogicalProcessors"]} threads";
                break;
            }
        }
        catch { CpuName = "Processeur inconnu"; }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["Name"]?.ToString()?.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase) == true) continue;
                GpuName = obj["Name"]?.ToString()?.Trim() ?? "";
                GpuDriver = obj["DriverVersion"]?.ToString() ?? "";
                var vramBytes = Convert.ToInt64(obj["AdapterRAM"] ?? 0L);
                GpuMemory = vramBytes > 0 ? $"{vramBytes / 1024 / 1024} Mo VRAM" : "N/A";
                break;
            }
        }
        catch { GpuName = "GPU inconnu"; }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PhysicalMemory");
            long totalKb = 0;
            var speeds = new List<string>();
            foreach (ManagementObject obj in searcher.Get())
            {
                totalKb += Convert.ToInt64(obj["Capacity"] ?? 0L);
                var spd = obj["Speed"]?.ToString();
                if (!string.IsNullOrEmpty(spd) && !speeds.Contains(spd)) speeds.Add(spd);
            }
            RamReference = $"{totalKb / 1024 / 1024 / 1024} Go" + (speeds.Count > 0 ? $" DDR @ {string.Join("/", speeds)} MHz" : "");
        }
        catch { }

        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_BaseBoard");
            foreach (ManagementObject obj in searcher.Get())
            {
                MotherboardReference = $"{obj["Manufacturer"]} {obj["Product"]}".Trim();
                break;
            }
        }
        catch { }
    }

    private void DetectNetworkAdapter()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var stats = ni.GetIPStatistics();
                if (stats.BytesReceived > 0)
                {
                    _activeAdapter = ni;
                    var ipProps = ni.GetIPProperties();
                    NetworkAdapterName = ni.Name;
                    NetworkAddress = ipProps.UnicastAddresses
                        .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        ?.Address.ToString() ?? "";
                    NetworkSpeed = ni.Speed > 0 ? $"{ni.Speed / 1_000_000} Mbps" : "";
                    var dns = ipProps.DnsAddresses;
                    NetworkDns = dns.Count > 0 ? string.Join(", ", dns.Select(a => a.ToString())) : "";
                    var s = ni.GetIPStatistics();
                    _prevBytesIn = s.BytesReceived;
                    _prevBytesOut = s.BytesSent;
                    break;
                }
            }
        }
        catch { }
    }

    private void Poll()
    {
        try { CpuPercent = Math.Round(_cpuCounter?.NextValue() ?? 0, 1); } catch { }

        var sensors = _sensorModule.GetReadings();
        ApplySensorReadings(sensors);

        try
        {
            using var os = new ManagementObject("win32_operatingsystem=@");
            os.Get();
            var total = Convert.ToDouble(os["TotalVisibleMemorySize"]);
            var free = Convert.ToDouble(os["FreePhysicalMemory"]);
            RamTotalMb = total / 1024.0;
            var usedKb = total - free;
            RamUsedMb = usedKb / 1024.0;
            RamPercent = total > 0 ? Math.Round(usedKb / total * 100, 1) : 0;
        }
        catch { }

        PollNetwork();
        PollPing();
        PollUptime();
        if (CpuTempText == "N/A")
            TryGetTemperatures();

        EnqueueHistory(CpuHistory, CpuPercent);
        EnqueueHistory(RamHistory, RamPercent);
        EnqueueHistory(GpuHistory, GpuPercent);
        EnqueueHistory(NetworkInHistory, NetworkInMbps);

        DataUpdated?.Invoke();
    }

    private void ApplySensorReadings(IReadOnlyList<HardwareSensorReading> sensors)
    {
        var cpuTemp = sensors
            .Where(s => s.Unit == "\u00B0C" && (s.Type == "CPU" || LooksLikeCpuTemperature(s)))
            .OrderByDescending(s => SensorNameScore(s.Name))
            .FirstOrDefault();
        if (cpuTemp != null)
        {
            CpuTempText = $"{cpuTemp.Value:F0}\u00B0C";
            CpuTempDiagnostic = $"{cpuTemp.HardwareName} / {cpuTemp.Name}";
        }

        var gpuTemp = sensors.FirstOrDefault(s => s.Id == "gpu-temp-0")
            ?? sensors
                .Where(s => s.Unit == "\u00B0C" && (s.Type == "GPU" || LooksLikeGpuTemperature(s)))
                .OrderByDescending(s => SensorNameScore(s.Name))
                .FirstOrDefault();
        if (gpuTemp != null)
            GpuTempText = $"{gpuTemp.Value:F0}\u00B0C";

        var boardTemp = sensors
            .Where(s => s.Type == "MOTHERBOARD" && s.Unit == "\u00B0C" && !LooksLikeCpuTemperature(s) && !LooksLikeGpuTemperature(s))
            .OrderByDescending(s => SensorNameScore(s.Name))
            .FirstOrDefault();
        if (boardTemp != null)
            MoboTempText = $"{boardTemp.Value:F0}\u00B0C";

        var cpuLoad = sensors.FirstOrDefault(s => s.Id == "cpu-load-1")
            ?? sensors.FirstOrDefault(s => s.Id == "cpu-load-0")
            ?? sensors
                .Where(s => s.Type == "CPU" && s.Unit == "%")
                .OrderByDescending(s => SensorNameScore(s.Name))
                .FirstOrDefault();
        if (cpuLoad != null)
            CpuPercent = Math.Round(cpuLoad.Value, 1);

        var gpuLoad = sensors.FirstOrDefault(s => s.Id == "gpu-load-0")
            ?? sensors
                .Where(s => s.Type == "GPU" && s.Unit == "%")
                .OrderByDescending(s => SensorNameScore(s.Name))
                .FirstOrDefault();
        if (gpuLoad != null)
            GpuPercent = Math.Round(gpuLoad.Value, 1);

        var usedRam = sensors.FirstOrDefault(s => s.Id == "ram-data-memory-used")
            ?? sensors.FirstOrDefault(s => s.Type == "RAM" && s.Unit == "GB" && s.Name.Contains("used", StringComparison.OrdinalIgnoreCase));
        var availableRam = sensors.FirstOrDefault(s => s.Id == "ram-data-memory-available")
            ?? sensors.FirstOrDefault(s => s.Type == "RAM" && s.Unit == "GB" && s.Name.Contains("available", StringComparison.OrdinalIgnoreCase));
        if (usedRam != null && availableRam != null)
        {
            RamUsedMb = usedRam.Value * 1024;
            RamTotalMb = (usedRam.Value + availableRam.Value) * 1024;
            RamPercent = RamTotalMb > 0 ? Math.Round(RamUsedMb / RamTotalMb * 100, 1) : RamPercent;
        }
    }

    private static int SensorNameScore(string name)
    {
        var n = name.ToLowerInvariant();
        if (n.Contains("package") || n.Contains("total") || n.Contains("gpu core")) return 100;
        if (n.Contains("core") || n.Contains("cpu") || n.Contains("gpu")) return 80;
        if (n.Contains("hot spot") || n.Contains("hotspot")) return 70;
        return 0;
    }

    private static bool LooksLikeCpuTemperature(HardwareSensorReading sensor)
    {
        var text = $"{sensor.Name} {sensor.HardwareName}".ToLowerInvariant();
        return text.Contains("cpu") || text.Contains("package") || text.Contains("processor");
    }

    private static bool LooksLikeGpuTemperature(HardwareSensorReading sensor)
    {
        var text = $"{sensor.Name} {sensor.HardwareName}".ToLowerInvariant();
        return text.Contains("gpu") || text.Contains("graphics");
    }

    private void PollNetwork()
    {
        if (_activeAdapter == null) return;
        try
        {
            var s = _activeAdapter.GetIPStatistics();
            var inBytes = s.BytesReceived - _prevBytesIn;
            var outBytes = s.BytesSent - _prevBytesOut;
            _prevBytesIn = s.BytesReceived;
            _prevBytesOut = s.BytesSent;
            NetworkInMbps = Math.Round(inBytes * 8.0 / 1_000_000, 2);
            NetworkOutMbps = Math.Round(outBytes * 8.0 / 1_000_000, 2);
        }
        catch { }
    }

    private void PollPing()
    {
        if (_isPinging || DateTime.UtcNow < _nextPingAt)
            return;

        _nextPingAt = DateTime.UtcNow.AddSeconds(5);
        _isPinging = true;
        _ = Task.Run(async () =>
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync("8.8.8.8", 1500);
                PingMs = reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : 0;
            }
            catch
            {
                PingMs = 0;
            }
            finally
            {
                _isPinging = false;
            }
        });
    }

    private void PollUptime()
    {
        var ts = TimeSpan.FromMilliseconds(Environment.TickCount64);
        Uptime = ts.Days > 0
            ? $"{ts.Days}j {ts.Hours:D2}h {ts.Minutes:D2}m"
            : $"{ts.Hours:D2}h {ts.Minutes:D2}m {ts.Seconds:D2}s";
    }

    private void TryGetTemperatures()
    {
        if (TryReadAcpiThermalZone(out var acpiTemp))
        {
            CpuTempText = $"{acpiTemp:F0}\u00B0C";
            CpuTempDiagnostic = "WMI ACPI ThermalZone";
            return;
        }

        if (TryReadCimTemperature(out var cimTemp))
        {
            CpuTempText = $"{cimTemp:F0}\u00B0C";
            CpuTempDiagnostic = "WMI CIM TemperatureSensor";
            return;
        }

        CpuTempText = "N/A";
        CpuTempDiagnostic = "Aucun capteur temperature CPU detecte via WMI/ACPI";
        if (!_loggedTemperatureUnavailable)
        {
            _loggedTemperatureUnavailable = true;
            AppLog.Warning("SystemMonitorService", CpuTempDiagnostic);
        }
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
            var temps = new List<double>();
            foreach (ManagementObject obj in searcher.Get())
            {
                var t = Convert.ToDouble(obj["CurrentTemperature"]);
                temps.Add((t - 2732.0) / 10.0);
            }
            if (temps.Count > 0) CpuTempText = $"{temps[0]:F0}°C";
        }
        catch
        {
            CpuTempText = "N/A";
        }
    }

    private static void EnqueueHistory(Queue<double> q, double value)
    {
        q.Enqueue(value);
        while (q.Count > HistorySize) q.Dequeue();
    }

    private static bool TryReadAcpiThermalZone(out double temperature)
    {
        temperature = 0;
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature");
            var temps = new List<double>();
            foreach (ManagementObject obj in searcher.Get())
            {
                var raw = Convert.ToDouble(obj["CurrentTemperature"]);
                var celsius = (raw - 2732.0) / 10.0;
                if (celsius is > 0 and < 130)
                    temps.Add(celsius);
            }

            if (temps.Count == 0)
                return false;

            temperature = temps.Min();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool TryReadCimTemperature(out double temperature)
    {
        temperature = 0;
        foreach (var className in new[] { "CIM_PackageTempSensor", "CIM_TemperatureSensor", "Win32_TemperatureProbe" })
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\CIMV2", $"SELECT * FROM {className}");
                var values = new List<double>();
                foreach (ManagementObject obj in searcher.Get())
                {
                    foreach (var property in new[] { "CurrentReading", "NominalReading", "NormalMax", "MaxReadable" })
                    {
                        if (obj.Properties[property]?.Value == null)
                            continue;

                        if (!double.TryParse(obj.Properties[property].Value.ToString(), out var raw))
                            continue;

                        var celsius = NormalizeTemperature(raw);
                        if (celsius is > 0 and < 130)
                            values.Add(celsius);
                    }
                }

                if (values.Count > 0)
                {
                    temperature = values.Min();
                    return true;
                }
            }
            catch
            {
                // Some WMI classes exist but are not queryable on every Windows build.
            }
        }

        return false;
    }

    private static double NormalizeTemperature(double raw)
    {
        if (raw > 2732)
            return (raw - 2732.0) / 10.0;
        if (raw > 273.2)
            return raw - 273.15;
        return raw;
    }

    public void Dispose()
    {
        _timer.Stop();
        _cpuCounter?.Dispose();
    }
}
