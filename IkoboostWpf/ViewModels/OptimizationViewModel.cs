using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IkoboostWpf.Services;
using IkoboostWpf.Views;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace IkoboostWpf.ViewModels;

public sealed partial class OptimizationViewModel : BaseViewModel
{
    private readonly OptimizationService _optimization;
    private readonly NetworkService _network;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _log = string.Empty;
    [ObservableProperty] private string _selectedDnsProfile = "Cloudflare";
    [ObservableProperty] private string _selectedPowerPlan = "Equilibre";
    [ObservableProperty] private long _ramBeforeCleanupMb;
    [ObservableProperty] private long _ramAfterCleanupMb;
    [ObservableProperty] private long _ramFreedMb;

    [ObservableProperty] private string _duplicateScanPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    [ObservableProperty] private string _duplicateScanStatus = string.Empty;
    [ObservableProperty] private bool _isDuplicateScanBusy;

    [ObservableProperty] private string _driverScanStatus = string.Empty;
    [ObservableProperty] private bool _isDriverScanBusy;

    public IReadOnlyDictionary<string, string> DnsProfiles => OptimizationService.DnsProfiles;
    public IReadOnlyDictionary<string, string> PowerPlans => OptimizationService.PowerPlans;
    public ObservableCollection<BloatwareAppOption> BloatwareApps { get; } = [];
    public ObservableCollection<RegistryOptimizationOption> RegistryOptimizations { get; } = [];
    public ObservableCollection<DuplicateGroupOption> DuplicateGroups { get; } = [];
    public ObservableCollection<DriverInfoItem> InstalledDrivers { get; } = [];

    public OptimizationViewModel(OptimizationService optimization, NetworkService network)
    {
        _optimization = optimization;
        _network = network;

        foreach (var app in OptimizationService.BloatwareApps)
            BloatwareApps.Add(new BloatwareAppOption(app));

        foreach (var registryOptimization in OptimizationService.RegistryOptimizations)
            RegistryOptimizations.Add(new RegistryOptimizationOption(registryOptimization));
    }

