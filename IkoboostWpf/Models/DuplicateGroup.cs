using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IkoboostWpf.Models;

public class DuplicateGroup
{
    public string Header { get; set; } = "";
    public List<DuplicateFile> Files { get; set; } = new();
}

public partial class DuplicateFile : ObservableObject
{
    [ObservableProperty] private bool _isSelectedForDelete;
    public string Path { get; set; } = "";
    public long Size { get; set; }
}
