using System.Diagnostics;
using System.Management;

namespace IkoboostWpf.Services;

public sealed class ProcessService
{
    public record ProcessInfo(
        int Pid,
        string Name,
        string Description,
        string Publisher,
        string Path,
        float CpuPercent,
        long MemoryMb,
        ProcessPriorityClass Priority);

    private readonly Dictionary<int, (TimeSpan cpuTime, DateTime wallTime)> _prevCpuTimes = [];

    public async Task<IReadOnlyList<ProcessInfo>> GetProcessesAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var now = DateTime.UtcNow;
            var cpuCount = Math.Max(1, Environment.ProcessorCount);
            var newTimes = new Dictionary<int, (TimeSpan, DateTime)>();
            var list = new List<ProcessInfo>();

            foreach (var p in Process.GetProcesses())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var memMb = p.WorkingSet64 / 1024 / 1024;
                    var path = string.Empty;
                    var desc = string.Empty;
                    var pub = string.Empty;
                    float cpuPercent = 0f;

                    try
                    {
                        var currentCpuTime = p.TotalProcessorTime;
                        newTimes[p.Id] = (currentCpuTime, now);
                        if (_prevCpuTimes.TryGetValue(p.Id, out var prev))
                        {
                            var cpuDelta = (currentCpuTime - prev.cpuTime).TotalMilliseconds;
                            var wallDelta = (now - prev.wallTime).TotalMilliseconds;
                            if (wallDelta > 0)
                                cpuPercent = (float)Math.Min(100.0, cpuDelta / (wallDelta * cpuCount) * 100.0);
                        }
                    }
                    catch { /* accès refusé ou processus terminé */ }

                    try
                    {
                        path = p.MainModule?.FileName ?? string.Empty;
                        if (!string.IsNullOrEmpty(path))
                        {
                            var fvi = FileVersionInfo.GetVersionInfo(path);
                            desc = fvi.FileDescription ?? string.Empty;
                            pub = fvi.CompanyName ?? string.Empty;
                        }
                    }
                    catch { /* accès refusé aux modules système */ }

                    list.Add(new ProcessInfo(p.Id, p.ProcessName, desc, pub, path, cpuPercent, memMb, p.PriorityClass));
                }
                catch { /* processus terminé entre-temps */ }
            }

            _prevCpuTimes.Clear();
            foreach (var kv in newTimes)
                _prevCpuTimes[kv.Key] = kv.Value;

            return (IReadOnlyList<ProcessInfo>)list;
        }, ct);
    }

    public async Task KillProcessAsync(int pid)
    {
        await Task.Run(() =>
        {
            using var p = Process.GetProcessById(pid);
            p.Kill(entireProcessTree: true);
            p.WaitForExit(5000);
        });
    }

    public async Task SetPriorityAsync(int pid, ProcessPriorityClass priority)
    {
        await Task.Run(() =>
        {
            using var p = Process.GetProcessById(pid);
            p.PriorityClass = priority;
        });
    }

    public async Task RestartProcessAsync(int pid)
    {
        await Task.Run(() =>
        {
            using var p = Process.GetProcessById(pid);
            var path = p.MainModule?.FileName ?? throw new InvalidOperationException("Chemin introuvable.");
            p.Kill(entireProcessTree: true);
            p.WaitForExit(5000);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        });
    }
}
