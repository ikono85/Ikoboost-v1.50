using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IkoboostWpf.Services;

public sealed class OptimizationService
{
    public sealed record RamCleanupResult(int ProcessesOptimized, int ProcessesSkipped, long BeforeMb, long AfterMb);
    public sealed record BloatwareApp(string DisplayName, string PackageName, string Description);
    public sealed record RegistryOptimization(
        string DisplayName,
        string Description,
        string Hive,
        string KeyPath,
        string ValueName,
        string ValueType,
        string? ValueData,
        bool DeleteValue);

    public sealed record DuplicateGroup(string Hash, long SizeBytes, IReadOnlyList<string> Paths);

    public sealed record DriverInfo(string DeviceName, string DriverVersion, string DriverDate, string ProviderName);

    public static readonly Dictionary<string, string> DnsProfiles = new()
    {
        ["Cloudflare"] = "1.1.1.1",
        ["Google"] = "8.8.8.8",
        ["Quad9"] = "9.9.9.9",
        ["OpenDNS"] = "208.67.222.222"
    };

    public static readonly Dictionary<string, string> PowerPlans = new()
    {
        ["Economie d'energie"] = "a1841308-3541-4fab-bc81-f71556f20b4a",
        ["Equilibre"] = "381b4222-f694-41f0-9685-ff5bb260df2e",
        ["Haute performance"] = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"
    };

    public static readonly IReadOnlyList<BloatwareApp> BloatwareApps =
    [
        new("Clipchamp", "Clipchamp.Clipchamp", "Editeur video preinstalle"),
        new("Cortana", "Microsoft.549981C3F5F10", "Assistant Microsoft"),
        new("Microsoft News", "Microsoft.BingNews", "Actualites Microsoft"),
        new("Microsoft Solitaire", "Microsoft.MicrosoftSolitaireCollection", "Jeux Solitaire"),
        new("Microsoft To Do", "Microsoft.Todos", "Liste de taches Microsoft"),
        new("Mixed Reality Portal", "Microsoft.MixedReality.Portal", "Portail realite mixte"),
        new("Movies & TV", "Microsoft.ZuneVideo", "Lecteur films et TV"),
        new("MSN Weather", "Microsoft.BingWeather", "Meteo Microsoft"),
        new("Office Hub", "Microsoft.MicrosoftOfficeHub", "Raccourci Office / Microsoft 365"),
        new("OneNote", "Microsoft.Office.OneNote", "OneNote preinstalle"),
        new("People", "Microsoft.People", "Contacts Microsoft"),
        new("Power Automate", "Microsoft.PowerAutomateDesktop", "Automatisation Windows"),
        new("Skype", "Microsoft.SkypeApp", "Application Skype"),
        new("Spotify", "SpotifyAB.SpotifyMusic", "Spotify preinstalle"),
        new("Xbox", "Microsoft.GamingApp", "Application Xbox"),
        new("Xbox Game Bar", "Microsoft.XboxGamingOverlay", "Overlay Xbox Game Bar"),
        new("Xbox Identity Provider", "Microsoft.XboxIdentityProvider", "Service de connexion Xbox")
    ];

    public static readonly IReadOnlyList<RegistryOptimization> RegistryOptimizations =
    [
        new(
            "Désactiver Xbox Game DVR",
            "Réduit les captures/overlays Xbox en arrière-plan.",
            "HKCU",
            @"Software\Microsoft\Windows\CurrentVersion\GameDVR",
            "AppCaptureEnabled",
            "REG_DWORD",
            "0",
            false),
        new(
            "Désactiver Game Bar auto",
            "Empêche l'ouverture automatique de la Xbox Game Bar.",
            "HKCU",
            @"Software\Microsoft\GameBar",
            "AutoGameModeEnabled",
            "REG_DWORD",
            "0",
            false),
        new(
            "Désactiver pubs personnalisées",
            "Coupe l'identifiant publicitaire Windows pour l'utilisateur courant.",
            "HKCU",
            @"Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
            "Enabled",
            "REG_DWORD",
            "0",
            false),
        new(
            "Désactiver suggestions Windows",
            "Réduit les contenus suggérés dans Windows.",
            "HKCU",
            @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
            "SubscribedContent-338388Enabled",
            "REG_DWORD",
            "0",
            false),
        new(
            "Désactiver astuces écran verrouillage",
            "Retire les astuces et contenus promotionnels de l'écran de verrouillage.",
            "HKCU",
            @"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
            "RotatingLockScreenOverlayEnabled",
            "REG_DWORD",
            "0",
            false),
        new(
            "Accélérer démarrage utilisateur",
            "Supprime le délai artificiel de lancement des apps au démarrage.",
            "HKCU",
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize",
            "StartupDelayInMSec",
            "REG_DWORD",
            "0",
            false),
        new(
            "Désactiver recherche Bing menu Démarrer",
            "Limite la recherche du menu Démarrer aux résultats locaux.",
            "HKCU",
            @"Software\Microsoft\Windows\CurrentVersion\Search",
            "BingSearchEnabled",
            "REG_DWORD",
            "0",
            false),
        new(
            "Désactiver Cortana recherche",
            "Désactive l'assistant Cortana dans la recherche Windows.",
            "HKCU",
            @"Software\Microsoft\Windows\CurrentVersion\Search",
            "CortanaConsent",
            "REG_DWORD",
            "0",
            false),
        new(
            "Retirer OneDrive du démarrage",
            "Supprime seulement l'entrée de démarrage automatique OneDrive.",
            "HKCU",
            @"Software\Microsoft\Windows\CurrentVersion\Run",
            "OneDrive",
            "REG_SZ",
            null,
            true)
    ];

