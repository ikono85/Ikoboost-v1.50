using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using IkoboostWpf.Models;
using Microsoft.Win32;

namespace IkoboostWpf.Services;

public sealed class OptimizeService
{
    [DllImport("kernel32.dll")] private static extern bool SetProcessWorkingSetSize(nint proc, nint min, nint max);

    public static readonly Dictionary<string, string> PowerPlans = new()
    {
        ["Équilibré"] = "381b4222-f694-41f0-9685-ff5bb260df2e",
        ["Performances élevées"] = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
        ["Économies d'énergie"] = "a1841308-3541-4fab-bc81-f71556f20b4a",
        ["Ultra performances"] = "e9a42b02-d5df-448d-aa00-03f14749eb61",
    };

    public async Task<string> CleanTempFilesAsync(IProgress<string>? progress = null)
    {
        var sb = new StringBuilder();
        long totalFreed = 0;

        var folders = new[]
        {
            Path.GetTempPath(),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
        };

        foreach (var folder in folders.Distinct())
        {
            if (!Directory.Exists(folder)) continue;
            progress?.Report($"Nettoyage : {folder}");
            try
            {
                foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        totalFreed += info.Length;
                        info.Delete();
                    }
                    catch { }
                }
                foreach (var dir in Directory.GetDirectories(folder))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }
            catch { }
        }

        sb.AppendLine($"✓ Fichiers temporaires nettoyés.");
        sb.AppendLine($"  Espace libéré : {FormatBytes(totalFreed)}");
        return sb.ToString().Trim();
    }

    public async Task<string> CleanDiskAsync()
    {
        try
        {
            var p = Process.Start("cleanmgr.exe", "/sagerun:1");
            return "✓ Nettoyage de disque lancé.";
        }
        catch (Exception ex) { return $"✗ {ex.Message}"; }
    }

    public (long Before, long After, long Freed) CleanRam()
    {
        var before = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect();

        foreach (var p in Process.GetProcesses())
        {
            try { SetProcessWorkingSetSize(p.Handle, -1, -1); } catch { }
        }
        var after = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        var freed = after - before;
        return (before / 1024 / 1024, after / 1024 / 1024, Math.Abs(freed) / 1024 / 1024);
    }

    public async Task<string> CreateRestorePointAsync()
    {
        try
        {
            var script = "Checkpoint-Computer -Description 'Ikoboost Restore' -RestorePointType MODIFY_SETTINGS";
            var p = Process.Start(new ProcessStartInfo("powershell.exe",
                $"-NonInteractive -Command \"{script}\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
            await p.WaitForExitAsync();
            return p.ExitCode == 0
                ? "✓ Point de restauration créé."
                : "⚠ Point de restauration : opération terminée (peut nécessiter les droits admin).";
        }
        catch (Exception ex) { return $"✗ {ex.Message}"; }
    }

    public async Task<string> RepairSystemAsync(IProgress<string>? progress = null)
    {
        var sb = new StringBuilder();
        progress?.Report("SFC /scannow en cours...");
        try
        {
            var sfc = Process.Start(new ProcessStartInfo("sfc", "/scannow")
            {
                UseShellExecute = true,
                Verb = "runas"
            });
            if (sfc != null) await sfc.WaitForExitAsync();
            sb.AppendLine("✓ SFC terminé.");
        }
        catch (Exception ex) { sb.AppendLine($"✗ SFC : {ex.Message}"); }
        return sb.ToString().Trim();
    }

    public async Task<string> SetPowerPlanAsync(string planName)
    {
        if (!PowerPlans.TryGetValue(planName, out var guid))
            return "Profil inconnu.";
        try
        {
            var p = Process.Start(new ProcessStartInfo("powercfg", $"/setactive {guid}")
            {
                UseShellExecute = false,
                CreateNoWindow = true
            })!;
            await p.WaitForExitAsync();
            return $"✓ Profil d'alimentation « {planName} » activé.";
        }
        catch (Exception ex) { return $"✗ {ex.Message}"; }
    }

    public async Task<List<DriverInfo>> GetDriversAsync()
    {
        var list = new List<DriverInfo>();
        try
        {
            using var s = new ManagementObjectSearcher("SELECT * FROM Win32_PnPSignedDriver WHERE DriverVersion IS NOT NULL");
            foreach (ManagementObject obj in s.Get())
            {
                var dev = obj["DeviceName"]?.ToString();
                if (string.IsNullOrWhiteSpace(dev)) continue;
                list.Add(new DriverInfo(
                    dev,
                    obj["ProviderName"]?.ToString() ?? "",
                    obj["DriverVersion"]?.ToString() ?? "",
                    obj["DriverDate"]?.ToString()?.Substring(0, Math.Min(8, obj["DriverDate"]?.ToString()?.Length ?? 0)) ?? ""
                ));
            }
        }
        catch { }
        return list.DistinctBy(d => d.DeviceName).OrderBy(d => d.DeviceName).ToList();
    }

    public async Task<List<DuplicateGroup>> ScanDuplicatesAsync(string folder, IProgress<string>? progress = null)
    {
        var groups = new List<DuplicateGroup>();
        if (!Directory.Exists(folder)) return groups;

        progress?.Report("Scan en cours...");
        var byHash = new Dictionary<string, List<string>>();
        try
        {
            foreach (var file in Directory.GetFiles(folder, "*", SearchOption.AllDirectories))
            {
                try
                {
                    var hash = await ComputeHashAsync(file);
                    if (!byHash.ContainsKey(hash)) byHash[hash] = new();
                    byHash[hash].Add(file);
                }
                catch { }
            }
        }
        catch { }

        foreach (var (hash, files) in byHash.Where(kv => kv.Value.Count > 1))
        {
            var size = new FileInfo(files[0]).Length;
            groups.Add(new DuplicateGroup
            {
                Header = $"{files.Count} fichiers identiques — {FormatBytes(size)} chacun",
                Files = files.Select(f => new DuplicateFile { Path = f, Size = size }).ToList()
            });
        }
        return groups;
    }

    public static List<RegistryOptimization> GetRegistryOptimizations() => new()
    {
        new() { DisplayName = "Désactiver Cortana", Action = "Désactiver", KeyPath = @"SOFTWARE\Policies\Microsoft\Windows\Windows Search", ValueName = "AllowCortana", NewValue = 0, ValueKind = RegistryValueKind.DWord, Description = "Désactive Cortana dans la barre de recherche.", Recommended = true },
        new() { DisplayName = "Désactiver la télémétrie", Action = "Désactiver", KeyPath = @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", ValueName = "AllowTelemetry", NewValue = 0, ValueKind = RegistryValueKind.DWord, Description = "Réduit les données envoyées à Microsoft.", Recommended = true },
        new() { DisplayName = "Accélérer les menus", Action = "Optimiser", KeyPath = @"Control Panel\Desktop", ValueName = "MenuShowDelay", NewValue = "0", ValueKind = RegistryValueKind.String, Description = "Affichage instantané des menus.", Recommended = true },
        new() { DisplayName = "Désactiver UAC auto-élév.", Action = "Désactiver", KeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", ValueName = "EnableLUA", NewValue = 0, ValueKind = RegistryValueKind.DWord, Description = "Désactive les invites UAC automatiques (risque modéré).", Recommended = false },
        new() { DisplayName = "Activer Windows Defender", Action = "Activer", KeyPath = @"SOFTWARE\Policies\Microsoft\Windows Defender", ValueName = "DisableAntiSpyware", NewValue = 0, ValueKind = RegistryValueKind.DWord, Description = "S'assure que Windows Defender est actif.", Recommended = true },
    };

    public static List<BloatwareApp> GetBloatwareList() => new()
    {
        new() { DisplayName = "Microsoft Solitaire Collection", PackageName = "Microsoft.MicrosoftSolitaireCollection", Description = "Jeux de cartes préinstallés.", Recommended = true },
        new() { DisplayName = "Candy Crush Saga", PackageName = "king.com.CandyCrushSaga", Description = "Jeu mobile préinstallé.", Recommended = true },
        new() { DisplayName = "Xbox", PackageName = "Microsoft.XboxApp", Description = "Application Xbox (si non utilisée).", Recommended = false },
        new() { DisplayName = "Skype", PackageName = "Microsoft.SkypeApp", Description = "Application Skype préinstallée.", Recommended = true },
        new() { DisplayName = "Maps", PackageName = "Microsoft.WindowsMaps", Description = "Cartes Windows.", Recommended = true },
        new() { DisplayName = "3D Viewer", PackageName = "Microsoft.Microsoft3DViewer", Description = "Visionneuse 3D.", Recommended = true },
        new() { DisplayName = "Mixed Reality Portal", PackageName = "Microsoft.MixedReality.Portal", Description = "Portail de réalité mixte.", Recommended = true },
        new() { DisplayName = "People", PackageName = "Microsoft.People", Description = "Application Contacts.", Recommended = true },
        new() { DisplayName = "Mail & Calendar", PackageName = "microsoft.windowscommunicationsapps", Description = "Mail/Calendrier Metro (si Outlook utilisé).", Recommended = false },
    };

    public async Task<string> ApplyRegistryOptimizationsAsync(IEnumerable<RegistryOptimization> selected)
    {
        var sb = new StringBuilder();
        foreach (var opt in selected.Where(o => o.IsSelected))
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(opt.KeyPath, true);
                key?.SetValue(opt.ValueName, opt.NewValue ?? 0, opt.ValueKind);
                sb.AppendLine($"✓ {opt.DisplayName}");
            }
            catch (Exception ex) { sb.AppendLine($"✗ {opt.DisplayName} : {ex.Message}"); }
        }
        return sb.Length > 0 ? sb.ToString().Trim() : "Aucune optimisation sélectionnée.";
    }

    public async Task<string> UninstallBloatwareAsync(IEnumerable<BloatwareApp> selected, IProgress<string>? progress = null)
    {
        var sb = new StringBuilder();
        foreach (var app in selected.Where(a => a.IsSelected))
        {
            progress?.Report($"Désinstallation : {app.DisplayName}...");
            try
            {
                var p = Process.Start(new ProcessStartInfo("powershell.exe",
                    $"-NonInteractive -Command \"Get-AppxPackage -Name '{app.PackageName}' | Remove-AppxPackage\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                })!;
                await p.WaitForExitAsync();
                sb.AppendLine($"✓ {app.DisplayName} désinstallé.");
            }
            catch (Exception ex) { sb.AppendLine($"✗ {app.DisplayName} : {ex.Message}"); }
        }
        return sb.Length > 0 ? sb.ToString().Trim() : "Aucune application sélectionnée.";
    }

    private static async Task<string> ComputeHashAsync(string path)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        await using var stream = File.OpenRead(path);
        var hash = await sha.ComputeHashAsync(stream);
        return Convert.ToHexString(hash);
    }

    private static string FormatBytes(long bytes) =>
        bytes switch
        {
            >= 1024L * 1024 * 1024 => $"{bytes / 1024.0 / 1024 / 1024:F2} Go",
            >= 1024 * 1024 => $"{bytes / 1024.0 / 1024:F1} Mo",
            >= 1024 => $"{bytes / 1024.0:F0} Ko",
            _ => $"{bytes} o"
        };
}
