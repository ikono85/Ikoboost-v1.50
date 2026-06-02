using System.Collections.ObjectModel;

namespace IkoboostWpf.Models;

public sealed class HardwareSensorGroup
{
    public string Name { get; init; } = "";
    public ObservableCollection<SensorItem> Items { get; init; } = [];
}
