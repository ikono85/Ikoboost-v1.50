using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IkoboostWpf.Models;
using IkoboostWpf.Services;
using System.Net.NetworkInformation;
using System.Windows.Threading;

namespace IkoboostWpf.ViewModels;

public partial class NetworkViewModel : ObservableObject
{
    private readonly NetworkService _service = new();
    private readonly DispatcherTimer _throughputTimer;
    private NetworkInterface? _adapter;
    private long _prevIn, _prevOut;
    private readonly Queue<double> _inSamples = new();
    private readonly Queue<double> _outSamples = new();
    private const int ThroughputHistorySize = 60;

    [ObservableProperty] private string _adapterName = "";
    [ObservableProperty] private string _ipv4 = "";
    [ObservableProperty] private string _ipv6 = "";
    [ObservableProperty] private string _gateway = "";
    [ObservableProperty] private string _dns = "";
    [ObservableProperty] private string _pingMs = "—";
    [ObservableProperty] private string _selectedDnsProfile = "Cloudflare (1.1.1.1)";
    [ObservableProperty] private string _dnsLog = "";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private double _inMbps;
    [ObservableProperty] private double _outMbps;
    [ObservableProperty] private IEnumerable<double> _inHistory = [];
    [ObservableProperty] private IEnumerable<double> _outHistory = [];
    [ObservableProperty] private string _speedTestStatus = "Prêt";
    [ObservableProperty] private bool _isSpeedTestRunning;
    [ObservableProperty] private double _speedTestProgress;
    [ObservableProperty] private double _speedTestPingMs;
    [ObservableProperty] private double _speedTestDownloadMbps;
    [ObservableProperty] private double _speedTestUploadMbps;
    [ObservableProperty] private double _speedTestJitterMs;
    [ObservableProperty] private string _speedTestError = "";
    [ObservableProperty] private ObservableCollection<SpeedTestRecord> _speedTestHistory = [];
    [ObservableProperty] private double _pingAverageMs;
    [ObservableProperty] private string _pingMinMs = "—";
    [ObservableProperty] private string _pingMaxMs = "—";
    [ObservableProperty] private ObservableCollection<PingResult> _pingResults = [];
    [ObservableProperty] private string _repairLog = "";

    public Dictionary<string, object> DnsProfiles =>
        NetworkService.DnsProfiles.ToDictionary(kv => kv.Key, kv => (object)kv.Value);

    public NetworkViewModel()
    {
        var info = _service.GetAdapterInfo();
        AdapterName = info.Adapter;
        Ipv4 = info.Ipv4;
        Ipv6 = info.Ipv6;
        Gateway = info.Gateway;
        Dns = info.Dns;
        MeasurePing();
        InitThroughput();

        _throughputTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _throughputTimer.Tick += (_, _) => PollThroughput();
        _throughputTimer.Start();
    }

    private void InitThroughput()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var s = ni.GetIPStatistics();
                if (s.BytesReceived > 0) { _adapter = ni; _prevIn = s.BytesReceived; _prevOut = s.BytesSent; break; }
            }
        }
        catch { }
    }

    private void PollThroughput()
    {
        if (_adapter == null) return;
        try
        {
            var s = _adapter.GetIPStatistics();
            InMbps = Math.Round((s.BytesReceived - _prevIn) * 8.0 / 1_000_000, 2);
            OutMbps = Math.Round((s.BytesSent - _prevOut) * 8.0 / 1_000_000, 2);
            _prevIn = s.BytesReceived;
            _prevOut = s.BytesSent;
            PushSample(_inSamples, InMbps);
            PushSample(_outSamples, OutMbps);
            InHistory = _inSamples.ToArray();
            OutHistory = _outSamples.ToArray();
        }
        catch { }
    }

    private static void PushSample(Queue<double> samples, double value)
    {
        samples.Enqueue(Math.Max(0, value));
        while (samples.Count > ThroughputHistorySize)
            samples.Dequeue();
    }

    private async void MeasurePing()
    {
        try
        {
            var ping = new Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 2000);
            PingMs = reply.Status == IPStatus.Success ? $"{reply.RoundtripTime}" : "—";
        }
        catch { }
    }

    [RelayCommand]
    private async Task SetDns()
    {
        IsBusy = true;
        DnsLog = "Changement DNS en cours...";
        DnsLog = await _service.SetDnsAsync(SelectedDnsProfile);
        IsBusy = false;
    }

    [RelayCommand]
    private async Task FlushDns()
    {
        IsBusy = true;
        DnsLog = await _service.FlushDnsAsync();
        IsBusy = false;
    }

    [RelayCommand]
    private async Task RunSpeedTest()
    {
        IsSpeedTestRunning = true;
        SpeedTestProgress = 0;
        SpeedTestError = "";
        SpeedTestStatus = "Test en cours...";

        var result = await _service.RunSpeedTestAsync(new Progress<int>(p => SpeedTestProgress = p));

        if (result != null)
        {
            SpeedTestPingMs = result.PingMs;
            SpeedTestDownloadMbps = result.DownloadMbps;
            SpeedTestUploadMbps = result.UploadMbps;
            SpeedTestJitterMs = result.JitterMs;
            SpeedTestHistory.Insert(0, result);
            if (SpeedTestHistory.Count > 20) SpeedTestHistory.RemoveAt(SpeedTestHistory.Count - 1);
            SpeedTestStatus = $"Test terminé : {result.DownloadMbps:F1} / {result.UploadMbps:F1} Mbps";
        }
        else
        {
            SpeedTestError = "Impossible de contacter le serveur de test. Vérifiez la connexion.";
            SpeedTestStatus = "Test échoué";
        }
        IsSpeedTestRunning = false;
        SpeedTestProgress = 0;
    }

    [RelayCommand]
    private async Task PingAll()
    {
        IsBusy = true;
        PingResults.Clear();
        var results = await _service.PingAllServersAsync();
        var successful = results.Where(r => r.Success).ToList();
        if (successful.Any())
        {
            var times = successful
                .Select(r => double.TryParse(r.Ms.Replace(" ms", ""), out var v) ? v : 0)
                .Where(v => v > 0).ToList();
            PingAverageMs = times.Any() ? Math.Round(times.Average(), 1) : 0;
            PingMinMs = times.Any() ? $"{(int)times.Min()}" : "—";
            PingMaxMs = times.Any() ? $"{(int)times.Max()}" : "—";
        }
        foreach (var r in results) PingResults.Add(r);
        IsBusy = false;
    }

    [RelayCommand]
    private async Task RepairNetwork()
    {
        IsBusy = true;
        RepairLog = "Réparation en cours...";
        RepairLog = await _service.RepairNetworkAsync();
        IsBusy = false;
    }
}
