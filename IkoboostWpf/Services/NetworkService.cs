using System.Net;
using System.Diagnostics;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace IkoboostWpf.Services;

public sealed class NetworkService : IDisposable
{
    public record AdapterInfo(string Name, string IPv4, string IPv6, string Gateway, string Dns, string Speed);
    public record PingResult(string Host, bool Success, long RoundtripMs, string Error);
    public record SpeedTestProgress(string Stage, double Percent);
    public record SpeedTestResult(double PingMs, double DownloadMbps, double UploadMbps, double JitterMs);

    private const string PingUrl = "https://speed.cloudflare.com/__down?bytes=0";
    private const string UploadUrl = "https://speed.cloudflare.com/__up";
    private const int UploadBytes = 2 * 1024 * 1024;
    private const int DownloadBytes = 10_000_000;
    private static readonly string DownloadUrl = $"https://speed.cloudflare.com/__down?bytes={DownloadBytes}";
    private static readonly Uri[] UploadUrls =
    [
        new(UploadUrl),
        new("https://postman-echo.com/post"),
        new("https://httpbin.org/post")
    ];

    private static readonly string[] PingTargets =
    [
        "1.1.1.1",       // Cloudflare
        "8.8.8.8",       // Google
        "9.9.9.9",       // Quad9
        "13.107.4.52",   // Microsoft
        "140.82.114.4"   // GitHub
    ];

    private static readonly string[] PingTargetNames =
    [
        "Cloudflare", "Google", "Quad9", "Microsoft", "GitHub"
    ];

    private NetworkInterface? _activeAdapter;
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTime _lastStatsTime;
    private bool _hasLastStats;
    private readonly HttpClient _httpClient;
    private bool _disposed;

