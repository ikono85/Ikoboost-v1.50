using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;
using System.Windows.Media;

namespace IkoboostWpf.Models;

public partial class StartupItem : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _source = "";
    [ObservableProperty] private string _command = "";
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _canToggle = true;
    [ObservableProperty] private string _impact = "Faible";
    [ObservableProperty] private Brush _impactBrush = Brushes.LimeGreen;
    [ObservableProperty] private Brush _impactBackground = Brushes.Transparent;

    public RegistryKey? RegKey { get; set; }
    public string RegName { get; set; } = "";
    public bool IsUserKey { get; set; }
    public RegistryValueKind ValueKind { get; set; } = RegistryValueKind.String;
}