    public async Task<string> SetDnsAsync(string adapterName, string dns, CancellationToken ct = default)
    {
        return await RunNetshAsync(
            $"interface ip set dns name=\"{adapterName}\" static {dns} primary", ct);
    }

    public async Task<string> FlushDnsAsync(CancellationToken ct = default)
        => await RunCommandAsync("ipconfig", "/flushdns", ct);

    public async Task<string> SetPowerPlanAsync(string planGuid, CancellationToken ct = default)
    {
        return await RunCommandAsync("powercfg", $"/setactive {planGuid}", ct);
    }

    public async Task<string> CreateRestorePointAsync(CancellationToken ct = default)
    {
        var name = $"Ikoboost - {DateTime.Now:yyyy-MM-dd HH-mm}";
        var script = "$ErrorActionPreference = 'Stop'; "
                   + $"Checkpoint-Computer -Description '{EscapePowerShellSingleQuoted(name)}' -RestorePointType 'MODIFY_SETTINGS'; "
                   + $"'[OK] Point de restauration cree : {EscapePowerShellSingleQuoted(name)}'";

        return await RunPowerShellAsync(script, ct);
    }

    public async Task<RamCleanupResult> CleanRamAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var beforeMb = GetTotalWorkingSetMb();
            var optimized = 0;
            var skipped = 0;
            var currentProcessId = Environment.ProcessId;

            foreach (var process in Process.GetProcesses())
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    if (process.Id == currentProcessId || process.HasExited)
                    {
                        skipped++;
                        continue;
                    }

