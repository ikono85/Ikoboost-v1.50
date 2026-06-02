using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IkoboostWpf.Models;
using IkoboostWpf.Services;

namespace IkoboostWpf.ViewModels;

public partial class OptimizeViewModel : ObservableObject
{
    private readonly OptimizeService _service = new();

    [ObservableProperty] private string _selectedOptimizationTab = "Nettoyage";
    [ObservableProperty] private double _ramBeforeCleanupMb;
    [ObservableProperty] private double _ramAfterCleanupMb;
    [ObservableProperty] private double _ramFreedMb;
    [ObservableProperty] private string _selectedPowerPlan = "Équilibré";
    [ObservableProperty] private string _driverScanStatus = "";
    [ObservableProperty] private ObservableCollection<DriverInfo> _installedDrivers = [];
    [ObservableProperty] private string _duplicateScanPath = "";
    [ObservableProperty] private string _duplicateScanStatus = "";
    [ObservableProperty] private ObservableCollection<DuplicateGroup> _duplicateGroups = [];
    [ObservableProperty] private ObservableCollection<RegistryOptimization> _registryOptimizations;
    [ObservableProperty] private ObservableCollection<BloatwareApp> _bloatwareApps;
    [ObservableProperty] private string _log = "";

    public Dictionary<string, string> PowerPlans => OptimizeService.PowerPlans;

    public OptimizeViewModel()
    {
        RegistryOptimizations = new ObservableCollection<RegistryOptimization>(
            OptimizeService.GetRegistryOptimizations());
        BloatwareApps = new ObservableCollection<BloatwareApp>(
            OptimizeService.GetBloatwareList());
    }

    [RelayCommand]
    private async Task OneClickMaintenance()
    {
        AppendLog("=== Maintenance 1-clic ===");
        AppendLog(await _service.CleanTempFilesAsync(new Progress<string>(AppendLog)));
        var (before, after, freed) = _service.CleanRam();
        AppendLog($"✓ RAM : {freed} Mo libérés ({before} Mo → {after} Mo)");
        AppendLog("=== Terminé ===");
    }

    [RelayCommand]
    private async Task Cleanup()
    {
        AppendLog(await _service.CleanTempFilesAsync(new Progress<string>(AppendLog)));
    }

    [RelayCommand]
    private async Task CleanDisk()
    {
        AppendLog(await _service.CleanDiskAsync());
    }

    [RelayCommand]
    private void CleanRam()
    {
        var (before, after, freed) = _service.CleanRam();
        RamBeforeCleanupMb = before;
        RamAfterCleanupMb = after;
        RamFreedMb = freed;
        AppendLog($"✓ RAM nettoyée — {freed} Mo libérés.");
    }

    [RelayCommand]
    private async Task CreateRestorePoint()
    {
        AppendLog(await _service.CreateRestorePointAsync());
    }

    [RelayCommand]
    private async Task RepairSystem()
    {
        AppendLog("Réparation du système en cours...");
        AppendLog(await _service.RepairSystemAsync(new Progress<string>(AppendLog)));
    }

    [RelayCommand]
    private async Task SetPowerPlan()
    {
        AppendLog(await _service.SetPowerPlanAsync(SelectedPowerPlan));
    }

    [RelayCommand]
    private async Task LoadDrivers()
    {
        DriverScanStatus = "Chargement des pilotes...";
        InstalledDrivers = new ObservableCollection<DriverInfo>(await _service.GetDriversAsync());
        DriverScanStatus = $"{InstalledDrivers.Count} pilote(s) installés.";
    }

    [RelayCommand]
    private void ScanDriverUpdates()
    {
        DriverScanStatus = "Lancez Windows Update pour rechercher les mises à jour de pilotes.";
    }

    [RelayCommand]
    private async Task ScanDuplicates()
    {
        DuplicateScanStatus = "Scan en cours...";
        var groups = await _service.ScanDuplicatesAsync(DuplicateScanPath,
            new Progress<string>(s => DuplicateScanStatus = s));
        DuplicateGroups = new ObservableCollection<DuplicateGroup>(groups);
        DuplicateScanStatus = $"{groups.Count} groupe(s) de doublons trouvés.";
    }

    [RelayCommand]
    private void DeleteSelectedDuplicates()
    {
        int deleted = 0;
        int failed = 0;
        var errors = new List<string>();

        foreach (var grp in DuplicateGroups)
            foreach (var f in grp.Files.Where(f => f.IsSelectedForDelete))
            {
                try
                {
                    if (!System.IO.File.Exists(f.Path))
                    {
                        failed++;
                        errors.Add($"Introuvable: {f.Path}");
                        continue;
                    }

                    System.IO.File.Delete(f.Path);
                    deleted++;
                }
                catch (Exception ex)
                {
                    failed++;
                    errors.Add($"{f.Path}: {ex.Message}");
                    AppLog.Error("OptimizeViewModel.DeleteSelectedDuplicates", ex);
                }
            }

        AppendLog($"{deleted} doublon(s) supprime(s).");
        if (failed > 0)
        {
            AppendLog($"{failed} suppression(s) impossible(s):");
            foreach (var error in errors.Take(8))
                AppendLog($"- {error}");
            if (errors.Count > 8)
                AppendLog($"- ... {errors.Count - 8} autre(s) erreur(s)");
        }

        if (failed == 0)
            DuplicateGroups.Clear();
    }

    [RelayCommand]
    private void SelectRecommendedRegistryOptimizations()
    {
        foreach (var opt in RegistryOptimizations)
            opt.IsSelected = opt.Recommended;
    }

    [RelayCommand]
    private void ClearRegistryOptimizationSelection()
    {
        foreach (var opt in RegistryOptimizations) opt.IsSelected = false;
    }

    [RelayCommand]
    private async Task ApplySelectedRegistryOptimizations()
    {
        AppendLog(await _service.ApplyRegistryOptimizationsAsync(RegistryOptimizations));
    }

    [RelayCommand]
    private void SelectRecommendedBloatware()
    {
        foreach (var app in BloatwareApps) app.IsSelected = app.Recommended;
    }

    [RelayCommand]
    private void ClearBloatwareSelection()
    {
        foreach (var app in BloatwareApps) app.IsSelected = false;
    }

    [RelayCommand]
    private async Task UninstallSelectedBloatware()
    {
        AppendLog(await _service.UninstallBloatwareAsync(BloatwareApps,
            new Progress<string>(AppendLog)));
    }

    private void AppendLog(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        Log = Log.Length > 8000 ? line : Log + "\n" + line;
    }
}
