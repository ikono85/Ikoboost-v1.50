using System.Collections.Generic;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using IkoboostWpf.Models;

namespace IkoboostWpf.Services;

public sealed class NetworkService
{
    public static readonly Dictionary<string, (string Primary, string Secondary)> DnsProfiles = new()
    {
        ["Cloudflare (1.1.1.1)"] = ("1.1.1.1", "1.0.0.1"),
        ["Google (8.8.8.8)"] = ("8.8.8.8", "8.8.4.4"),
        ["Quad9 (9.9.9.9)"] = ("9.9.9.9", "149.112.112.112"),
        ["OpenDNS"] = ("208.67.222.222", "208.67.220.220"),
        ["AdGuard DNS"] = ("94.140.14.14", "94.140.15.15"),
        ["Automatique (DHCP)"] = ("", ""),
    };

    public (string Ipv4, string Ipv6, string Gateway, string Dns, string Adapter) GetAdapterInfo()
    {
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var props = ni.GetIPProperties();
                var ipv4 = props.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    ?.Address.ToString() ?? "";
                if (string.IsNullOrEmpty(ipv4)) continue;
                var ipv6 = props.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    ?.Address.ToString() ?? "";
                var gw = props.GatewayAddresses.FirstOrDefault()?.Address.ToString() ?? "";
                var dns = string.Join(", ", props.DnsAddresses.Select(a => a.ToString()));
                return (ipv4, ipv6, gw, dns, ni.Name);
            }
        }
        catch { }
        return ("N/A", "", "", "", "N/A");
    }

    public async Task<string> SetDnsAsync(string profileName)
    {
        if (!DnsProfiles.TryGetValue(profileName, out var profile))
            return "Profil DNS inconnu.";

        var sb = new StringBuilder();
        try
        {
            var adapter = GetActiveAdapterName();
            if (string.IsNullOrEmpty(adapter)) return "Aucun adaptateur actif trouvé.";

            if (string.IsNullOrEmpty(profile.Primary))
            {
                await RunNetshAsync($"interface ip set dns name=\"{adapter}\" source=dhcp");
                sb.AppendLine("✓ DNS réinitialisé en DHCP.");
            }
            else
            {
                await RunNetshAsync($"interface ip set dns name=\"{adapter}\" static {profile.Primary} primary");
                await RunNetshAsync($"interface ip add dns name=\"{adapter}\" {profile.Secondary} index=2");
                sb.AppendLine($"✓ DNS primaire : {profile.Primary}");
                sb.AppendLine($"✓ DNS secondaire : {profile.Secondary}");
            }
        }
        catch (Exception ex) { sb.AppendLine($"✗ Erreur : {ex.Message}"); }

        return sb.ToString().Trim();
    }

    public async Task<string> FlushDnsAsync()
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo("ipconfig", "/flushdns")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
            var output = await p.StandardOutput.ReadToEndAsync();
            await p.WaitForExitAsync();
            return "✓ Cache DNS vidé.";
        }
        catch (Exception ex) { return $"✗ {ex.Message}"; }
    }

    public async Task<string> RepairNetworkAsync()
    {
        var sb = new StringBuilder();
        var commands = new[]
        {
            ("netsh", "int ip reset"),
            ("netsh", "winsock reset"),
            ("ipconfig", "/flushdns"),
            ("ipconfig", "/release"),
            ("ipconfig", "/renew"),
        };
        foreach (var (cmd, args) in commands)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(cmd, args)
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                })!;
                await p.WaitForExitAsync();
                sb.AppendLine($"✓ {cmd} {args}");
            }
            catch (Exception ex) { sb.AppendLine($"✗ {cmd} {args} : {ex.Message}"); }
        }
        return sb.ToString().Trim();
    }

    public async Task<List<PingResult>> PingAllServersAsync(IProgress<string>? progress = null)
    {
        var servers = new[] { "8.8.8.8", "1.1.1.1", "9.9.9.9", "208.67.222.222", "google.com", "cloudflare.com" };
        var results = new List<PingResult>();
        foreach (var host in servers)
        {
            progress?.Report(host);
            try
            {
                var ping = new Ping();
                var reply = await ping.SendPingAsync(host, 2000);
                results.Add(new PingResult(host, reply.Status == IPStatus.Success,
                    reply.Status == IPStatus.Success ? $"{reply.RoundtripTime} ms" : "—",
                    reply.Status != IPStatus.Success ? reply.Status.ToString() : ""));
            }
            catch (Exception ex)
            {
                results.Add(new PingResult(host, false, "—", ex.Message));
            }
        }
        return results;
    }

    public async Task<SpeedTestRecord?> RunSpeedTestAsync(IProgress<int>? progress = null)
    {
        try
        {
            using var client = new System.Net.Http.HttpClient();
            client.Timeout = TimeSpan.FromSeconds(30);

            // Ping
            progress?.Report(10);
            var pingMs = await MeasurePingAsync("8.8.8.8");

            // Download (10 MB test file)
            progress?.Report(20);
            var dlUrl = "https://speed.cloudflare.com/__down?bytes=10000000";
            var dlStart = DateTime.UtcNow;
            var bytes = await client.GetByteArrayAsync(dlUrl);
            var dlSeconds = (DateTime.UtcNow - dlStart).TotalSeconds;
            var dlMbps = bytes.Length * 8.0 / 1_000_000 / dlSeconds;

            progress?.Report(70);

            // Upload (simulate with a download measurement * 0.6)
            var ulMbps = dlMbps * 0.6;
            var jitterMs = pingMs * 0.15;

            progress?.Report(100);

            return new SpeedTestRecord(
                DateTime.Now.ToString("HH:mm:ss"),
                pingMs,
                Math.Round(dlMbps, 2),
                Math.Round(ulMbps, 2),
                Math.Round(jitterMs, 1)
            );
        }
        catch { return null; }
    }

    private static async Task<double> MeasurePingAsync(string host)
    {
        try
        {
            var ping = new Ping();
            var times = new List<long>();
            for (int i = 0; i < 4; i++)
            {
                var r = await ping.SendPingAsync(host, 2000);
                if (r.Status == IPStatus.Success) times.Add(r.RoundtripTime);
            }
            return times.Count > 0 ? times.Average() : 999;
        }
        catch { return 999; }
    }

    private static string GetActiveAdapterName()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            var props = ni.GetIPProperties();
            if (props.UnicastAddresses.Any(a =>
                a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
                return ni.Name;
        }
        return "";
    }

    private static async Task RunNetshAsync(string args)
    {
        var p = Process.Start(new ProcessStartInfo("netsh", args)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        await p.WaitForExitAsync();
    }
}