    [RelayCommand]
    private async Task CleanupAsync()
    {
        IsBusy = true;
        Log = "Nettoyage en cours…";
        try { Log = await _optimization.CleanupSystemAsync(); }
        catch (Exception ex) { Log = $"Erreur : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SetDnsAsync()
    {
        if (!DnsProfiles.TryGetValue(SelectedDnsProfile, out var dns)) return;
        var info = _network.GetActiveAdapterInfo();
        if (info == null) { Log = "Aucun adaptateur réseau actif détecté."; return; }

        IsBusy = true;
        Log = $"Application du DNS {SelectedDnsProfile} ({dns})…";
        try
        {
            var result = await _optimization.SetDnsAsync(info.Name, dns);
            Log = $"[OK] DNS appliqué : {result}";
        }
        catch (Exception ex) { Log = $"Erreur DNS : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task SetPowerPlanAsync()
    {
        if (!PowerPlans.TryGetValue(SelectedPowerPlan, out var guid)) return;

        IsBusy = true;
        Log = $"Application du profil d'alimentation : {SelectedPowerPlan}…";
        try
        {
            var result = await _optimization.SetPowerPlanAsync(guid);
            Log = $"[OK] Profil appliqué. {result}";
        }
        catch (Exception ex) { Log = $"Erreur profil : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CreateRestorePointAsync()
    {
        var confirm = MessageBox.Show(
            "Créer un point de restauration Windows maintenant ?\n\nCette action peut demander les droits administrateur et nécessite que la protection du système soit activée.",
            "Point de restauration",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        IsBusy = true;
        Log = "Creation du point de restauration en cours...\n";
        try { Log += await _optimization.CreateRestorePointAsync(); }
        catch (Exception ex) { Log += $"\nErreur : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CleanRamAsync()
    {
        var confirm = MessageBox.Show(
            "Nettoyer la RAM maintenant ?\n\nCette action vide la memoire de travail des processus accessibles sans fermer les applications.",
            "Cleaner RAM",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
            return;

        IsBusy = true;
        Log = "Nettoyage RAM en cours...\n";
        try
        {
            var result = await _optimization.CleanRamAsync();
            RamBeforeCleanupMb = result.BeforeMb;
            RamAfterCleanupMb = result.AfterMb;
            RamFreedMb = Math.Max(0, result.BeforeMb - result.AfterMb);
            Log += $"[OK] Cleaner RAM termine.\n"
                + $"Processus optimises : {result.ProcessesOptimized}\n"
                + $"Processus ignores : {result.ProcessesSkipped}\n"
                + $"RAM avant : {RamBeforeCleanupMb:N0} Mo\n"
                + $"RAM apres : {RamAfterCleanupMb:N0} Mo\n"
                + $"RAM liberee estimee : {RamFreedMb:N0} Mo";
        }
        catch (Exception ex) { Log += $"\nErreur : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task OneClickMaintenanceAsync()
    {
        var confirm = MessageBox.Show(
            "Lancer la maintenance complète ?\n\nCela va :\n• Nettoyer les fichiers temporaires\n• Appliquer le profil d'alimentation sélectionné",
            "Maintenance en un clic", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        Log = "Maintenance en cours…\n";
        try
        {
            Log += await _optimization.CleanupSystemAsync();

            if (PowerPlans.TryGetValue(SelectedPowerPlan, out var guid))
                Log += "\n" + await _optimization.SetPowerPlanAsync(guid);

            Log += "\n[OK] Maintenance terminée.";
        }
        catch (Exception ex) { Log += $"\nErreur : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task RepairSystemAsync()
    {
        var confirm = MessageBox.Show(
            "Lancer la réparation des fichiers système ?\n\nSFC et DISM seront exécutés. Cette opération peut prendre plusieurs minutes.",
            "Réparation système",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        Log = "Réparation système en cours...\n";
        try { Log += await _optimization.RepairSystemAsync(); }
        catch (Exception ex) { Log += $"\nErreur : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task CleanDiskAsync()
    {
        var confirm = MessageBox.Show(
            "Lancer le nettoyage avancé des disques ?\n\nCela supprimera le cache local, INetCache, les miniatures et videra la corbeille.",
            "Nettoyage avancé",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        Log = "Nettoyage avancé en cours...\n";
        try { Log += await _optimization.CleanDiskAsync(); }
        catch (Exception ex) { Log += $"\nErreur : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ScanDuplicatesAsync()
    {
        var dialog = new OpenFolderDialog { Title = "Choisir un dossier à analyser" };
        if (dialog.ShowDialog() != true) return;
        DuplicateScanPath = dialog.FolderName;

        IsDuplicateScanBusy = true;
        DuplicateScanStatus = "Analyse en cours...";
        DuplicateGroups.Clear();
        try
        {
            var progress = new Progress<string>(msg => DuplicateScanStatus = msg);
            var groups = await _optimization.FindDuplicatesAsync(DuplicateScanPath, progress, CancellationToken.None);
            foreach (var g in groups)
                DuplicateGroups.Add(new DuplicateGroupOption(g));
            var totalMb = groups.Sum(g => g.SizeBytes * (g.Paths.Count - 1)) / 1024.0 / 1024.0;
            DuplicateScanStatus = $"{groups.Count} groupe(s) trouvé(s), {totalMb:N1} Mo récupérables.";
        }
        catch (Exception ex) { DuplicateScanStatus = $"Erreur : {ex.Message}"; }
        finally { IsDuplicateScanBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteSelectedDuplicatesAsync()
    {
        var toDelete = DuplicateGroups
            .SelectMany(g => g.Files)
            .Where(f => f.IsSelectedForDelete)
            .Select(f => f.Path)
            .ToList();

        if (toDelete.Count == 0)
        {
            Log = "Aucun fichier sélectionné pour la suppression.";
            return;
        }

        var items = toDelete.Select(p => $"• {p}");
        var confirmed = ConfirmationDialog.Show(
            title: "Supprimer les doublons",
            message: $"{toDelete.Count} fichier(s) sélectionné(s) seront supprimés définitivement :",
            confirmLabel: "Supprimer",
            isDanger: true,
            items: items);
        if (!confirmed) return;

        IsBusy = true;
        Log = "Suppression des doublons en cours...\n";
        try { Log += await _optimization.DeleteDuplicatesAsync(toDelete); }
        catch (Exception ex) { Log += $"\nErreur : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task LoadDriversAsync()
    {
        IsDriverScanBusy = true;
        InstalledDrivers.Clear();
        try
        {
            var drivers = await _optimization.GetInstalledDriversAsync();
            foreach (var d in drivers)
                InstalledDrivers.Add(new DriverInfoItem { DeviceName = d.DeviceName, DriverVersion = d.DriverVersion, DriverDate = d.DriverDate, ProviderName = d.ProviderName });
            DriverScanStatus = $"{InstalledDrivers.Count} pilotes installés.";
        }
        catch (Exception ex) { DriverScanStatus = $"Erreur : {ex.Message}"; }
        finally { IsDriverScanBusy = false; }
    }

    [RelayCommand]
    private async Task ScanDriverUpdatesAsync()
    {
        var confirm = MessageBox.Show(
            "Lancer la recherche de mises à jour des pilotes ?\n\nCela va déclencher un scan Windows Update et pnputil. Les mises à jour s'installeront via Windows Update.",
            "Mise à jour des pilotes",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        IsBusy = true;
        Log = "Recherche de mises à jour des pilotes en cours...\n";
        try
        {
            Log += await _optimization.ScanDriverUpdatesAsync();
            Log += await _optimization.OpenWindowsUpdateDriversAsync();
        }
        catch (Exception ex) { Log += $"\nErreur : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void SelectRecommendedBloatware()
    {
        foreach (var app in BloatwareApps)
            app.IsSelected = app.PackageName is not "Microsoft.XboxIdentityProvider";
    }

    [RelayCommand]
    private void ClearBloatwareSelection()
    {
        foreach (var app in BloatwareApps)
            app.IsSelected = false;
    }

    [RelayCommand]
    private async Task UninstallSelectedBloatwareAsync()
    {
        var selectedApps = BloatwareApps
            .Where(app => app.IsSelected)
            .ToList();

        if (selectedApps.Count == 0)
        {
            Log = "Sélectionne au moins une application à désinstaller.";
            return;
        }

        var items = selectedApps.Select(a => $"• {a.DisplayName}  ({a.PackageName})");
        var (confirmed, createRestore) = ConfirmationDialog.ShowWithRestoreOption(
            title: "Désinstaller les bloatwares",
            message: $"{selectedApps.Count} application(s) sélectionnée(s) seront supprimées pour l'utilisateur courant :",
            confirmLabel: "Désinstaller",
            items: items);
        if (!confirmed) return;

        IsBusy = true;
        Log = string.Empty;

        if (createRestore)
        {
            Log = "Création d'un point de restauration…\n";
            try { Log += await _optimization.CreateRestorePointAsync() + "\n"; }
            catch (Exception ex) { Log += $"Point de restauration : {ex.Message}\n"; }
        }

        Log += "Désinstallation des bloatwares en cours…\n";
        try
        {
            Log += await _optimization.UninstallBloatwareAsync(selectedApps.Select(app => app.ToServiceModel()));
        }
        catch (Exception ex) { Log += $"\nErreur : {ex.Message}"; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void SelectRecommendedRegistryOptimizations()
    {
        foreach (var optimization in RegistryOptimizations)
            optimization.IsSelected = optimization.ValueName is not "OneDrive";
    }

    [RelayCommand]
    private void ClearRegistryOptimizationSelection()
    {
        foreach (var optimization in RegistryOptimizations)
            optimization.IsSelected = false;
    }

    [RelayCommand]
    private async Task ApplySelectedRegistryOptimizationsAsync()
    {
        var selectedOptimizations = RegistryOptimizations
            .Where(optimization => optimization.IsSelected)
            .ToList();

        if (selectedOptimizations.Count == 0)
        {
            Log = "Sélectionne au moins une optimisation registre à appliquer.";
            return;
        }

        var items = selectedOptimizations.Select(o => $"• {o.DisplayName}  — {o.Action}");
        var confirmed = ConfirmationDialog.Show(
            title: "Modifier le registre",
            message: $"{selectedOptimizations.Count} optimisation(s) sélectionnée(s) seront appliquées. Un point de restauration est recommandé avant de continuer.",
            confirmLabel: "Appliquer",
            isDanger: false,    // avertissement (style primary), pas une action destructive irréversible
            items: items);
        if (!confirmed) return;

        IsBusy = true;
        Log = "Application des optimisations registre en cours…\n";
        try
        {
            Log += await _optimization.ApplyRegistryOptimizationsAsync(selectedOptimizations.Select(optimization => optimization.ToServiceModel()));
        }
        catch (Exception ex) { Log += $"\nErreur : {ex.Message}"; }
        finally { IsBusy = false; }
    }
}

public sealed partial class BloatwareAppOption : ObservableObject
{
    private readonly OptimizationService.BloatwareApp _app;

    [ObservableProperty] private bool _isSelected;

    public BloatwareAppOption(OptimizationService.BloatwareApp app)
    {
        _app = app;
    }

    public string DisplayName => _app.DisplayName;
    public string PackageName => _app.PackageName;
    public string Description => _app.Description;

    public OptimizationService.BloatwareApp ToServiceModel() => _app;
}

public sealed partial class RegistryOptimizationOption : ObservableObject
{
    private readonly OptimizationService.RegistryOptimization _optimization;

    [ObservableProperty] private bool _isSelected;

    public RegistryOptimizationOption(OptimizationService.RegistryOptimization optimization)
    {
        _optimization = optimization;
    }

    public string DisplayName => _optimization.DisplayName;
    public string Description => _optimization.Description;
    public string RegistryPath => $@"{_optimization.Hive}\{_optimization.KeyPath}";
    public string ValueName => _optimization.ValueName;
    public string Action => _optimization.DeleteValue
        ? "Supprimer"
        : $"Définir {_optimization.ValueData}";

    public OptimizationService.RegistryOptimization ToServiceModel() => _optimization;
}

public sealed partial class DuplicateFileOption : ObservableObject
{
    public string Path { get; init; } = string.Empty;
    [ObservableProperty] private bool _isSelectedForDelete;
}

public sealed class DuplicateGroupOption
{
    public string Hash { get; }
    public long SizeBytes { get; }
    public string SizeMb { get; }
    public IReadOnlyList<string> Paths { get; }
    public string Header { get; }
    public List<DuplicateFileOption> Files { get; }

    public DuplicateGroupOption(OptimizationService.DuplicateGroup group)
    {
        Hash = group.Hash;
        SizeBytes = group.SizeBytes;
        SizeMb = $"{group.SizeBytes / 1024.0 / 1024.0:N2} Mo";
        Paths = group.Paths;
        Header = $"({group.Paths.Count} copies) — {SizeMb} — {group.Hash[..Math.Min(8, group.Hash.Length)]}";
        Files = group.Paths.Select((p, i) => new DuplicateFileOption
        {
            Path = p,
            IsSelectedForDelete = i > 0
        }).ToList();
    }
}

public sealed class DriverInfoItem
{
    public string DeviceName { get; init; } = string.Empty;
    public string DriverVersion { get; init; } = string.Empty;
    public string DriverDate { get; init; } = string.Empty;
    public string ProviderName { get; init; } = string.Empty;
}
