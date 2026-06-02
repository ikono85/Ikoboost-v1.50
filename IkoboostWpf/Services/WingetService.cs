using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using IkoboostWpf.Models;
using Microsoft.Win32;

namespace IkoboostWpf.Services;

public sealed class WingetService
{
    private const string UserUninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string MachineUninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
    private const string WowUninstallKey = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    public bool IsWingetAvailable()
    {
        try
        {
            var p = Process.Start(new ProcessStartInfo("winget", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p == null) return false;
            return p.WaitForExit(2000) && p.ExitCode == 0;
        }
        catch { return false; }
    }

    public Task<List<AppItem>> GetInstalledAppsAsync(IProgress<string>? progress = null)
    {
        progress?.Report("Chargement rapide depuis Windows...");
        return Task.Run(() =>
        {
            var apps = new List<AppItem>();
            AddRegistryApps(apps, Registry.CurrentUser, UserUninstallKey, "Utilisateur");
            AddRegistryApps(apps, Registry.LocalMachine, MachineUninstallKey, "Windows");
            AddRegistryApps(apps, Registry.LocalMachine, WowUninstallKey, "Windows x86");

            return apps
                .GroupBy(a => string.IsNullOrWhiteSpace(a.Id) ? a.Name : a.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        });
    }

    public async Task<string> UpgradeAppAsync(string appId)
    {
        try { return await RunWingetAsync($"upgrade --id \"{appId}\" --accept-package-agreements --accept-source-agreements"); }
        catch (Exception ex) { return $"Erreur: {ex.Message}"; }
    }

    public async Task<string> UpgradeAllAsync()
    {
        try { return await RunWingetAsync("upgrade --all --accept-package-agreements --accept-source-agreements"); }
        catch (Exception ex) { return $"Erreur: {ex.Message}"; }
    }

    public async Task<string> UninstallAppAsync(string appId)
    {
        try { return await RunWingetAsync($"uninstall --id \"{appId}\" --accept-source-agreements"); }
        catch (Exception ex) { return $"Erreur: {ex.Message}"; }
    }

    private static void AddRegistryApps(List<AppItem> apps, RegistryKey root, string path, string source)
    {
        try
        {
            using var key = root.OpenSubKey(path);
            if (key == null) return;

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using var appKey = key.OpenSubKey(subKeyName);
                if (appKey == null) continue;

                var name = appKey.GetValue("DisplayName")?.ToString();
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (appKey.GetValue("SystemComponent")?.ToString() == "1") continue;
                if (appKey.GetValue("ParentKeyName") != null) continue;

                var publisher = appKey.GetValue("Publisher")?.ToString() ?? "";
                var version = appKey.GetValue("DisplayVersion")?.ToString() ?? "";
                var icon = CleanIconPath(appKey.GetValue("DisplayIcon")?.ToString() ?? "");
                apps.Add(new AppItem
                {
                    Name = name.Trim(),
                    Id = subKeyName,
                    DisplayPublisher = publisher,
                    DisplayVersion = version,
                    SourceLabel = source,
                    IconPath = icon,
                });
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("WingetService.AddRegistryApps", ex);
        }
    }

    private static string CleanIconPath(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var text = raw.Trim().Trim('"');
        var commaIndex = text.LastIndexOf(',');
        if (commaIndex > 1 && int.TryParse(text[(commaIndex + 1)..], out _))
            text = text[..commaIndex].Trim().Trim('"');
        return Environment.ExpandEnvironmentVariables(text);
    }

    private static async Task<string> RunWingetAsync(string args)
    {
        var p = Process.Start(new ProcessStartInfo("winget", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("winget introuvable.");

        var outputTask = p.StandardOutput.ReadToEndAsync();
        var errorTask = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync();
        var output = await outputTask;
        var error = await errorTask;
        return string.IsNullOrWhiteSpace(output) ? error : output;
    }
}
