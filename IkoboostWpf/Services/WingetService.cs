using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Xml.Linq;

namespace IkoboostWpf.Services;

public sealed class WingetService
{
    public record AppInfo(
        string Id,
        string Name,
        string InstalledVersion,
        string AvailableVersion,
        bool UpdateAvailable,
        string Publisher = "N/A",
        string Source = "Registre",
        string UninstallString = "",
        string IconPath = "")
    {
        public string Initials
        {
            get
            {
                var words = Name.Split([' ', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
                if (words.Length == 0)
                    return "?";

                return string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])));
            }
        }

        public string DisplayVersion => InstalledVersion is "N/A" or "" ? "Version inconnue" : InstalledVersion;
        public string DisplayPublisher => Publisher is "N/A" or "" ? "Editeur inconnu" : Publisher;
        public string SourceLabel => Source.Contains("Appx", StringComparison.OrdinalIgnoreCase) ? "Store" : "Desktop";
    }

    private static readonly string[] ManagedAppIds =
    [
        "Google.Chrome",
        "Mozilla.Firefox",
        "VideoLAN.VLC",
        "7zip.7zip",
        "Microsoft.VisualStudioCode",
        "Notepad++.Notepad++",
        "Discord.Discord",
        "Spotify.Spotify",
        "OBSProject.OBSStudio",
        "Git.Git"
    ];

    public bool IsWingetAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("winget", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            if (p == null)
                return false;

            if (!p.WaitForExit(3000))
            {
                p.Kill(entireProcessTree: true);
                return false;
            }

            return p?.ExitCode == 0;
        }
        catch { return false; }
    }

    public async Task<IReadOnlyList<AppInfo>> ListInstalledAsync(CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var apps = new Dictionary<string, AppInfo>(StringComparer.OrdinalIgnoreCase);

            foreach (var app in ReadRegistryApps())
                apps.TryAdd(BuildAppKey(app), app);

            foreach (var app in ReadAppxPackages())
                apps.TryAdd(BuildAppKey(app), app);

            return (IReadOnlyList<AppInfo>)apps.Values
                .Where(a => !string.IsNullOrWhiteSpace(a.Name))
                .OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }, ct);
    }

    public async Task<string> InstallAsync(string appId, CancellationToken ct = default)
        => await RunWingetAsync($"install --id {appId} --silent --accept-package-agreements --accept-source-agreements", ct);

    public async Task<string> UninstallAsync(AppInfo app, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(app.UninstallString))
            return await RunUninstallStringAsync(app.UninstallString, ct);

        if (app.Source.Contains("Appx", StringComparison.OrdinalIgnoreCase))
            return await RunPowerShellAsync($"Get-AppxPackage -PackageFullName '{EscapePowerShell(app.Id)}' | Remove-AppxPackage", ct);

        if (IsWingetAvailable())
            return await RunWingetAsync($"uninstall --id {app.Id} --silent", ct);

        throw new InvalidOperationException("Aucune methode de desinstallation silencieuse disponible pour cette application.");
    }

    public async Task<string> UninstallAsync(string appId, CancellationToken ct = default)
        => await RunWingetAsync($"uninstall --id {appId} --silent", ct);

    public async Task<string> UpgradeAsync(string appId, CancellationToken ct = default)
        => await RunWingetAsync($"upgrade --id {appId} --silent --accept-package-agreements", ct);

    public async Task<string> UpgradeAllAsync(CancellationToken ct = default)
        => await RunWingetAsync("upgrade --all --silent --accept-package-agreements --accept-source-agreements", ct);

    public IReadOnlyList<string> GetManagedAppIds() => ManagedAppIds;

    private static IEnumerable<AppInfo> ReadRegistryApps()
    {
        var registryPaths = new[]
        {
            (Registry.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "Registre x64"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", "Registre x86"),
            (Registry.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", "Registre utilisateur")
        };

        foreach (var (root, path, source) in registryPaths)
        {
            using var key = root.OpenSubKey(path);
            if (key == null)
                continue;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using var appKey = key.OpenSubKey(subKeyName);
                if (appKey == null)
                    continue;

                var name = ReadString(appKey, "DisplayName");
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (ReadInt(appKey, "SystemComponent") == 1
                    || ReadString(appKey, "ReleaseType").Equals("Update", StringComparison.OrdinalIgnoreCase))
                    continue;

                var uninstallString = ReadString(appKey, "QuietUninstallString");
                if (string.IsNullOrWhiteSpace(uninstallString))
                    uninstallString = ReadString(appKey, "UninstallString");
                var iconPath = ReadString(appKey, "DisplayIcon");
                var installLocation = ReadString(appKey, "InstallLocation");
                if (string.IsNullOrWhiteSpace(iconPath))
                    iconPath = FindIconCandidate(name, installLocation, uninstallString);

                yield return new AppInfo(
                    subKeyName,
                    name,
                    EmptyToNa(ReadString(appKey, "DisplayVersion")),
                    "N/A",
                    false,
                    EmptyToNa(ReadString(appKey, "Publisher")),
                    source,
                    uninstallString,
                    iconPath);
            }
        }
    }

    private static IEnumerable<AppInfo> ReadAppxPackages()
    {
        using var packagesKey = Registry.CurrentUser.OpenSubKey(
            @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages");
        if (packagesKey == null)
            yield break;

        foreach (var packageFullName in packagesKey.GetSubKeyNames())
        {
            var parts = packageFullName.Split('_');
            var name = parts.Length > 0 ? parts[0] : packageFullName;
            var version = parts.Length > 1 ? parts[1] : "N/A";
            var publisher = parts.Length > 2 ? parts[^1] : "N/A";

            if (IsFrameworkPackage(name))
                continue;

            var packageRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps", packageFullName);
            var iconPath = FindAppxIcon(packageRoot);

            yield return new AppInfo(
                packageFullName,
                name,
                version,
                "N/A",
                false,
                publisher,
                "Microsoft Store / Appx",
                "",
                iconPath);
        }
    }

    private static bool IsFrameworkPackage(string name)
        => name.StartsWith("Microsoft.NET.", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Microsoft.VCLibs", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Microsoft.UI.Xaml", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Microsoft.WindowsAppRuntime", StringComparison.OrdinalIgnoreCase)
        || name.StartsWith("Microsoft.Services.Store.Engagement", StringComparison.OrdinalIgnoreCase);

    private static async Task<string> RunWingetAsync(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("winget", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Impossible de lancer winget.");
        var outputTask = proc.StandardOutput.ReadToEndAsync(ct);
        var errorTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var output = await outputTask;
        var error = await errorTask;

        if (proc.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            throw new InvalidOperationException($"winget a echoue (code {proc.ExitCode}) : {error.Trim()}");

        return output;
    }

    private static async Task<string> RunUninstallStringAsync(string uninstallString, CancellationToken ct)
    {
        var psi = new ProcessStartInfo("cmd.exe", $"/c {uninstallString}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Impossible de lancer la desinstallation.");
        var outputTask = proc.StandardOutput.ReadToEndAsync(ct);
        var errorTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var output = await outputTask;
        var error = await errorTask;

        return proc.ExitCode == 0 || string.IsNullOrWhiteSpace(error)
            ? output
            : $"Erreur : {error.Trim()}";
    }

    private static async Task<string> RunPowerShellAsync(string command, CancellationToken ct)
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

        using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Impossible de lancer PowerShell.");
        var outputTask = proc.StandardOutput.ReadToEndAsync(ct);
        var errorTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var output = await outputTask;
        var error = await errorTask;

        if (proc.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            throw new InvalidOperationException(error.Trim());

        return output;
    }

    private static string ReadString(RegistryKey key, string valueName)
        => key.GetValue(valueName)?.ToString()?.Trim() ?? string.Empty;

    private static int ReadInt(RegistryKey key, string valueName)
        => int.TryParse(key.GetValue(valueName)?.ToString(), out var value) ? value : 0;

    private static string EmptyToNa(string value)
        => string.IsNullOrWhiteSpace(value) ? "N/A" : value;

    private static string BuildAppKey(AppInfo app)
        => $"{app.Name}|{app.InstalledVersion}|{app.Publisher}";

    private static string EscapePowerShell(string value)
        => value.Replace("'", "''");

    private static string FindAppxIcon(string packageRoot)
    {
        try
        {
            var manifestPath = Path.Combine(packageRoot, "AppxManifest.xml");
            if (!File.Exists(manifestPath))
                return FindLargestImage(packageRoot);

            var doc = XDocument.Load(manifestPath);
            var logo = doc
                .Descendants()
                .SelectMany(element => element.Attributes())
                .Where(attribute => attribute.Name.LocalName is "Square44x44Logo" or "Square150x150Logo" or "Logo")
                .Select(attribute => attribute.Value)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

            if (string.IsNullOrWhiteSpace(logo))
                return FindLargestImage(packageRoot);

            var directPath = Path.Combine(packageRoot, logo.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(directPath))
                return directPath;

            var assetDir = Path.GetDirectoryName(directPath);
            var assetName = Path.GetFileNameWithoutExtension(directPath);
            if (!string.IsNullOrWhiteSpace(assetDir) && Directory.Exists(assetDir))
            {
                var themed = Directory.EnumerateFiles(assetDir, $"{assetName}*.png", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(path => path.Contains("targetsize-256", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(path => path.Contains("targetsize-128", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(path => path.Contains("targetsize-64", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(path => path.Contains("targetsize-48", StringComparison.OrdinalIgnoreCase))
                    .ThenBy(path => path.Contains("scale-", StringComparison.OrdinalIgnoreCase))
                    .ThenByDescending(path => new FileInfo(path).Length)
                    .FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(themed))
                    return themed;
            }

            return FindLargestImage(packageRoot);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string FindLargestImage(string root)
    {
        try
        {
            if (!Directory.Exists(root))
                return string.Empty;

            return Directory.EnumerateFiles(root, "*.png", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path).Contains("logo", StringComparison.OrdinalIgnoreCase))
                .Select(path => new FileInfo(path))
                .Where(file => file.Exists)
                .OrderByDescending(file => file.Length)
                .FirstOrDefault()
                ?.FullName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string FindIconCandidate(string appName, string installLocation, string uninstallString)
    {
        var fromUninstall = ExtractExecutablePath(uninstallString);
        if (!string.IsNullOrWhiteSpace(fromUninstall))
            return fromUninstall;

        if (string.IsNullOrWhiteSpace(installLocation) || !Directory.Exists(installLocation))
            return string.Empty;

        try
        {
            var appWords = appName
                .Split([' ', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word.Length > 1)
                .ToArray();

            var exes = Directory.EnumerateFiles(installLocation, "*.exe", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .Where(file => file.Exists)
                .OrderByDescending(file => appWords.Any(word => file.Name.Contains(word, StringComparison.OrdinalIgnoreCase)))
                .ThenBy(file => file.Name.Contains("unins", StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(file => file.Length)
                .ToList();

            return exes.FirstOrDefault()?.FullName ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtractExecutablePath(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return string.Empty;

        var text = Environment.ExpandEnvironmentVariables(command.Trim());
        var quotedMatch = System.Text.RegularExpressions.Regex.Match(text, "\"([^\"]+\\.exe)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (quotedMatch.Success && File.Exists(quotedMatch.Groups[1].Value))
            return quotedMatch.Groups[1].Value;

        var exeIndex = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex < 0)
            return string.Empty;

        var candidate = text[..(exeIndex + 4)].Trim().Trim('"');
        return File.Exists(candidate) ? candidate : string.Empty;
    }
}
