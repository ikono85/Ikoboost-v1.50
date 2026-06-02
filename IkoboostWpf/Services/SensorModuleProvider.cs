namespace IkoboostWpf.Services;

public static class SensorModuleProvider
{
    public static SensorModule Shared { get; } = new();
}
