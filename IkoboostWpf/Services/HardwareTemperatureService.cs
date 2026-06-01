using LibreHardwareMonitor.Hardware;
using System.Management;

namespace IkoboostWpf.Services;

public sealed class HardwareTemperatureService : IDisposable
{
    public record SensorReading(string Id, string Name, string Type, float ValueCelsius, string Unit, DateTime Timestamp);

    private readonly Computer _computer;
    private readonly SemaphoreSlim _readLock = new(1, 1);
    private IReadOnlyList<SensorReading> _lastReadings = [];
    private bool _disposed;

    public HardwareTemperatureService()
    {
        _computer = new Computer
        {
            IsCpuEnabled = true,
            IsGpuEnabled = true,
            IsMemoryEnabled = true,
            IsMotherboardEnabled = true,
            IsControllerEnabled = true,
            IsStorageEnabled = true
        };

        try { _computer.Open(); }
        catch { }
    }

    public async Task<IReadOnlyList<SensorReading>> GetTemperaturesAsync(CancellationToken ct = default)
    {
        await _readLock.WaitAsync(ct);
        try
        {
            return await Task.Run(() =>
            {
                var readings = new List<SensorReading>();

                try
                {
                    RefreshHardwareTree();
                    ReadComputer(readings, ct);

                    if (readings.Count == 0)
                    {
                        Thread.Sleep(250);
                        RefreshHardwareTree();
                        ReadComputer(readings, ct);
                    }

                    if (readings.Count > 0)
                    {
                        _lastReadings = readings;
                        return (IReadOnlyList<SensorReading>)readings;
                    }
                }
                catch
                {
                    readings.Clear();
                }

                readings.AddRange(ReadFallbackTemperatures());
                if (readings.Count > 0)
                    _lastReadings = readings;

                return readings.Count > 0 ? readings : _lastReadings;
            }, ct);
        }
        finally
        {
            _readLock.Release();
        }
    }

    private void RefreshHardwareTree()
    {
        _computer.Accept(new UpdateVisitor());
    }

    private void ReadComputer(ICollection<SensorReading> readings, CancellationToken ct)
    {
        foreach (var hardware in _computer.Hardware)
            ReadHardware(hardware, readings, ct, null);
    }

    private static void ReadHardware(
        IHardware hardware,
        ICollection<SensorReading> readings,
        CancellationToken ct,
        string? inheritedHardwareType)
    {
        ct.ThrowIfCancellationRequested();

        var hardwareType = MapHardwareType(hardware.HardwareType, hardware.Name) ?? inheritedHardwareType;
        var index = 0;
        foreach (var sensor in hardware.Sensors)
        {
            ct.ThrowIfCancellationRequested();
            var value = sensor.Value;
            if (sensor.SensorType != SensorType.Temperature
                || !value.HasValue
                || value.Value <= 0
                || value.Value >= 150)
                continue;

            var sensorType = hardwareType ?? GuessType($"{hardware.Name} {sensor.Name}");
            var idPrefix = sensorType.Equals("GPU", StringComparison.OrdinalIgnoreCase)
                ? "gpu-temp"
                : sensorType.Equals("CPU", StringComparison.OrdinalIgnoreCase)
                    ? "cpu-temp"
                    : "sensor-temp";

            readings.Add(new SensorReading(
                $"{idPrefix}-{index}",
                sensor.Name,
                sensorType,
                value.Value,
                "C",
                DateTime.Now));
            index++;
        }

        foreach (var subHardware in hardware.SubHardware)
            ReadHardware(subHardware, readings, ct, hardwareType);
    }

    private static string? MapHardwareType(HardwareType type, string name)
    {
        return type switch
        {
            HardwareType.Cpu => "CPU",
            HardwareType.GpuAmd or HardwareType.GpuIntel or HardwareType.GpuNvidia => "GPU",
            HardwareType.Memory => "Memory",
            _ when name.Contains("cpu", StringComparison.OrdinalIgnoreCase) => "CPU",
            _ when name.Contains("gpu", StringComparison.OrdinalIgnoreCase) => "GPU",
            _ => null
        };
    }

    private static List<SensorReading> ReadFallbackTemperatures()
    {
        var readings = new List<SensorReading>();
        readings.AddRange(ReadOhmWmi());
        readings.AddRange(ReadAcpiThermalZones());
        readings.AddRange(ReadWin32TemperatureProbe());
        return readings;
    }

    private static List<SensorReading> ReadOhmWmi()
    {
        var readings = new List<SensorReading>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\OpenHardwareMonitor",
                "SELECT Name, SensorType, Value FROM Sensor WHERE SensorType='Temperature'");

            var index = 0;
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "Inconnu";
                var val = Convert.ToSingle(obj["Value"]);
                if (val is > 0 and < 150)
                    readings.Add(new SensorReading($"ohm-temp-{index++}", name, GuessType(name), val, "C", DateTime.Now));
            }
        }
        catch { }

        return readings;
    }

    private static List<SensorReading> ReadAcpiThermalZones()
    {
        var readings = new List<SensorReading>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                @"root\WMI",
                "SELECT InstanceName, CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");

            var index = 0;
            foreach (ManagementObject obj in searcher.Get())
            {
                var rawValue = Convert.ToDouble(obj["CurrentTemperature"]);
                var celsius = (float)((rawValue / 10.0) - 273.15);
                var name = obj["InstanceName"]?.ToString() ?? $"Zone thermique {index}";
                if (celsius is > 0 and < 150)
                    readings.Add(new SensorReading($"acpi-temp-{index}", name, "CPU", celsius, "C", DateTime.Now));

                index++;
            }
        }
        catch { }

        return readings;
    }

    private static List<SensorReading> ReadWin32TemperatureProbe()
    {
        var readings = new List<SensorReading>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, CurrentReading FROM Win32_TemperatureProbe");

            var index = 0;
            foreach (ManagementObject obj in searcher.Get())
            {
                var raw = obj["CurrentReading"];
                if (raw == null)
                    continue;

                var celsius = Convert.ToSingle(raw);
                var name = obj["Name"]?.ToString() ?? "Sonde";
                if (celsius is > 0 and < 150)
                    readings.Add(new SensorReading($"probe-temp-{index++}", name, GuessType(name), celsius, "C", DateTime.Now));
            }
        }
        catch { }

        return readings;
    }

    private static string GuessType(string name)
    {
        if (name.Contains("gpu", StringComparison.OrdinalIgnoreCase))
            return "GPU";

        if (name.Contains("memory", StringComparison.OrdinalIgnoreCase)
            || name.Contains("ram", StringComparison.OrdinalIgnoreCase))
            return "Memory";

        return "CPU";
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        try { _computer.Close(); }
        catch { }
        _readLock.Dispose();
    }

    private sealed class UpdateVisitor : IVisitor
    {
        public void VisitComputer(IComputer computer)
        {
            computer.Traverse(this);
        }

        public void VisitHardware(IHardware hardware)
        {
            hardware.Update();
            foreach (var subHardware in hardware.SubHardware)
                subHardware.Accept(this);
        }

        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
}
