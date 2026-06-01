using CommunityToolkit.Mvvm.ComponentModel;
using IkoboostWpf.Services;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;

namespace IkoboostWpf.ViewModels;

public sealed partial class DashboardViewModel : BaseViewModel
{
    private readonly SystemInfoService _sysInfo;
    private readonly NetworkService _network;
    private readonly HardwareTemperatureService _temps;
    private readonly DispatcherTimer _timer;
    private CancellationTokenSource _cts = new();
    private bool _initialized;
    private bool _isRefreshing;
    private int _refreshSeconds;

    [ObservableProperty] private string _machineName = string.Empty;
    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _osVersion = string.Empty;
    [ObservableProperty] private string _uptime = string.Empty;
    [ObservableProperty] private string _cpuName = string.Empty;
    [ObservableProperty] private string _gpuName = string.Empty;
    [ObservableProperty] private string _ramReference = "N/A";
    [ObservableProperty] private string _motherboardReference = "N/A";
    [ObservableProperty] private string _cpuDetails = string.Empty;
    [ObservableProperty] private string _cpuTemperature = "N/A";
    [ObservableProperty] private int _cpuCurrentClockMhz;
    [ObservableProperty] private int _cpuMaxClockMhz;
    [ObservableProperty] private float _gpuPercent;
    [ObservableProperty] private string _gpuUsageText = "N/A";
    [ObservableProperty] private string _gpuTemperature = "N/A";
    [ObservableProperty] private string _gpuMemory = "N/A";
    [ObservableProperty] private string _gpuDriver = "N/A";

    [ObservableProperty] private float _cpuPercent;
    [ObservableProperty] private long _ramUsedMb;
    [ObservableProperty] private long _ramAvailableMb;
    [ObservableProperty] private long _ramTotalMb;
    [ObservableProperty] private double _ramPercent;

    [ObservableProperty] private double _networkInMbps;
    [ObservableProperty] private double _networkOutMbps;
    [ObservableProperty] private long _pingMs;
    [ObservableProperty] private string _networkAdapterName = "N/A";
    [ObservableProperty] private string _networkAddress = "N/A";
    [ObservableProperty] private string _networkGateway = "N/A";
    [ObservableProperty] private string _networkDns = "N/A";
    [ObservableProperty] private string _networkSpeed = "N/A";

    [ObservableProperty] private string _temperature = "N/A";
    [ObservableProperty] private string _driveInfo = string.Empty;
    [ObservableProperty] private bool _isLoading = true;

    // ── Widget Températures ───────────────────────────────────────────────
    [ObservableProperty] private string _cpuTempText = "N/A";
    [ObservableProperty] private string _gpuTempText = "N/A";
    [ObservableProperty] private string _moboTempText = "N/A";
    [ObservableProperty] private System.Windows.Media.Brush _cpuTempBrush = System.Windows.Media.Brushes.Gray;
    [ObservableProperty] private System.Windows.Media.Brush _gpuTempBrush = System.Windows.Media.Brushes.Gray;
    [ObservableProperty] private System.Windows.Media.Brush _moboTempBrush = System.Windows.Media.Brushes.Gray;
    [ObservableProperty] private bool _hasGpuTempSensor;
    [ObservableProperty] private bool _hasMoboTempSensor;

    // ── Widget Score santé ────────────────────────────────────────────────
    [ObservableProperty] private int _healthScore = 0;
    [ObservableProperty] private string _healthLabel = "—";
    [ObservableProperty] private System.Windows.Media.Brush _healthScoreBrush = System.Windows.Media.Brushes.Gray;
    [ObservableProperty] private int _healthCpuScore;
    [ObservableProperty] private int _healthRamScore;
    [ObservableProperty] private int _healthTempScore;
    [ObservableProperty] private int _healthDiskScore;

    // ── Sparkline history (60 points × refresh interval = sliding window) ──
    [ObservableProperty] private System.Windows.Media.Brush _ramSparklineBrush = System.Windows.Media.Brushes.LightGreen;

    public ObservableCollection<double> CpuHistory { get; } = new(Enumerable.Repeat(0.0, 60));
    public ObservableCollection<double> RamHistory { get; } = new(Enumerable.Repeat(0.0, 60));
    public ObservableCollection<double> GpuHistory { get; } = new(Enumerable.Repeat(0.0, 60));
    public ObservableCollection<double> NetworkInHistory { get; } = new(Enumerable.Repeat(0.0, 60));

