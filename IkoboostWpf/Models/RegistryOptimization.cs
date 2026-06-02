using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Win32;

namespace IkoboostWpf.Models;

public partial class RegistryOptimization : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public string DisplayName { get; set; } = "";
    public string Action { get; set; } = "";
    public string ValueName { get; set; } = "";
    public string Description { get; set; } = "";
    public string KeyPath { get; set; } = "";
    public object? NewValue { get; set; }
    public RegistryValueKind ValueKind { get; set; } = RegistryValueKind.DWord;
    public bool Recommended { get; set; }
}
