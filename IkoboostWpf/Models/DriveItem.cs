using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace IkoboostWpf.Models;

public partial class DriveItem : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _label = "";
    [ObservableProperty] private double _usagePercent;
    [ObservableProperty] private string _used = "";
    [ObservableProperty] private string _available = "";
    [ObservableProperty] private string _total = "";
    [ObservableProperty] private Brush _usageBrush = Brushes.Cyan;
}