    public ObservableCollection<DriveUsageRow> Drives { get; } = [];

    public DashboardViewModel(
        SystemInfoService sysInfo,
        NetworkService network,
        HardwareTemperatureService temps,
        int refreshSeconds = 2)
    {
        _sysInfo = sysInfo;
        _network = network;
        _temps = temps;

        _refreshSeconds = NormalizeRefreshSeconds(refreshSeconds);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_refreshSeconds) };
        _timer.Tick += async (_, _) =>
        {
            if (!IsDisposed)
                await RefreshAsync();
        };
    }

    public async Task InitializeAsync()
    {
        if (IsDisposed) return;

        if (_initialized)
        {
            if (!_timer.IsEnabled)
                _timer.Start();
            return;
        }
        _initialized = true;

        MachineName = _sysInfo.MachineName;
        UserName = _sysInfo.UserName;
        OsVersion = _sysInfo.OsVersion;

        CancellationToken initCt;
        try { initCt = _cts.Token; }
        catch (ObjectDisposedException) { return; }

        try
        {
            CpuName = await _sysInfo.GetCpuNameAsync(initCt);
            GpuName = await _sysInfo.GetGpuNameAsync(initCt);
            var cpu = await _sysInfo.GetCpuDetailsAsync(initCt);
            CpuDetails = FormatCpuDetails(cpu);
            var gpu = await _sysInfo.GetGpuDetailsAsync(initCt);
            GpuName = gpu.Name;
            GpuMemory = gpu.AdapterRamMb > 0 ? $"{gpu.AdapterRamMb:N0} Mo" : "N/A";
            GpuDriver = gpu.DriverVersion;
            var ram = await _sysInfo.GetRamDetailsAsync(initCt);
            RamReference = ram.Summary;
            var board = await _sysInfo.GetBoardDetailsAsync(initCt);
            MotherboardReference = $"{board.Manufacturer} {board.Product}".Trim();

            await RefreshAsync();
            IsLoading = false;

            if (!IsDisposed)
                _timer.Start();
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }

    public void SetRefreshInterval(int refreshSeconds)
    {
        if (IsDisposed) return;

        _refreshSeconds = NormalizeRefreshSeconds(refreshSeconds);
        _timer.Interval = TimeSpan.FromSeconds(_refreshSeconds);
    }

    public void Pause()
    {
        if (!IsDisposed)
            _timer.Stop();
    }

    private async Task RefreshAsync()
    {
        if (IsDisposed) return;
        if (_isRefreshing) return;

        CancellationToken ct;
        try { ct = _cts.Token; }
        catch (ObjectDisposedException) { return; }

        _isRefreshing = true;
        try
        {
            Uptime = FormatUptime(_sysInfo.Uptime);

            var cpuDetailsTask = _sysInfo.GetCpuDetailsAsync(ct);
            var gpuUsageTask = _sysInfo.GetGpuUsagePercentAsync(ct);
            var pingTask = _network.PingAsync(timeoutMs: GetDashboardPingTimeoutMs(), ct: ct);
            var temperaturesTask = _temps.GetTemperaturesAsync(ct);
            var drivesTask = _sysInfo.GetDrivesAsync(ct);

            // CPU — premier appel retourne 0, c'est normal pour les perf counters
            CpuPercent = _sysInfo.GetCpuUsagePercent();
            PushHistory(CpuHistory, CpuPercent);
            var cpu = await cpuDetailsTask;
            CpuCurrentClockMhz = cpu.CurrentClockMhz;
            CpuMaxClockMhz = cpu.MaxClockMhz;
            CpuDetails = FormatCpuDetails(cpu);

            // GPU
            var gpuUsage = await gpuUsageTask;
            GpuPercent = gpuUsage >= 0 ? gpuUsage : 0;
            GpuUsageText = gpuUsage >= 0 ? $"{gpuUsage:F1} %" : "N/A";
            PushHistory(GpuHistory, GpuPercent);

            // RAM
            var memory = _sysInfo.GetMemoryDetails();
            RamAvailableMb = memory.AvailableMb;
            RamTotalMb = memory.TotalMb;
            RamUsedMb = memory.TotalMb - memory.AvailableMb;
            RamPercent = memory.TotalMb > 0 ? (double)RamUsedMb / memory.TotalMb * 100.0 : 0;
            PushHistory(RamHistory, RamPercent);
            RamSparklineBrush = GetThemeBrush(RamPercent >= 80 ? "WarningBrush" : "SuccessBrush");

            // Réseau throughput
            RefreshNetworkInfo();
            var (inMbps, outMbps) = _network.GetThroughput();
            NetworkInMbps = Math.Round(inMbps, 2);
            NetworkOutMbps = Math.Round(outMbps, 2);
            PushHistory(NetworkInHistory, NetworkInMbps);

            // Ping async
            PingMs = await pingTask;

            // Température async
            var readings = await temperaturesTask;
            CpuTemperature = FormatCpuTemperature(readings);
            GpuTemperature = FormatGpuTemperature(readings);
            Temperature = readings.Count > 0
                ? $"{readings[0].ValueCelsius:F1} °C ({readings[0].Name})"
                : "N/A";

            // Widget Températures
            var cpuTempRaw = GetCpuTempFloat(readings);
            var gpuTempRaw = GetGpuTempFloat(readings);
            var moboTempRaw = GetMoboTempFloat(readings);
            CpuTempText = cpuTempRaw > 0 ? $"{cpuTempRaw:F0}°C" : "N/A";
            GpuTempText = gpuTempRaw > 0 ? $"{gpuTempRaw:F0}°C" : "N/A";
            MoboTempText = moboTempRaw > 0 ? $"{moboTempRaw:F0}°C" : "N/A";
            CpuTempBrush = GetTempBrush(cpuTempRaw);
            GpuTempBrush = GetTempBrush(gpuTempRaw);
            MoboTempBrush = GetTempBrush(moboTempRaw);
            HasGpuTempSensor = gpuTempRaw > 0;
            HasMoboTempSensor = moboTempRaw > 0;

            // Disques
            var drives = await drivesTask;
            Drives.Clear();
            foreach (var drive in drives)
            {
                var usedBytes = drive.TotalSize - drive.AvailableFreeSpace;
                var percent = drive.TotalSize > 0 ? (double)usedBytes / drive.TotalSize * 100.0 : 0;
                Drives.Add(new DriveUsageRow(
                    drive.Name,
                    drive.VolumeLabel,
                    FormatBytes(usedBytes),
                    FormatBytes(drive.AvailableFreeSpace),
                    FormatBytes(drive.TotalSize),
                    Math.Round(percent, 1),
                    GetDriveUsageBrush(percent)));
            }

            DriveInfo = Drives.Count == 0 ? "N/A" : string.Empty;

            // Widget Score santé (après Drives pour avoir les données disque)
            RefreshHealthScore(cpuTempRaw);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch
        {
            Temperature = "N/A";
            DriveInfo = "N/A";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private int GetDashboardPingTimeoutMs()
        => Math.Clamp(_refreshSeconds * 500, 500, 1000);

    private static int NormalizeRefreshSeconds(int refreshSeconds)
        => Math.Clamp(refreshSeconds, 1, 60);

    private static string FormatUptime(TimeSpan t)
        => $"{(int)t.TotalDays}j {t.Hours:D2}h {t.Minutes:D2}m";

    private static string FormatBytes(long bytes)
        => $"{bytes / 1024d / 1024d / 1024d:N1} Go";

    private static System.Windows.Media.Brush GetDriveUsageBrush(double percent)
    {
        var key = percent switch
        {
            >= 90 => "ErrorBrush",
            >= 75 => "WarningBrush",
            _ => "SuccessBrush"
        };

        return System.Windows.Application.Current.TryFindResource(key) is System.Windows.Media.Brush brush
            ? brush
            : System.Windows.Media.Brushes.Gray;
    }

    private static string FormatCpuDetails(SystemInfoService.CpuDetails cpu)
    {
        var coresLabel = LocalizationService.Get("Dashboard.Cores");
        var threadsLabel = LocalizationService.Get("Dashboard.Threads");
        var freqLabel = LocalizationService.Get("Dashboard.FreqMax");
        var cores = cpu.Cores > 0 ? $"{cpu.Cores} {coresLabel}" : $"? {coresLabel}";
        var logical = cpu.LogicalProcessors > 0 ? $"{cpu.LogicalProcessors} {threadsLabel}" : $"? {threadsLabel}";
        var clock = cpu.MaxClockMhz > 0 ? $"{cpu.MaxClockMhz} {freqLabel}" : "N/A";
        return $"{cores} / {logical} / {clock}";
    }

    private static string FormatCpuTemperature(IReadOnlyList<HardwareTemperatureService.SensorReading> readings)
    {
        var cpuReadings = readings
            .Where(r => r.Type.Equals("CPU", StringComparison.OrdinalIgnoreCase)
                     || IsCpuTemperatureName(r.Name))
            .Where(IsUsableTemperature)
            .ToList();

        var reading = cpuReadings
            .OrderByDescending(r => IsPreferredCpuTemperatureName(r.Name))
            .ThenByDescending(r => r.ValueCelsius)
            .FirstOrDefault();

        return reading == null
            ? "N/A"
            : $"{reading.ValueCelsius:F1} C ({reading.Name})";
    }

    private static bool IsCpuTemperatureName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return name.Contains("cpu", StringComparison.OrdinalIgnoreCase)
            || name.Contains("core", StringComparison.OrdinalIgnoreCase)
            || name.Contains("package", StringComparison.OrdinalIgnoreCase)
            || name.Contains("tctl", StringComparison.OrdinalIgnoreCase)
            || name.Contains("tdie", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPreferredCpuTemperatureName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return name.Contains("package", StringComparison.OrdinalIgnoreCase)
            || name.Contains("temperature", StringComparison.OrdinalIgnoreCase)
            || name.Contains("tctl", StringComparison.OrdinalIgnoreCase)
            || name.Contains("tdie", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatGpuTemperature(IReadOnlyList<HardwareTemperatureService.SensorReading> readings)
    {
        var reading = readings
            .Where(r => r.Type.Equals("GPU", StringComparison.OrdinalIgnoreCase)
                     || r.Id.Equals("gpu-temp-0", StringComparison.OrdinalIgnoreCase)
                     || IsGpuTemperatureName(r.Name))
            .Where(IsUsableTemperature)
            .OrderByDescending(r => r.ValueCelsius)
            .FirstOrDefault();

        return reading == null
            ? LocalizationService.Get("Dashboard.GpuTempNoSensor")
            : $"{reading.ValueCelsius:F1} C ({reading.Name})";
    }

    private static bool IsUsableTemperature(HardwareTemperatureService.SensorReading reading)
        => reading.ValueCelsius is > 0 and < 150;

    private static bool IsGpuTemperatureName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return name.Contains("gpu", StringComparison.OrdinalIgnoreCase)
            || name.Contains("graphics", StringComparison.OrdinalIgnoreCase)
            || name.Contains("video", StringComparison.OrdinalIgnoreCase)
            || name.Contains("hot spot", StringComparison.OrdinalIgnoreCase)
            || name.Contains("hotspot", StringComparison.OrdinalIgnoreCase)
            || name.Contains("junction", StringComparison.OrdinalIgnoreCase)
            || name.Contains("edge", StringComparison.OrdinalIgnoreCase)
            || name.Contains("memory junction", StringComparison.OrdinalIgnoreCase);
    }

    // ── Sparkline helpers ─────────────────────────────────────────────────

    /// <summary>Maintains a fixed-length 60-point sliding window.</summary>
    private static void PushHistory(ObservableCollection<double> history, double value)
    {
        if (history.Count >= 60)
            history.RemoveAt(0);
        history.Add(value);
    }

    /// <summary>
    /// Resolves a theme brush by resource key at call time, so the sparkline color
    /// stays correct after a theme change (updated on next refresh tick).
    /// </summary>
    private static System.Windows.Media.Brush GetThemeBrush(string key)
    {
        try
        {
            return System.Windows.Application.Current?.TryFindResource(key)
                       as System.Windows.Media.Brush
                   ?? System.Windows.Media.Brushes.Gray;
        }
        catch
        {
            return System.Windows.Media.Brushes.Gray;
        }
    }

    // ── Temperature widget helpers ────────────────────────────────────────

    private static float GetCpuTempFloat(IReadOnlyList<HardwareTemperatureService.SensorReading> readings)
        => readings
            .Where(r => r.Type.Equals("CPU", StringComparison.OrdinalIgnoreCase) || IsCpuTemperatureName(r.Name))
            .Where(IsUsableTemperature)
            .OrderByDescending(r => IsPreferredCpuTemperatureName(r.Name))
            .ThenByDescending(r => r.ValueCelsius)
            .FirstOrDefault()?.ValueCelsius ?? -1;

    private static float GetGpuTempFloat(IReadOnlyList<HardwareTemperatureService.SensorReading> readings)
        => readings
            .Where(r => r.Type.Equals("GPU", StringComparison.OrdinalIgnoreCase)
                     || r.Id.Equals("gpu-temp-0", StringComparison.OrdinalIgnoreCase)
                     || IsGpuTemperatureName(r.Name))
            .Where(IsUsableTemperature)
            .OrderByDescending(r => r.ValueCelsius)
            .FirstOrDefault()?.ValueCelsius ?? -1;

    private static float GetMoboTempFloat(IReadOnlyList<HardwareTemperatureService.SensorReading> readings)
        => readings
            .Where(r => IsMoboTemperatureName(r.Name))
            .Where(IsUsableTemperature)
            .OrderBy(r => r.ValueCelsius)
            .FirstOrDefault()?.ValueCelsius ?? -1;

    private static bool IsMoboTemperatureName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        return name.Contains("system", StringComparison.OrdinalIgnoreCase)
            || name.Contains("motherboard", StringComparison.OrdinalIgnoreCase)
            || name.Contains("ambient", StringComparison.OrdinalIgnoreCase);
    }

    private static System.Windows.Media.Brush GetTempBrush(float celsius) =>
        GetThemeBrush(celsius switch
        {
            <= 0 => null!,
            >= 80 => "ErrorBrush",
            >= 60 => "WarningBrush",
            _ => "SuccessBrush"
        }) is { } b && celsius > 0
            ? b
            : System.Windows.Media.Brushes.Gray;

    // ── Health score ──────────────────────────────────────────────────────

    private void RefreshHealthScore(float cpuTempRaw)
    {
        var cpuScore = Math.Max(0, 100 - (int)CpuPercent);
        var ramScore = Math.Max(0, 100 - (int)RamPercent);
        var tempScore = cpuTempRaw <= 0
            ? 70
            : (int)(cpuTempRaw switch
            {
                < 60 => 100,
                < 70 => 80,
                < 80 => 55,
                < 90 => 25,
                _ => 0
            });
        var diskScore = Drives.Count == 0
            ? 80
            : Math.Max(0, 100 - (int)Drives.Max(d => d.UsagePercent));

        HealthCpuScore = cpuScore;
        HealthRamScore = ramScore;
        HealthTempScore = tempScore;
        HealthDiskScore = diskScore;

        HealthScore = Math.Clamp((cpuScore * 3 + ramScore * 3 + tempScore * 2 + diskScore * 2) / 10, 0, 100);
        HealthLabel = HealthScore switch
        {
            >= 85 => "EXCELLENT",
            >= 65 => "BIEN",
            >= 40 => "ATTENTION",
            _ => "CRITIQUE"
        };
        HealthScoreBrush = GetThemeBrush(HealthScore switch
        {
            >= 65 => "SuccessBrush",
            >= 40 => "WarningBrush",
            _ => "ErrorBrush"
        });
    }

    private void RefreshNetworkInfo()
    {
        var adapter = _network.GetActiveAdapterInfo();
        if (adapter == null)
        {
            NetworkAdapterName = "N/A";
            NetworkAddress = "N/A";
            NetworkGateway = "N/A";
            NetworkDns = "N/A";
            NetworkSpeed = "N/A";
            return;
        }

        NetworkAdapterName = adapter.Name;
        NetworkAddress = adapter.IPv4;
        NetworkGateway = adapter.Gateway;
        NetworkDns = adapter.Dns;
        NetworkSpeed = adapter.Speed;
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        base.Dispose(); // marque IsDisposed = true avant Cancel pour bloquer les nouveaux accès
        _timer.Stop();
        _cts.Cancel();
    }
}

public sealed record DriveUsageRow(
    string Name,
    string Label,
    string Used,
    string Available,
    string Total,
    double UsagePercent,
    System.Windows.Media.Brush UsageBrush);