    public NetworkService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
    }

    public AdapterInfo? GetActiveAdapterInfo()
    {
        try
        {
            var adapter = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                         && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .OrderByDescending(n => n.GetIPStatistics().BytesReceived)
                .FirstOrDefault();

            if (_activeAdapter?.Id != adapter?.Id)
                _hasLastStats = false;

            _activeAdapter = adapter;

            if (_activeAdapter == null) return null;

            var props = _activeAdapter.GetIPProperties();
            var ipv4 = props.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)
                ?.Address.ToString() ?? "N/A";
            var ipv6 = props.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetworkV6)
                ?.Address.ToString() ?? "N/A";
            var gateway = props.GatewayAddresses
                .FirstOrDefault()?.Address.ToString() ?? "N/A";
            var dns = string.Join(", ", props.DnsAddresses.Select(d => d.ToString()).Take(2));
            if (string.IsNullOrEmpty(dns)) dns = "N/A";
            var speed = _activeAdapter.Speed > 0
                ? $"{_activeAdapter.Speed / 1_000_000d:N0} Mbps"
                : "N/A";

            return new AdapterInfo(_activeAdapter.Name, ipv4, ipv6, gateway, dns, speed);
        }
        catch { return null; }
    }

    public (double InMbps, double OutMbps) GetThroughput()
    {
        if (_activeAdapter == null)
            GetActiveAdapterInfo();

        if (_activeAdapter == null) return (0, 0);

        try
        {
            var stats = _activeAdapter.GetIPv4Statistics();
            var now = DateTime.UtcNow;

            if (!_hasLastStats)
            {
                _lastBytesReceived = stats.BytesReceived;
                _lastBytesSent = stats.BytesSent;
                _lastStatsTime = now;
                _hasLastStats = true;
                return (0, 0);
            }

            var elapsed = (now - _lastStatsTime).TotalSeconds;
            if (elapsed < 0.1) return (0, 0);

            var inBytes = Math.Max(0, stats.BytesReceived - _lastBytesReceived) / elapsed;
            var outBytes = Math.Max(0, stats.BytesSent - _lastBytesSent) / elapsed;

            _lastBytesReceived = stats.BytesReceived;
            _lastBytesSent = stats.BytesSent;
            _lastStatsTime = now;

            return (inBytes / 1_000_000.0 * 8, outBytes / 1_000_000.0 * 8); // Mbps
        }
        catch { return (0, 0); }
    }

    public async Task<long> PingAsync(string host = "1.1.1.1", int timeoutMs = 2000, CancellationToken ct = default)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeoutMs).WaitAsync(ct);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : -1;
        }
        catch { return -1; }
    }

    public async Task<IReadOnlyList<PingResult>> PingAllTargetsAsync(CancellationToken ct = default)
    {
        var tasks = PingTargets.Select((host, i) => PingSingleAsync(PingTargetNames[i], host, ct));
        var results = await Task.WhenAll(tasks);
        return results;
    }

    public async Task<SpeedTestResult> RunSpeedTestAsync(IProgress<SpeedTestProgress>? progress = null, CancellationToken ct = default)
    {
        progress?.Report(new SpeedTestProgress("Mesure du ping", 5));
        var pingSamples = await RunOptionalAsync(
            token => MeasureNetworkPingAsync(progress, token),
            Array.Empty<double>(),
            TimeSpan.FromSeconds(4),
            ct);

        var averagePing = pingSamples.Count > 0 ? pingSamples.Average() : 0;
        var jitter = CalculateJitter(pingSamples);

        progress?.Report(new SpeedTestProgress("Mesure du download", 30));
        var downloadMbps = await RunOptionalAsync(
            token => MeasureDownloadAsync(progress, token),
            0d,
            TimeSpan.FromSeconds(15),
            ct);

        progress?.Report(new SpeedTestProgress("Mesure de l'upload", 70));
        var uploadMbps = await RunOptionalAsync(
            token => MeasureUploadAsync(progress, token),
            0d,
            TimeSpan.FromSeconds(10),
            ct);

        progress?.Report(new SpeedTestProgress("Test termine", 100));
        return new SpeedTestResult(
            Math.Round(averagePing, 1),
            Math.Round(downloadMbps, 2),
            Math.Round(uploadMbps, 2),
            Math.Round(jitter, 1));
    }

    private async Task<IReadOnlyList<double>> MeasureNetworkPingAsync(IProgress<SpeedTestProgress>? progress, CancellationToken ct)
    {
        var tasks = PingTargets.Select(host => PingAsync(host, 1000, ct));
        var results = await Task.WhenAll(tasks);
        progress?.Report(new SpeedTestProgress("Ping termine", 25));

        return results
            .Where(ms => ms >= 0)
            .Select(ms => (double)ms)
            .ToArray();
    }

    private async Task<IReadOnlyList<double>> MeasureCloudflarePingAsync(IProgress<SpeedTestProgress>? progress, CancellationToken ct)
    {
        var samples = new List<double>();
        const int sampleCount = 3;
        for (var i = 0; i < sampleCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            using var request = new HttpRequestMessage(HttpMethod.Get, PingUrl);
            var sw = Stopwatch.StartNew();
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();
            sw.Stop();
            samples.Add(sw.Elapsed.TotalMilliseconds);
            progress?.Report(new SpeedTestProgress($"Ping {i + 1}/{sampleCount}", 5 + ((i + 1) * 6)));
            await Task.Delay(60, ct);
        }

        return samples;
    }

    private async Task<double> MeasureDownloadAsync(IProgress<SpeedTestProgress>? progress, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, DownloadUrl);
        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        var buffer = new byte[128 * 1024];
        long totalRead = 0;
        var sw = Stopwatch.StartNew();

        while (true)
        {
            var read = await stream.ReadAsync(buffer, ct);
            if (read == 0)
                break;

            totalRead += read;
            var percent = 30 + Math.Min(35, totalRead / (double)DownloadBytes * 35);
            progress?.Report(new SpeedTestProgress("Download en cours", percent));
        }

        sw.Stop();
        return ToMbps(totalRead, sw.Elapsed);
    }

    private async Task<double> MeasureUploadAsync(IProgress<SpeedTestProgress>? progress, CancellationToken ct)
    {
        var uploadData = new byte[UploadBytes];
        Exception? lastError = null;

        for (var i = 0; i < UploadUrls.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(new SpeedTestProgress($"Upload en cours ({i + 1}/{UploadUrls.Length})", 70 + (i * 6)));

            try
            {
                using var content = new ByteArrayContent(uploadData);
                content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
                using var request = new HttpRequestMessage(HttpMethod.Post, UploadUrls[i])
                {
                    Content = content
                };

                var sw = Stopwatch.StartNew();
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();
                sw.Stop();

                progress?.Report(new SpeedTestProgress("Upload termine", 95));
                return ToMbps(UploadBytes, sw.Elapsed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex;
            }
        }

        throw new HttpRequestException("Aucun serveur d'upload n'a repondu.", lastError);
    }

    private static async Task<T> RunOptionalAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        T fallback,
        TimeSpan timeout,
        CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            return await operation(timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return fallback;
        }
        catch (HttpRequestException)
        {
            return fallback;
        }
        catch (PingException)
        {
            return fallback;
        }
    }

    private static double CalculateJitter(IReadOnlyList<double> samples)
    {
        if (samples.Count < 2)
            return 0;

        var deltas = new List<double>();
        for (var i = 1; i < samples.Count; i++)
            deltas.Add(Math.Abs(samples[i] - samples[i - 1]));

        return deltas.Average();
    }

    private static double ToMbps(long bytes, TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds <= 0)
            return 0;

        return bytes * 8d / elapsed.TotalSeconds / 1_000_000d;
    }

    private static async Task<PingResult> PingSingleAsync(string name, string host, CancellationToken ct)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, 2000).WaitAsync(ct);
            if (reply.Status == IPStatus.Success)
                return new PingResult(name, true, reply.RoundtripTime, string.Empty);
            return new PingResult(name, false, -1, reply.Status.ToString());
        }
        catch (Exception ex)
        {
            return new PingResult(name, false, -1, ex.Message);
        }
    }

    public async Task<string> RepairNetworkAsync(CancellationToken ct = default)
    {
        var log = new System.Text.StringBuilder();
        var commands = new[]
        {
            ("ipconfig", "/flushdns"),
            ("ipconfig", "/registerdns"),
            ("netsh", "winsock reset catalog"),
        };

        foreach (var (exe, args) in commands)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo(exe, args)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = System.Diagnostics.Process.Start(psi)!;
                var outputTask = proc.StandardOutput.ReadToEndAsync(ct);
                var errorTask = proc.StandardError.ReadToEndAsync(ct);
                await proc.WaitForExitAsync(ct);
                var output = await outputTask;
                var error = await errorTask;
                log.AppendLine(proc.ExitCode == 0
                    ? $"[OK] {exe} {args} {output.Trim()}".Trim()
                    : $"[ERREUR] {exe} {args} : {error.Trim()}");
            }
            catch (Exception ex)
            {
                log.AppendLine($"[ERREUR] {exe} {args} : {ex.Message}");
            }
        }
        return log.ToString();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();
    }
}
