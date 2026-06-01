using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IkoboostWpf.Services;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Threading;

namespace IkoboostWpf.ViewModels;

public sealed partial class NetworkViewModel : BaseViewModel
{
    private readonly NetworkService _network;
    private readonly OptimizationService _optimization;
    private readonly DispatcherTimer _timer;
    private CancellationTokenSource _cts = new();
    private bool _initialized;
    private bool _isRefreshingThroughput;

    [ObservableProperty] private string _adapterName = "N/A";
    [ObservableProperty] private string _ipv4 = "N/A";
    [ObservableProperty] private string _ipv6 = "N/A";
    [ObservableProperty] private string _gateway = "N/A";
    [ObservableProperty] private string _dns = "N/A";
    [ObservableProperty] private long _pingMs = -1;
    [ObservableProperty] private double _inMbps;
    [ObservableProperty] private double _outMbps;
    [ObservableProperty] private double _pingAverageMs;
    [ObservableProperty] private long _pingMinMs = -1;
    [ObservableProperty] private long _pingMaxMs = -1;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isSpeedTestRunning;
    [ObservableProperty] private double _speedTestPingMs;
    [ObservableProperty] private double _speedTestDownloadMbps;
    [ObservableProperty] private double _speedTestUploadMbps;
    [ObservableProperty] private double _speedTestJitterMs;
    [ObservableProperty] private double _speedTestProgress;
    [ObservableProperty] private string _speedTestStatus = "Pret";
    [ObservableProperty] private string _speedTestError = string.Empty;
    [ObservableProperty] private string _repairLog = string.Empty;
    [ObservableProperty] private string _dnsLog = string.Empty;
    [ObservableProperty] private string _selectedDnsProfile = "Cloudflare";

    public ObservableCollection<PingResultRow> PingResults { get; } = [];
    public ObservableCollection<SpeedTestHistoryRow> SpeedTestHistory { get; } = [];
    public IReadOnlyDictionary<string, string> DnsProfiles => OptimizationService.DnsProfiles;

