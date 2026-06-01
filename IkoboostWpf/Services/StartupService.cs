using Microsoft.Win32;
using System.IO;

namespace IkoboostWpf.Services;

public sealed class StartupService
{
    private const string DisabledRoot = @"Software\Ikoboost\DisabledStartup";

    public sealed record StartupEntry(
        string Id,
        string Name,
        string Command,
        string Source,
        string Location,
        bool IsEnabled,
        bool CanToggle);

    private sealed record StartupLocation(
        RegistryHive Hive,
        RegistryView View,
        string KeyPath,
        string Source,
        string LocationId,
        bool Writable);

    private static readonly StartupLocation[] RegistryLocations =
    [
        new(RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run", "Registre utilisateur", "HKCU64", true),
        new(RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run", "Registre machine", "HKLM64", true),
        new(RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\Run", "Registre machine 32-bit", "HKLM32", true)
    ];

    public Task<IReadOnlyList<StartupEntry>> GetEntriesAsync(CancellationToken ct = default)
        => Task.Run(() =>
        {
            var entries = new List<StartupEntry>();
            foreach (var location in RegistryLocations)
            {
                ct.ThrowIfCancellationRequested();
                ReadEnabledRegistryEntries(location, entries);
                ReadDisabledRegistryEntries(location, entries);
            }

            ReadStartupFolderEntries(
                Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                "Dossier démarrage utilisateur",
                "StartupUser",
                entries);

            ReadStartupFolderEntries(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                "Dossier démarrage machine",
                "StartupCommon",
                entries);

            IReadOnlyList<StartupEntry> result = entries
                .GroupBy(e => e.Id, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .OrderByDescending(e => e.IsEnabled)
                .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            return result;
        }, ct);

    public Task SetEnabledAsync(StartupEntry entry, bool enabled, CancellationToken ct = default)
        => Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            if (!entry.CanToggle)
                throw new InvalidOperationException("Cette entrée ne peut pas être modifiée automatiquement.");

            if (entry.Location.StartsWith("Startup", StringComparison.OrdinalIgnoreCase))
            {
                SetStartupFolderEntryEnabled(entry, enabled);
                return;
            }

            var location = RegistryLocations.FirstOrDefault(l => l.LocationId.Equals(entry.Location, StringComparison.OrdinalIgnoreCase));
            if (location == null)
                throw new InvalidOperationException("Source de démarrage inconnue.");

            SetRegistryEntryEnabled(location, entry.Name, enabled);
        }, ct);

    private static void ReadEnabledRegistryEntries(StartupLocation location, ICollection<StartupEntry> entries)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(location.Hive, location.View);
            using var key = baseKey.OpenSubKey(location.KeyPath, false);
            if (key == null)
                return;

            foreach (var valueName in key.GetValueNames())
            {
                var command = Convert.ToString(key.GetValue(valueName)) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(command))
                    continue;

                entries.Add(new StartupEntry(
                    BuildId(location.LocationId, valueName, true),
                    valueName,
                    command,
                    location.Source,
                    location.LocationId,
                    true,
                    location.Writable));
            }
        }
        catch
        {
            // Some registry views can be blocked depending on elevation.
        }
    }

    private static void ReadDisabledRegistryEntries(StartupLocation location, ICollection<StartupEntry> entries)
    {
        try
        {
            using var disabledRoot = Registry.CurrentUser.OpenSubKey($@"{DisabledRoot}\{location.LocationId}", false);
            if (disabledRoot == null)
                return;

            foreach (var valueName in disabledRoot.GetValueNames())
            {
                var command = Convert.ToString(disabledRoot.GetValue(valueName)) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(command))
                    continue;

                entries.Add(new StartupEntry(
                    BuildId(location.LocationId, valueName, false),
                    valueName,
                    command,
                    $"{location.Source} (désactivé)",
                    location.LocationId,
                    false,
                    true));
            }
        }
        catch
        {
            // Keep the UI usable even if the disabled cache is unreadable.
        }
    }

    private static void SetRegistryEntryEnabled(StartupLocation location, string name, bool enabled)
    {
        using var baseKey = RegistryKey.OpenBaseKey(location.Hive, location.View);
        using var runKey = baseKey.OpenSubKey(location.KeyPath, true)
            ?? throw new InvalidOperationException("La clé de démarrage Windows est inaccessible.");

        using var disabledKey = Registry.CurrentUser.CreateSubKey($@"{DisabledRoot}\{location.LocationId}", true)
            ?? throw new InvalidOperationException("Impossible d'ouvrir la zone de désactivation IkoBoost.");

        if (enabled)
        {
            var command = Convert.ToString(disabledKey.GetValue(name));
            if (string.IsNullOrWhiteSpace(command))
                throw new InvalidOperationException("Commande de démarrage introuvable pour la réactivation.");

            runKey.SetValue(name, command, RegistryValueKind.String);
            disabledKey.DeleteValue(name, false);
        }
        else
        {
            var command = Convert.ToString(runKey.GetValue(name));
            if (string.IsNullOrWhiteSpace(command))
                throw new InvalidOperationException("Commande de démarrage introuvable pour la désactivation.");

            disabledKey.SetValue(name, command, RegistryValueKind.String);
            runKey.DeleteValue(name, false);
        }
    }

    private static void ReadStartupFolderEntries(string folder, string source, string locationId, ICollection<StartupEntry> entries)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return;

        try
        {
            foreach (var path in Directory.EnumerateFiles(folder))
            {
                var fileName = Path.GetFileName(path);
                var isDisabled = fileName.EndsWith(".ikoboost-disabled", StringComparison.OrdinalIgnoreCase);
                var displayName = isDisabled
                    ? fileName[..^".ikoboost-disabled".Length]
                    : fileName;

                entries.Add(new StartupEntry(
                    BuildId(locationId, displayName, !isDisabled),
                    displayName,
                    path,
                    isDisabled ? $"{source} (désactivé)" : source,
                    locationId,
                    !isDisabled,
                    true));
            }
        }
        catch
        {
            // Folder access can fail for common startup without elevation.
        }
    }

    private static void SetStartupFolderEntryEnabled(StartupEntry entry, bool enabled)
    {
        var currentPath = entry.Command;
        var enabledPath = enabled && currentPath.EndsWith(".ikoboost-disabled", StringComparison.OrdinalIgnoreCase)
            ? currentPath[..^".ikoboost-disabled".Length]
            : currentPath;

        var disabledPath = enabledPath + ".ikoboost-disabled";

        if (enabled)
        {
            if (!File.Exists(currentPath))
                throw new FileNotFoundException("Raccourci de démarrage introuvable.", currentPath);

            File.Move(currentPath, enabledPath, true);
        }
        else
        {
            if (!File.Exists(enabledPath))
                throw new FileNotFoundException("Raccourci de démarrage introuvable.", enabledPath);

            File.Move(enabledPath, disabledPath, true);
        }
    }

    private static string BuildId(string location, string name, bool enabled)
        => $"{location}|{enabled}|{name}";
}
