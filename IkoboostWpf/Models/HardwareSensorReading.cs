namespace IkoboostWpf.Models;

public sealed class HardwareSensorReading
{
    public string Id { get; init; } = "";
    public string Type { get; init; } = "";
    public string Name { get; init; } = "";
    public string HardwareName { get; init; } = "";
    public string HardwareType { get; init; } = "";
    public float Value { get; init; }
    public string Unit { get; init; } = "";
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
