using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace IkoboostWpf.Models;

public partial class SensorItem : ObservableObject
{
    [ObservableProperty] private string _category = "";
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _reference = "";
    [ObservableProperty] private string _value = "";
    [ObservableProperty] private string _level = "Normal";
    [ObservableProperty] private bool _hasProductInfo;
    [ObservableProperty] private string _productUrl = "";
    [ObservableProperty] private double _valuePercent;
    [ObservableProperty] private Brush _accentBrush = Brushes.Gray;
    [ObservableProperty] private Brush _levelBrush = Brushes.Gray;
    [ObservableProperty] private Brush _levelBackground = Brushes.Transparent;
}
