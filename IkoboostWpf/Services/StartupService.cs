using System.Collections.Generic;
using System.IO;
using Microsoft.Win32;
using IkoboostWpf.Models;

namespace IkoboostWpf.Services;

public sealed class StartupService
{
    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string DisabledRunKey = RunKey + @"\Disabled";
    private const string StartupApprovedRunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    private const string StartupApprovedFolderKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    public List<StartupItem> GetStartupItems()
    {
        var list = new List<StartupItem>();
        AddItems(list, Registry.CurrentUser, RunKey, "HKCU\\Run", isUserKey: true, isEnabled: true);
        AddItems(list, Registry.CurrentUser, DisabledRunKey, "HKCU\\Run\\Disabled", isUserKey: true, isEnabled: false);
        AddItems(list, Registry.LocalMachine, RunKey, "HKLM\\Run", isUserKey: false, isEnabled: true);
        AddItems(list, Registry.LocalMachine, DisabledRunKey, "HKLM\\Run\\Disabled", isUserKey: false, isEnabled: false);
        AddStartupFolderItems(list);
        AddStartupApprovedItems(list, Registry.CurrentUser, StartupApprovedRunKey, "HKCU\\StartupApproved\\Run", isUserKey: true);
        AddStartupApprovedItems(list, Registry.LocalMachine, StartupApprovedRunKey, "HKLM\\StartupApproved\\Run", isUserKey: false);
        AddStartupApprovedItems(list, Registry.CurrentUser, StartupApprovedFolderKey, "HKCU\\StartupApproved\\StartupFolder", isUserKey: true);
        AddStartupApprovedItems(list, Registry.LocalMachine, StartupApprovedFolderKey, "HKLM\\StartupApproved\\StartupFolder", isUserKey: false);

        return list
            .GroupBy(DedupeKey, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(i => i.IsEnabled)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string ToggleItem(StartupItem item)
    {
        if (string.IsNullOrWhiteSpace(item.RegName))
            return "Element de demarrage invalide.";

        try
        {
            if (item.IsEnabled)
                return DisableItem(item);

            return EnableItem(item);
        }
        catch (UnauthorizedAccessException)
        {
            return "Droits insuffisants. Relancez Ikoboost en administrateur pour modifier cet element.";
        }
        catch (Exception ex)
        {
            return $"Erreur demarrage: {ex.Message}";
        }
    }

    private static void AddStartupFolderItems(List<StartupItem> list)
    {
        AddStartupFolderItems(list, Environment.GetFolderPath(Environment.SpecialFolder.Startup), "Dossier de demarrage");
        AddStartupFolderItems(list, Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), "Dossier de demarrage (Tous)");
    }

    private static void AddStartupFolderItems(List<StartupItem> list, string folder, string source)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return;

        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                var extension = Path.GetExtension(file);
                if (!IsStartupShortcut(extension))
                    continue;

                var name = Path.GetFileNameWithoutExtension(file);
                list.Add(new StartupItem
                {
                    Name = name,
                    Command = file,
                    Source = source,
                    IsEnabled = true,
                    CanToggle = false,
                    RegName = Path.GetFileName(file)
                });
            }
        }
        catch
        {
            AppLog.Warning("StartupService", $"Dossier inaccessible: {folder}");
        }
    }

    private static void AddStartupApprovedItems(List<StartupItem> list, RegistryKey root, string path, string source, bool isUserKey)
    {
        try
        {
            using var key = root.OpenSubKey(path, writable: false);
            if (key == null)
                return;

            foreach (var name in key.GetValueNames())
            {
                if (list.Any(i => SameStartupName(i, name)))
                    continue;

                var bytes = key.GetValue(name) as byte[];
                var disabled = bytes is { Length: > 0 } && bytes[0] == 0x03;
                list.Add(new StartupItem
                {
                    Name = Path.GetFileNameWithoutExtension(name),
                    Command = "",
                    Source = source,
                    IsEnabled = !disabled,
                    CanToggle = false,
                    RegName = name,
                    IsUserKey = isUserKey
                });
            }
        }
        catch
        {
            AppLog.Warning("StartupService", $"Cle inaccessible: {source}");
        }
    }

    private static bool IsStartupShortcut(string extension) =>
        extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".url", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".exe", StringComparison.OrdinalIgnoreCase);

    private static string DedupeKey(StartupItem item)
    {
        var name = string.IsNullOrWhiteSpace(item.RegName) ? item.Name : item.RegName;
        return Path.GetFileNameWithoutExtension(name).Trim();
    }

    private static bool SameStartupName(StartupItem item, string name)
    {
        var left = DedupeKey(item);
        var right = Path.GetFileNameWithoutExtension(name).Trim();
        return left.Equals(right, StringComparison.OrdinalIgnoreCase);
    }

    public string SetItemEnabled(StartupItem item, bool shouldBeEnabled)
    {
        if (string.IsNullOrWhiteSpace(item.RegName))
            return "Element de demarrage invalide.";

        try
        {
            if (shouldBeEnabled)
                return EnableItem(item);

            return DisableItem(item);
        }
        catch (UnauthorizedAccessException)
        {
            return "Droits insuffisants. Relancez Ikoboost en administrateur pour modifier cet element.";
        }
        catch (Exception ex)
        {
            return $"Erreur demarrage: {ex.Message}";
        }
    }

    private static void AddItems(List<StartupItem> list, RegistryKey root, string path, string source, bool isUserKey, bool isEnabled)
    {
        try
        {
            using var key = root.OpenSubKey(path, writable: false);
            if (key == null) return;

            foreach (var name in key.GetValueNames())
            {
                var command = key.GetValue(name)?.ToString() ?? "";
                if (string.IsNullOrWhiteSpace(command)) continue;

                list.Add(new StartupItem
                {
                    Name = name,
                    Command = command,
                    Source = source,
                    IsEnabled = isEnabled,
                    CanToggle = true,
                    RegName = name,
                    IsUserKey = isUserKey,
                    ValueKind = SafeValueKind(key, name),
                });
            }
        }
        catch
        {
            AppLog.Warning("StartupService", $"Cle inaccessible: {source}");
        }
    }

    private static string DisableItem(StartupItem item)
    {
        var root = item.IsUserKey ? Registry.CurrentUser : Registry.LocalMachine;
        using var run = root.OpenSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("Cle Run introuvable.");
        using var disabled = root.CreateSubKey(DisabledRunKey, writable: true)
            ?? throw new InvalidOperationException("Impossible de creer la cle Disabled.");

        var value = run.GetValue(item.RegName) ?? item.Command;
        var kind = SafeValueKind(run, item.RegName, item.ValueKind);
        disabled.SetValue(item.RegName, value, kind);
        run.DeleteValue(item.RegName, throwOnMissingValue: false);
        item.IsEnabled = false;
        return $"{item.Name} desactive au demarrage.";
    }

    private static string EnableItem(StartupItem item)
    {
        var root = item.IsUserKey ? Registry.CurrentUser : Registry.LocalMachine;
        using var run = root.OpenSubKey(RunKey, writable: true)
            ?? throw new InvalidOperationException("Cle Run introuvable.");
        using var disabled = root.OpenSubKey(DisabledRunKey, writable: true);

        var value = disabled?.GetValue(item.RegName) ?? item.Command;
        var kind = disabled != null ? SafeValueKind(disabled, item.RegName, item.ValueKind) : item.ValueKind;
        run.SetValue(item.RegName, value, kind);
        disabled?.DeleteValue(item.RegName, throwOnMissingValue: false);
        item.IsEnabled = true;
        return $"{item.Name} active au demarrage.";
    }

    private static RegistryValueKind SafeValueKind(RegistryKey key, string valueName, RegistryValueKind fallback = RegistryValueKind.String)
    {
        try { return key.GetValueKind(valueName); }
        catch { return fallback; }
    }
}
