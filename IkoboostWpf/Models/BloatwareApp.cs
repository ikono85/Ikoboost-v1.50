using CommunityToolkit.Mvvm.ComponentModel;

namespace IkoboostWpf.Models;

public partial class BloatwareApp : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public string DisplayName { get; set; } = "";
    public string PackageName { get; set; } = "";
    public string Description { get; set; } = "";
    public bool Recommended { get; set; }
}