    public NetworkViewModel(NetworkService network, OptimizationService optimization)
    {
        _network = network;
        _optimization = optimization;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) =>
        {
            if (!IsDisposed)
                await RefreshThroughputAsync();
        };
    }

    public async Task InitializeAsync()
    {
        if (IsDisposed) return;

        if (!_initialized)
        {
            _initialized = true;
            RefreshAdapterInfo();
            await PingAllAsync();
        }
        else
        {
            RefreshAdapterInfo();
        }

        if (!IsDisposed)
            _timer.Start();
    }

    public void Pause()
    {
        if (!IsDisposed)
            _timer.Stop();
    }

    private void RefreshAdapterInfo()
    {
        var info = _network.GetActiveAdapterInfo();
        if (info == null) return;
        AdapterName = info.Name;
        Ipv4 = info.IPv4;
        Ipv6 = info.IPv6;
        Gateway = info.Gateway;
        Dns = info.Dns;
    }

    private async Task RefreshThroughputAsync()
    {
        if (IsDisposed) return;
        if (_isRefreshingThroughput) return;

        CancellationToken ct;
        try { ct = _cts.Token; }
        catch (ObjectDisposedException) { return; }

        _isRefreshingThroughput = true;
        try
        {
            RefreshAdapterInfo();
            var (inMbps, outMbps) = _network.GetThroughput();
            InMbps = Math.Round(inMbps, 2);
            OutMbps = Math.Round(outMbps, 2);
            PingMs = await _network.PingAsync(ct: ct);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        finally { _isRefreshingThroughput = false; }
    }

    [RelayCommand]
    private async Task PingAllAsync()
    {
        if (IsDisposed) return;

        CancellationToken ct;
        try { ct = _cts.Token; }
        catch (ObjectDisposedException) { return; }

        IsBusy = true;
        PingResults.Clear();
        try
        {
            var results = await _network.PingAllTargetsAsync(ct);
            foreach (var r in results)
                PingResults.Add(new PingResultRow(r.Host, r.Success, r.RoundtripMs, r.Error));

            UpdatePingStats(results);
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        finally { IsBusy = false; }
    }

    private void UpdatePingStats(IReadOnlyList<NetworkService.PingResult> results)
    {
        var successful = results
            .Where(r => r.Success && r.RoundtripMs >= 0)
            .Select(r => r.RoundtripMs)
            .ToList();

        if (successful.Count == 0)
        {
            PingAverageMs = 0;
            PingMinMs = -1;
            PingMaxMs = -1;
            return;
        }

        PingAverageMs = Math.Round(successful.Average(), 1);
        PingMinMs = successful.Min();
        PingMaxMs = successful.Max();
    }

    [RelayCommand]
    private async Task RunSpeedTestAsync()
    {
        if (IsDisposed || IsSpeedTestRunning)
            return;

        CancellationToken ct;
        try { ct = _cts.Token; }
        catch (ObjectDisposedException) { return; }

        IsSpeedTestRunning = true;
        SpeedTestError = string.Empty;
        SpeedTestProgress = 0;
        SpeedTestStatus = "Preparation du test...";

        var progress = new Progress<NetworkService.SpeedTestProgress>(p =>
        {
            SpeedTestStatus = p.Stage;
            SpeedTestProgress = Math.Clamp(p.Percent, 0, 100);
        });

        try
        {
            var result = await _network.RunSpeedTestAsync(progress, ct);
            SpeedTestPingMs = result.PingMs;
            SpeedTestDownloadMbps = result.DownloadMbps;
            SpeedTestUploadMbps = result.UploadMbps;
            SpeedTestJitterMs = result.JitterMs;
            var hasPartialResult = result.DownloadMbps <= 0 || result.UploadMbps <= 0;
            SpeedTestStatus = hasPartialResult ? "Test termine (partiel)" : "Test termine";
            SpeedTestError = hasPartialResult
                ? "Connexion detectee, mais une mesure de debit n'a pas repondu assez vite."
                : string.Empty;
            SpeedTestProgress = 100;

            SpeedTestHistory.Insert(0, new SpeedTestHistoryRow(
                DateTime.Now.ToString("HH:mm:ss"),
                result.PingMs,
                result.DownloadMbps,
                result.UploadMbps,
                result.JitterMs));

            while (SpeedTestHistory.Count > 5)
                SpeedTestHistory.RemoveAt(SpeedTestHistory.Count - 1);
        }
        catch (HttpRequestException ex)
        {
            SpeedTestStatus = "Erreur reseau";
            SpeedTestError = $"Impossible de contacter Cloudflare Speed Test : {ex.Message}";
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            SpeedTestStatus = "Delai depasse";
            SpeedTestError = "Le test a pris trop de temps. Verifie ta connexion puis relance le test.";
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            SpeedTestStatus = "Erreur";
            SpeedTestError = $"Erreur speed test : {ex.Message}";
        }
        finally
        {
            IsSpeedTestRunning = false;
        }
    }

    [RelayCommand]
    private async Task RepairNetworkAsync()
    {
        if (IsDisposed) return;

        CancellationToken ct;
        try { ct = _cts.Token; }
        catch (ObjectDisposedException) { return; }

        IsBusy = true;
        RepairLog = "Réparation en cours…";
        try
        {
            RepairLog = await _network.RepairNetworkAsync(ct);
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex) { RepairLog = $"Erreur : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SetDnsAsync()
    {
        if (IsDisposed) return;

        if (!DnsProfiles.TryGetValue(SelectedDnsProfile, out var dns))
            return;

        var info = _network.GetActiveAdapterInfo();
        if (info == null)
        {
            DnsLog = "Aucun adaptateur réseau actif détecté.";
            return;
        }

        CancellationToken ctDns;
        try { ctDns = _cts.Token; }
        catch (ObjectDisposedException) { return; }

        IsBusy = true;
        DnsLog = $"Application du DNS {SelectedDnsProfile} ({dns}) sur {info.Name}...";

        try
        {
            var result = await _optimization.SetDnsAsync(info.Name, dns, ctDns);
            RefreshAdapterInfo();
            DnsLog = result.StartsWith("Erreur", StringComparison.OrdinalIgnoreCase)
                ? result
                : $"[OK] DNS appliqué : {SelectedDnsProfile} ({dns})";
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            DnsLog = $"Erreur DNS : {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task FlushDnsAsync()
    {
        if (IsDisposed) return;

        CancellationToken ctFlush;
        try { ctFlush = _cts.Token; }
        catch (ObjectDisposedException) { return; }

        IsBusy = true;
        DnsLog = "Flush DNS en cours...";
        try
        {
            var result = await _optimization.FlushDnsAsync(ctFlush);
            DnsLog = result.StartsWith("Erreur", StringComparison.OrdinalIgnoreCase)
                ? result
                : $"[OK] Cache DNS vide. {result.Trim()}";
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            DnsLog = $"Erreur flush DNS : {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        base.Dispose();
        _timer.Stop();
        _cts.Cancel();
    }
}

public sealed record PingResultRow(string Host, bool Success, long Ms, string Error);
public sealed record SpeedTestHistoryRow(string Time, double PingMs, double DownloadMbps, double UploadMbps, double JitterMs);
