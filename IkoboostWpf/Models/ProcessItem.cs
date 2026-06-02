using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace IkoboostWpf.Models;

public partial class ProcessItem : ObservableObject
{
    [ObservableProperty] private int _pid;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private double _cpuPercent;
    [ObservableProperty] private string _memoryMb = "";
    [ObservableProperty] private string _priority = "";
    [ObservableProperty] private string _priorityKey = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _publisher = "";
    [ObservableProperty] private Brush _cpuBrush = Brushes.Cyan;
}
