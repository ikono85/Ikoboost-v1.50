using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Media;
using IkoboostWpf.Models;

namespace IkoboostWpf.Services;

public sealed class ProcessService
{
    private readonly Dictionary<int, (TimeSpan Cpu, DateTime Seen)> _previousCpu = new();
    private readonly Dictionary<int, (string Description, string Publisher)> _metadataCache = new();
    private static readonly Brush Cyan = new SolidColorBrush(Color.FromRgb(0x2F, 0xE6, 0xF2));
    private static readonly Brush Amber = new SolidColorBrush(Color.FromRgb(0xF5, 0xB5, 0x3D));

    public List<ProcessItem> GetProcesses(string filter = "")
    {
        var now = DateTime.UtcNow;
        var currentIds = new HashSet<int>();
        var list = new List<ProcessItem>();
        var processes = string.IsNullOrWhiteSpace(filter)
            ? Process.GetProcesses()
            : Process.GetProcesses().Where(p =>
                p.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();

        foreach (var p in processes)
        {
            try
            {
                currentIds.Add(p.Id);
                var cpu = ComputeCpuPercent(p, now);
                var priorityKey = SafePriority(p);
                var metadata = GetMetadata(p);
                list.Add(new ProcessItem
                {
                    Pid = p.Id,
                    Name = $"{p.ProcessName}.exe",
                    MemoryMb = $"{p.WorkingSet64 / 1024 / 1024:N0}",
                    PriorityKey = priorityKey,
                    Priority = PriorityLabel(priorityKey),
                    Description = metadata.Description,
                    Publisher = metadata.Publisher,
                    CpuPercent = cpu,
                    CpuBrush = cpu >= 10 ? Amber : Cyan,
                });
            }
            catch { }
        }

        foreach (var oldId in _previousCpu.Keys.Where(id => !currentIds.Contains(id)).ToArray())
            _previousCpu.Remove(oldId);
        foreach (var oldId in _metadataCache.Keys.Where(id => !currentIds.Contains(id)).ToArray())
            _metadataCache.Remove(oldId);

        return list.OrderByDescending(x => x.CpuPercent)
            .ThenByDescending(x => ParseMemory(x.MemoryMb))
            .ToList();
    }

    public string KillProcess(int pid)
    {
        try
        {
            var p = Process.GetProcessById(pid);
            p.Kill();
            return $"Processus {p.ProcessName} (PID {pid}) termine.";
        }
        catch (Exception ex) { return ex.Message; }
    }

    public string SetPriority(int pid, ProcessPriorityClass priority)
    {
        try
        {
            var p = Process.GetProcessById(pid);
            p.PriorityClass = priority;
            return $"Priorite de {p.ProcessName} changee en {PriorityLabel(priority.ToString())}.";
        }
        catch (Exception ex) { return ex.Message; }
    }

    private double ComputeCpuPercent(Process p, DateTime now)
    {
        var total = p.TotalProcessorTime;
        if (!_previousCpu.TryGetValue(p.Id, out var previous))
        {
            _previousCpu[p.Id] = (total, now);
            return 0;
        }

        var elapsedMs = (now - previous.Seen).TotalMilliseconds;
        var cpuMs = (total - previous.Cpu).TotalMilliseconds;
        _previousCpu[p.Id] = (total, now);
        if (elapsedMs <= 0) return 0;

        var percent = cpuMs / elapsedMs / Environment.ProcessorCount * 100.0;
        return Math.Round(Math.Clamp(percent, 0, 100), 1);
    }

    private static string SafePriority(Process p)
    {
        try { return p.PriorityClass.ToString(); }
        catch { return "Normal"; }
    }

    private static string PriorityLabel(string priority) => priority switch
    {
        "RealTime" => "Temps reel",
        "High" => "Elevee",
        "AboveNormal" => "Superieure",
        "BelowNormal" => "Inferieure",
        "Idle" => "Basse",
        _ => "Normale"
    };

    private static double ParseMemory(string memory) =>
        double.TryParse(memory.Replace(" ", "").Replace(",", ""),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0;

    private (string Description, string Publisher) GetMetadata(Process p)
    {
        if (_metadataCache.TryGetValue(p.Id, out var metadata))
            return metadata;

        metadata = (TryGetDescription(p), TryGetPublisher(p));
        _metadataCache[p.Id] = metadata;
        return metadata;
    }

    private static string TryGetDescription(Process p)
    {
        try { return p.MainModule?.FileVersionInfo.FileDescription ?? ""; }
        catch { return ""; }
    }

    private static string TryGetPublisher(Process p)
    {
        try { return p.MainModule?.FileVersionInfo.CompanyName ?? ""; }
        catch { return ""; }
    }
}
