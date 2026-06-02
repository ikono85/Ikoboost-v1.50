using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using IkoboostWpf.Models;
using IkoboostWpf.Services;
using System.Windows.Media;

namespace IkoboostWpf.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly SystemMonitorService _monitor;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _machineName = Environment.MachineName;
    [ObservableProperty] private string _userName = Environment.UserName;
    [ObservableProperty] private string _osVersion = Environment.OSVersion.VersionString;
    [ObservableProperty] private string _uptime = "";
    [ObservableProperty] private string _cpuName = "";
    [ObservableProperty] private string _gpuName = "";
    [ObservableProperty] private string _ramReference = "";
    [ObservableProperty] private string _motherboardReference = "";

    // CPU
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private IEnumerable<double> _cpuHistory = [];
    [ObservableProperty] private string _cpuDetails = "";
    [ObservableProperty] private string _cpuCurrentClockMhz = "";
    [ObservableProperty] private string _cpuMaxClockMhz = "";
    [ObservableProperty] private string _cpuTemperature = "";
    [ObservableProperty] private string _cpuTempText = "N/A";
    [ObservableProperty] private string _cpuTempDiagnostic = "Capteur CPU non detecte";
    [ObservableProperty] private Brush _cpuTempBrush = Brushes.Gray;

    // RAM
    [ObservableProperty] private double _ramPercent;
    [ObservableProperty] private IEnumerable<double> _ramHistory = [];
    [ObservableProperty] private double _ramUsedMb;
    [ObservableProperty] private double _ramTotalMb;
    [ObservableProperty] private double _ramAvailableMb;

    // GPU
    [ObservableProperty] private double _gpuPercent;
    [ObservableProperty] private IEnumerable<double> _gpuHistory = [];
    [ObservableProperty] private string _gpuUsageText = "N/A";
    [ObservableProperty] private string _gpuTemperature = "N/A";
    [ObservableProperty] private string _gpuMemory = "";
    [ObservableProperty] private string _gpuDriver = "";
    [ObservableProperty] private bool _hasGpuTempSensor;
    [ObservableProperty] private string _gpuTempText = "N/A";
    [ObservableProperty] private Brush _gpuTempBrush = Brushes.Gray;
    [ObservableProperty] private bool _hasMoboTempSensor;
    [ObservableProperty] private string _moboTempText = "N/A";
    [ObservableProperty] private Brush _moboTempBrush = Brushes.Gray;

    // Network
    [ObservableProperty] private double _networkInMbps;
    [ObservableProperty] private double _networkOutMbps;
    [ObservableProperty] private IEnumerable<double> _networkInHistory = [];
    [ObservableProperty] private string _pingMs = "—";
    [ObservableProperty] private string _networkAdapterName = "";
    [ObservableProperty] private string _networkAddress = "";
    [ObservableProperty] private string _networkSpeed = "";
    [ObservableProperty] private string _networkDns = "";

    // Health
    [ObservableProperty] private double _healthScore = 85;
    [ObservableProperty] private Brush _healthScoreBrush = Brushes.LimeGreen;
    [ObservableProperty] private string _healthLabel = "Excellent";
    [ObservableProperty] private int _healthCpuScore = 85;
    [ObservableProperty] private int _healthRamScore = 80;
    [ObservableProperty] private int _healthTempScore = 90;
    [ObservableProperty] private int _healthDiskScore = 75;

    // Storage
    [ObservableProperty] private string _driveInfo = "";
    [ObservableProperty] private ObservableCollection<DriveItem> _drives = [];

    private static readonly Brush _accentBrush = new SolidColorBrush(Color.FromRgb(0x2F, 0xE6, 0xF2));
    private static readonly Brush _okBrush = new SolidColorBrush(Color.FromRgb(0x38, 0xD9, 0x96));
    private static readonly Brush _warnBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xB5, 0x3D));
    private static readonly Brush _critBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0x5E, 0x63));
    private static readonly Brush _grayBrush = new SolidColorBrush(Color.FromRgb(0x8F, 0xA0, 0xB2));

    public DashboardViewModel(SystemMonitorService monitor)
    {
        _monitor = monitor;
        CpuName = monitor.CpuName;
        GpuName = monitor.GpuName;
        GpuMemory = monitor.GpuMemory;
        GpuDriver = monitor.GpuDriver;
        RamReference = monitor.RamReference;
        MotherboardReference = monitor.MotherboardReference;
        CpuDetails = monitor.CpuDetails;
        CpuMaxClockMhz = monitor.CpuMaxClockMhz;
        CpuCurrentClockMhz = monitor.CpuCurrentClockMhz;
        CpuTempText = monitor.CpuTempText;
        CpuTempDiagnostic = monitor.CpuTempDiagnostic;
        CpuTempBrush = TempBrush(monitor.CpuTempText);
        CpuTemperature = monitor.CpuTempText;
        monitor.DataUpdated += OnDataUpdated;
        LoadDrives();
        IsLoading = false;
    }

    private void OnDataUpdated()
    {
        CpuPercent = _monitor.CpuPercent;
        CpuHistory = _monitor.CpuHistory.ToArray();
        CpuCurrentClockMhz = _monitor.CpuCurrentClockMhz;
        CpuTempText = _monitor.CpuTempText;
        CpuTempDiagnostic = _monitor.CpuTempDiagnostic;
        CpuTempBrush = TempBrush(_monitor.CpuTempText);
        CpuTemperature = _monitor.CpuTempText;

        RamPercent = _monitor.RamPercent;
        RamHistory = _monitor.RamHistory.ToArray();
        RamUsedMb = _monitor.RamUsedMb;
        RamTotalMb = _monitor.RamTotalMb;
        RamAvailableMb = _monitor.RamTotalMb - _monitor.RamUsedMb;

        GpuPercent = _monitor.GpuPercent;
        GpuHistory = _monitor.GpuHistory.ToArray();
        GpuUsageText = $"{_monitor.GpuPercent:F0} %";
        GpuTempText = _monitor.GpuTempText;
        GpuTempBrush = TempBrush(_monitor.GpuTempText);
        HasGpuTempSensor = _monitor.GpuTempText != "N/A";
        MoboTempText = _monitor.MoboTempText;
        MoboTempBrush = TempBrush(_monitor.MoboTempText);

        NetworkInMbps = _monitor.NetworkInMbps;
        NetworkOutMbps = _monitor.NetworkOutMbps;
        NetworkInHistory = _monitor.NetworkInHistory.ToArray();
        NetworkAdapterName = _monitor.NetworkAdapterName;
        NetworkAddress = _monitor.NetworkAddress;
        NetworkSpeed = _monitor.NetworkSpeed;
        NetworkDns = _monitor.NetworkDns;

        Uptime = _monitor.Uptime;
        ComputeHealthScore();
    }

    private void ComputeHealthScore()
    {
        HealthCpuScore = (int)(100 - CpuPercent);
        HealthRamScore = (int)(100 - RamPercent);
        HealthTempScore = ComputeTempScore();
        HealthDiskScore = ComputeDiskScore();
        HealthScore = (HealthCpuScore + HealthRamScore + HealthTempScore + HealthDiskScore) / 4.0;

        HealthScoreBrush = HealthScore switch
        {
            >= 80 => _okBrush,
            >= 60 => _warnBrush,
            _ => _critBrush
        };
        HealthLabel = HealthScore switch
        {
            >= 90 => "Excellent",
            >= 75 => "Bon",
            >= 60 => "Correct",
            >= 45 => "Dégradé",
            _ => "Critique"
        };
    }

    private int ComputeTempScore()
    {
        if (!TryParseTemp(CpuTempText, out var t)) return 85;
        return t switch { > 90 => 10, > 80 => 30, > 70 => 60, > 60 => 80, _ => 95 };
    }

    private int ComputeDiskScore()
    {
        if (Drives.Count == 0) return 80;
        var worst = Drives.Max(d => d.UsagePercent);
        return worst switch { > 95 => 10, > 85 => 40, > 70 => 70, _ => 90 };
    }

    private static Brush TempBrush(string text)
    {
        if (!TryParseTemp(text, out var t)) return _grayBrush;
        return t switch { > 85 => _critBrush, > 70 => _warnBrush, _ => _okBrush };
    }

    private static bool TryParseTemp(string text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text) || text == "N/A") return false;
        var m = System.Text.RegularExpressions.Regex.Match(text, @"\d+([.,]\d+)?");
        return m.Success && double.TryParse(m.Value.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out value);
    }

    private void LoadDrives()
    {
        var items = new ObservableCollection<DriveItem>();
        try
        {
            foreach (var di in System.IO.DriveInfo.GetDrives().Where(d => d.IsReady))
            {
                var pct = di.TotalSize > 0
                    ? (di.TotalSize - di.AvailableFreeSpace) * 100.0 / di.TotalSize : 0;
                items.Add(new DriveItem
                {
                    Name = di.Name,
                    Label = di.VolumeLabel,
                    UsagePercent = Math.Round(pct, 1),
                    Used = FormatSize(di.TotalSize - di.AvailableFreeSpace),
                    Available = FormatSize(di.AvailableFreeSpace),
                    Total = FormatSize(di.TotalSize),
                    UsageBrush = pct > 90 ? _critBrush : pct > 75 ? _warnBrush : _accentBrush,
                });
            }
        }
        catch { }
        Drives = items;
        DriveInfo = $"{items.Count} lecteur(s)";
    }

    private static string FormatSize(long bytes) =>
        bytes switch
        {
            >= 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024 / 1024:F1} To",
            >= 1024 * 1024 => $"{bytes / 1024.0 / 1024:F0} Mo",
            _ => $"{bytes / 1024.0:F0} Ko"
        };
}
