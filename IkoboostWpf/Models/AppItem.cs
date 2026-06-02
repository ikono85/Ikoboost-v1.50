using CommunityToolkit.Mvvm.ComponentModel;

namespace IkoboostWpf.Models;

public partial class AppItem : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _id = "";
    [ObservableProperty] private string _displayVersion = "";
    [ObservableProperty] private string _displayPublisher = "";
    [ObservableProperty] private string _sourceLabel = "";
    [ObservableProperty] private string _iconPath = "";
    [ObservableProperty] private bool _hasUpdate;
}
