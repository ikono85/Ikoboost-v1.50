using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IkoboostWpf.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Web;
using System.Windows.Threading;

namespace IkoboostWpf.ViewModels;

public sealed partial class HardwareViewModel : BaseViewModel
{
    private readonly HardwareTemperatureService _tempService;
    private readonly SystemInfoService _sysInfo;
    private readonly DispatcherTimer _timer;
    private CancellationTokenSource _cts = new();
    private bool _initialized;
    private bool _isRefreshing;

    [ObservableProperty] private bool _isLoading = true;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ObservableCollection<SensorRow> Sensors { get; } = [];

    public HardwareViewModel(HardwareTemperatureService tempService, SystemInfoService sysInfo)
    {
        _tempService = tempService;
        _sysInfo = sysInfo;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) =>
        {
            if (!IsDisposed)
                await RefreshAsync();
        };
    }

    public async Task InitializeAsync()
    {
        if (IsDisposed) return;

        if (!_initialized)
        {
            _initialized = true;
            await RefreshAsync();
            IsLoading = false;
        }

        if (!IsDisposed)
            _timer.Start();
    }

    public void Pause()
    {
        if (!IsDisposed)
            _timer.Stop();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsDisposed) return;
        if (_isRefreshing) return;

        CancellationToken ct;
        try { ct = _cts.Token; }
        catch (ObjectDisposedException) { return; }

        _isRefreshing = true;
        try
        {
            var readings = await _tempService.GetTemperaturesAsync(ct);
            var components = await _sysInfo.GetHardwareComponentsAsync(ct);
            var cpuDetails = await _sysInfo.GetCpuDetailsAsync(ct);
            var cpuName = await _sysInfo.GetCpuNameAsync(ct);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Sensors.Clear();

                foreach (var component in components)
                    Sensors.Add(new SensorRow(component.Category, component.Name, component.Reference, component.Value, "Info"));

                var cpu = _sysInfo.GetCpuUsagePercent();
                Sensors.Add(new SensorRow("CPU", "Utilisation", "Temps reel", cpu >= 0 ? $"{cpu:F1} %" : "N/A", GetCpuLevel(cpu)));
                Sensors.Add(new SensorRow("CPU", "Frequence actuelle", "WMI", cpuDetails.CurrentClockMhz > 0 ? $"{cpuDetails.CurrentClockMhz} MHz" : "N/A", "Info", cpuName));
                Sensors.Add(new SensorRow("CPU", "Frequence max", "WMI", cpuDetails.MaxClockMhz > 0 ? $"{cpuDetails.MaxClockMhz} MHz" : "N/A", "Info", cpuName));

                var (used, total) = _sysInfo.GetRamUsage();
                Sensors.Add(new SensorRow("RAM", "Utilisee", "Temps reel", used >= 0 ? $"{used} Mo / {total} Mo" : "N/A", "Normal"));

                if (readings.Count == 0)
                {
                    Sensors.Add(new SensorRow("Temperatures", "Etat", "Capteurs", "Aucun capteur disponible", "Info"));
                    StatusMessage = "Aucun capteur de temperature detecte par LibreHardwareMonitor. Lancez Ikoboost en administrateur et verifiez que votre materiel expose les sondes.";
                }
                else
                {
                    StatusMessage = string.Empty;
                    foreach (var r in readings.Where(r => r.ValueCelsius is > 0 and < 150))
                        Sensors.Add(new SensorRow(r.Type, r.Name, "Capteur", $"{r.ValueCelsius:F1} C", GetTempLevel(r.ValueCelsius)));

                    foreach (var r in readings.Where(r => r.ValueCelsius is > 0 and < 150)
                                              .Where(r => r.Type.Equals("CPU", StringComparison.OrdinalIgnoreCase) || IsCpuTemperatureName(r.Name)))
                        Sensors.Add(new SensorRow("CPU", $"Temperature {r.Name}", "Capteur", $"{r.ValueCelsius:F1} C", GetTempLevel(r.ValueCelsius)));
                }
            });
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur : {ex.Message}";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private static string GetCpuLevel(float v) => v switch
    {
        > 90 => "Critical",
        > 70 => "Warning",
        _ => "Normal"
    };

    private static string GetTempLevel(float v) => v switch
    {
        > 90 => "Critical",
        > 75 => "Warning",
        _ => "Normal"
    };

    private static bool IsCpuTemperatureName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        return name.Contains("cpu", StringComparison.OrdinalIgnoreCase)
            || name.Contains("core", StringComparison.OrdinalIgnoreCase)
            || name.Contains("package", StringComparison.OrdinalIgnoreCase)
            || name.Contains("tctl", StringComparison.OrdinalIgnoreCase)
            || name.Contains("tdie", StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void OpenProductPage(SensorRow? row)
    {
        if (row == null || row.Level != "Info")
            return;

        var query = BuildProductQuery(row);
        if (string.IsNullOrWhiteSpace(query) || query == "N/A")
            return;

        var url = $"https://www.google.com/search?q={HttpUtility.UrlEncode(query)}";
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static string BuildProductQuery(SensorRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.ProductQuery))
            return $"{row.ProductQuery} product page specs".Trim();

        var name = row.Name;
        var value = row.Value is not ("N/A" or "") ? row.Value : string.Empty;

        if (row.Category is "CPU" or "GPU" or "Carte mere" or "RAM" or "Disque" or "BIOS")
            return $"{name} {value} product page specs".Trim();

        return $"{row.Category} {name} {value} product page specs".Trim();
    }

    public override void Dispose()
    {
        if (IsDisposed) return;
        base.Dispose();
        _timer.Stop();
        _cts.Cancel();
    }
}

public sealed record SensorRow(string Category, string Name, string Reference, string Value, string Level, string? ProductQuery = null)
{
    public bool HasProductInfo => Level == "Info";
}