                    if (EmptyWorkingSet(process.Handle))
                        optimized++;
                    else
                        skipped++;
                }
                catch
                {
                    skipped++;
                }
                finally
                {
                    process.Dispose();
                }
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Thread.Sleep(250);
            var afterMb = GetTotalWorkingSetMb();
            return new RamCleanupResult(optimized, skipped, beforeMb, afterMb);
        }, ct);
    }

    public async Task<string> CleanRamLogAsync(CancellationToken ct = default)
    {
        var result = await CleanRamAsync(ct);
        var freedMb = Math.Max(0, result.BeforeMb - result.AfterMb);
        return $"[OK] Cleaner RAM termine.\n"
             + $"Processus optimises : {result.ProcessesOptimized}\n"
             + $"Processus ignores : {result.ProcessesSkipped}\n"
             + $"RAM avant : {result.BeforeMb:N0} Mo\n"
             + $"RAM apres : {result.AfterMb:N0} Mo\n"
             + $"RAM liberee estimee : {freedMb:N0} Mo";
    }

    public async Task<string> CleanupSystemAsync(CancellationToken ct = default)
    {
        var log = new StringBuilder();

        var userTemp = Path.GetFullPath(Path.GetTempPath());
        var windowsTemp = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));

        var userTempResult = CleanDirectory(userTemp, ct);
        log.AppendLine($"[OK] Temp utilisateur : {userTempResult.FilesDeleted} fichiers et {userTempResult.DirectoriesDeleted} dossiers supprimes. {userTempResult.Failures} ignores.");

        if (!string.Equals(userTemp.TrimEnd('\\'), windowsTemp.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
        {
            var windowsTempResult = CleanDirectory(windowsTemp, ct);
            log.AppendLine($"[OK] Temp Windows : {windowsTempResult.FilesDeleted} fichiers et {windowsTempResult.DirectoriesDeleted} dossiers supprimes. {windowsTempResult.Failures} ignores.");
        }

        try
        {
            var prefetch = @"C:\Windows\Prefetch";
            if (Directory.Exists(prefetch))
            {
                var deleted = 0;
                foreach (var f in Directory.EnumerateFiles(prefetch, "*.pf"))
                {
                    try
                    {
                        File.SetAttributes(f, FileAttributes.Normal);
                        File.Delete(f);
                        deleted++;
                    }
                    catch
                    {
                        // Locked or protected files are expected here.
                    }
                }
                log.AppendLine($"[OK] Prefetch : {deleted} fichiers supprimes.");
            }
        }
        catch (Exception ex) { log.AppendLine($"[ERREUR] Prefetch : {ex.Message}"); }

        var dnsResult = await RunCommandAsync("ipconfig", "/flushdns", ct);
        log.AppendLine($"[OK] DNS flush : {dnsResult.Trim()}");

        return log.ToString();
    }

    public async Task<string> RepairSystemAsync(CancellationToken ct = default)
    {
        var log = new StringBuilder();

        var sfcResult = await RunCommandAsync("sfc", "/scannow", ct);
        log.AppendLine(sfcResult.StartsWith("Erreur :", StringComparison.OrdinalIgnoreCase)
            ? $"[ERREUR] SFC : {sfcResult.Trim()}"
            : $"[OK] SFC : {sfcResult.Trim()}");

        var dismResult = await RunCommandAsync("DISM", "/Online /Cleanup-Image /RestoreHealth", ct);
        log.AppendLine(dismResult.StartsWith("Erreur :", StringComparison.OrdinalIgnoreCase)
            ? $"[ERREUR] DISM : {dismResult.Trim()}"
            : $"[OK] DISM : {dismResult.Trim()}");

        return log.ToString();
    }

    public async Task<string> CleanDiskAsync(CancellationToken ct = default)
    {
        var log = new StringBuilder();
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        var localTemp = Path.Combine(localAppData, "Temp");
        var localTempResult = CleanDirectory(localTemp, ct);
        log.AppendLine($"[OK] %LOCALAPPDATA%\\Temp : {localTempResult.FilesDeleted} fichiers ({localTempResult.DirectoriesDeleted} dossiers) supprimes. {localTempResult.Failures} ignores.");

        var inetCache = Path.Combine(localAppData, @"Microsoft\Windows\INetCache");
        var inetResult = CleanDirectory(inetCache, ct);
        log.AppendLine($"[OK] INetCache : {inetResult.FilesDeleted} fichiers supprimes. {inetResult.Failures} ignores.");

        try
        {
            var explorerDir = Path.Combine(localAppData, @"Microsoft\Windows\Explorer");
            if (Directory.Exists(explorerDir))
            {
                var thumbCount = 0;
                foreach (var f in Directory.EnumerateFiles(explorerDir, "thumbcache_*.db"))
                {
                    try { File.Delete(f); thumbCount++; }
                    catch { }
                }
                log.AppendLine($"[OK] Miniatures (thumbcache) : {thumbCount} fichiers supprimes.");
            }
        }
        catch (Exception ex) { log.AppendLine($"[ERREUR] Thumbcache : {ex.Message}"); }

        try
        {
            SHEmptyRecycleBin(IntPtr.Zero, null!, 0x00000001 | 0x00000002);
            log.AppendLine("[OK] Corbeille videe.");
        }
        catch (Exception ex) { log.AppendLine($"[ERREUR] Corbeille : {ex.Message}"); }

        return log.ToString();
    }

    public async Task<List<DuplicateGroup>> FindDuplicatesAsync(string rootPath, IProgress<string>? progress, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            const long minSize = 1024;
            const int hashReadLimit = 512 * 1024;

            var bySize = new Dictionary<long, List<string>>();

            foreach (var file in EnumerateFilesSafe(rootPath, ct))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(file);
                    if (info.Length < minSize) continue;
                    if (!bySize.TryGetValue(info.Length, out var list))
                    {
                        list = [];
                        bySize[info.Length] = list;
                    }
                    list.Add(file);
                }
                catch { }
            }

            var candidates = bySize.Where(kv => kv.Value.Count > 1).ToList();
            progress?.Report($"Calcul des hash pour {candidates.Sum(c => c.Value.Count)} fichiers...");

            var byHash = new Dictionary<string, List<(long Size, string Path)>>();
            foreach (var (size, files) in candidates)
            {
                foreach (var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                        var buffer = new byte[Math.Min(hashReadLimit, fs.Length)];
                        var read = fs.Read(buffer, 0, buffer.Length);
                        var hash = Convert.ToHexString(SHA256.HashData(buffer.AsSpan(0, read)));
                        if (!byHash.TryGetValue(hash, out var group))
                        {
                            group = [];
                            byHash[hash] = group;
                        }
                        group.Add((size, file));
                    }
                    catch { }
                }
            }

            return byHash
                .Where(kv => kv.Value.Count >= 2)
                .Select(kv => new DuplicateGroup(kv.Key, kv.Value[0].Size, kv.Value.Select(x => x.Path).ToList()))
                .OrderByDescending(g => g.SizeBytes)
                .ToList();
        }, ct);
    }

    public async Task<string> DeleteDuplicatesAsync(IEnumerable<string> pathsToDelete, CancellationToken ct = default)
    {
        var log = new StringBuilder();
        foreach (var path in pathsToDelete)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                File.Delete(path);
                log.AppendLine($"[OK] Supprime : {path}");
            }
            catch (Exception ex)
            {
                log.AppendLine($"[ERREUR] {path} : {ex.Message}");
            }
        }
        return await Task.FromResult(log.ToString());
    }

    public async Task<List<DriverInfo>> GetInstalledDriversAsync(CancellationToken ct = default)
    {
        var script = "Get-WmiObject Win32_PnPSignedDriver | Where-Object { $_.DeviceName -ne $null } | Select-Object DeviceName, DriverVersion, DriverDate, ProviderName | ConvertTo-Json -Compress";
        var json = await RunPowerShellAsync(script, ct);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var items = root.ValueKind == JsonValueKind.Array ? root.EnumerateArray().ToList() : [root];
            return items.Select(e => new DriverInfo(
                e.TryGetProperty("DeviceName", out var dn) ? dn.GetString() ?? "" : "",
                e.TryGetProperty("DriverVersion", out var dv) ? dv.GetString() ?? "" : "",
                e.TryGetProperty("DriverDate", out var dd) ? dd.GetString() ?? "" : "",
                e.TryGetProperty("ProviderName", out var pn) ? pn.GetString() ?? "" : ""))
                .ToList();
        }
        catch { return []; }
    }

    public async Task<string> ScanDriverUpdatesAsync(CancellationToken ct = default)
    {
        var log = new StringBuilder();

        var pnpResult = await RunCommandAsync("pnputil", "/scan-devices", ct);
        log.AppendLine(pnpResult.StartsWith("Erreur :", StringComparison.OrdinalIgnoreCase)
            ? $"[ERREUR] pnputil : {pnpResult.Trim()}"
            : $"[OK] pnputil : {pnpResult.Trim()}");

        var usoResult = await RunCommandAsync("UsoClient", "StartScan", ct);
        log.AppendLine(usoResult.StartsWith("Erreur :", StringComparison.OrdinalIgnoreCase)
            ? $"[ERREUR] UsoClient : {usoResult.Trim()}"
            : $"[OK] UsoClient scan declenche.");

        return log.ToString();
    }

    public Task<string> OpenWindowsUpdateDriversAsync(CancellationToken ct = default)
    {
        Process.Start(new ProcessStartInfo("ms-settings:windowsupdate-optionalupdates") { UseShellExecute = true });
        return Task.FromResult("[OK] Page Windows Update ouverte.");
    }

    public async Task<string> UninstallBloatwareAsync(IEnumerable<BloatwareApp> apps, CancellationToken ct = default)
    {
        var selectedApps = apps.ToList();
        if (selectedApps.Count == 0)
            return "Aucun bloatware selectionne.";

        var log = new StringBuilder();
        foreach (var app in selectedApps)
        {
            ct.ThrowIfCancellationRequested();
            var script = $"$pkg = Get-AppxPackage -Name '{EscapePowerShellSingleQuoted(app.PackageName)}'; if ($pkg) {{ $pkg | Remove-AppxPackage; '[OK] {EscapePowerShellSingleQuoted(app.DisplayName)} supprime.' }} else {{ '[INFO] {EscapePowerShellSingleQuoted(app.DisplayName)} introuvable.' }}";
            var result = await RunPowerShellAsync(script, ct);
            log.AppendLine(result.Trim());
        }

        return log.ToString();
    }

    public async Task<string> ApplyRegistryOptimizationsAsync(IEnumerable<RegistryOptimization> optimizations, CancellationToken ct = default)
    {
        var selectedOptimizations = optimizations.ToList();
        if (selectedOptimizations.Count == 0)
            return "Aucune optimisation registre selectionnee.";

        var log = new StringBuilder();
        foreach (var optimization in selectedOptimizations)
        {
            ct.ThrowIfCancellationRequested();
            var args = optimization.DeleteValue
                ? BuildRegDeleteArgs(optimization)
                : BuildRegAddArgs(optimization);

            var result = await RunCommandAsync("reg.exe", args, ct);
            var action = optimization.DeleteValue ? "supprime" : "applique";
            log.AppendLine(result.StartsWith("Erreur :", StringComparison.OrdinalIgnoreCase)
                ? $"[ERREUR] {optimization.DisplayName} : {result}"
                : $"[OK] {optimization.DisplayName} {action}.");
        }

        return log.ToString();
    }

    private static CleanupResult CleanDirectory(string path, CancellationToken ct)
    {
        var result = new CleanupResult();
        if (!Directory.Exists(path))
            return result;

        var directories = new Stack<string>();
        var visitedDirectories = new List<string>();
        directories.Push(path);

        while (directories.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var current = directories.Pop();
            visitedDirectories.Add(current);

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(current).ToList(); }
            catch
            {
                result.Failures++;
                continue;
            }

            foreach (var file in files)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                    result.FilesDeleted++;
                }
                catch
                {
                    result.Failures++;
                }
            }

            IEnumerable<string> childDirectories;
            try { childDirectories = Directory.EnumerateDirectories(current).ToList(); }
            catch
            {
                result.Failures++;
                continue;
            }

            foreach (var child in childDirectories)
                directories.Push(child);
        }

        foreach (var directory in visitedDirectories
                     .Where(d => !string.Equals(d, path, StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(d => d.Length))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                Directory.Delete(directory, recursive: false);
                result.DirectoriesDeleted++;
            }
            catch
            {
                result.Failures++;
            }
        }

        return result;
    }

    private static async Task<string> RunNetshAsync(string args, CancellationToken ct)
        => await RunCommandAsync("netsh", args, ct);

    private static async Task<string> RunPowerShellAsync(string command, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo("powershell.exe")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add(command);

            using var proc = Process.Start(psi)!;
            var outputTask = proc.StandardOutput.ReadToEndAsync(ct);
            var errorTask = proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            var output = await outputTask;
            var error = await errorTask;
            return proc.ExitCode == 0 ? output : $"Erreur : {error}";
        }
        catch (Exception ex) { return $"Erreur : {ex.Message}"; }
    }

    private static async Task<string> RunCommandAsync(string exe, string args, CancellationToken ct)
    {
        try
        {
            var psi = new ProcessStartInfo(exe, args)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            using var proc = Process.Start(psi)!;
            var outputTask = proc.StandardOutput.ReadToEndAsync(ct);
            var errorTask = proc.StandardError.ReadToEndAsync(ct);
            await proc.WaitForExitAsync(ct);
            var output = await outputTask;
            var error = await errorTask;
            return proc.ExitCode == 0 ? output : $"Erreur : {error}";
        }
        catch (Exception ex) { return $"Erreur : {ex.Message}"; }
    }

    private static string EscapePowerShellSingleQuoted(string value)
        => value.Replace("'", "''");

    private static string BuildRegAddArgs(RegistryOptimization optimization)
    {
        var key = $@"{optimization.Hive}\{optimization.KeyPath}";
        return $"add \"{key}\" /v \"{optimization.ValueName}\" /t {optimization.ValueType} /d \"{optimization.ValueData}\" /f";
    }

    private static string BuildRegDeleteArgs(RegistryOptimization optimization)
    {
        var key = $@"{optimization.Hive}\{optimization.KeyPath}";
        return $"delete \"{key}\" /v \"{optimization.ValueName}\" /f";
    }

    private static long GetTotalWorkingSetMb()
    {
        long totalBytes = 0;
        foreach (var process in Process.GetProcesses())
        {
            try { totalBytes += process.WorkingSet64; }
            catch { }
            finally { process.Dispose(); }
        }

        return totalBytes / 1024 / 1024;
    }

    private static IEnumerable<string> EnumerateFilesSafe(string root, CancellationToken ct)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var dir = stack.Pop();
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(dir); }
            catch { continue; }
            foreach (var f in files) yield return f;
            IEnumerable<string> dirs;
            try { dirs = Directory.EnumerateDirectories(dir); }
            catch { continue; }
            foreach (var d in dirs) stack.Push(d);
        }
    }

    [DllImport("psapi.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHEmptyRecycleBin(IntPtr hwnd, string pszRootPath, uint dwFlags);

    private sealed class CleanupResult
    {
        public int FilesDeleted { get; set; }
        public int DirectoriesDeleted { get; set; }
        public int Failures { get; set; }
    }
}
