using IkoboostWpf.Services;
using IkoboostWpf.ViewModels;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace IkoboostWpf.Views;

public partial class NetworkPage : Page
{
    private readonly NetworkViewModel _vm;

    public NetworkPage(NetworkService network, OptimizationService optimization)
    {
        _vm = new NetworkViewModel(network, optimization);

        Resources.Add("InvertBool", new InvertBoolConverter());
        Resources.Add("BoolToOkErrConverter", new BoolToOkErrConverter());
        Resources.Add("StringToVisConverter", new StringToVisibilityConverter());

        InitializeComponent();
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.InitializeAsync();
        Unloaded += (_, _) => _vm.Pause();
    }
}

public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is bool b && !b;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class BoolToOkErrConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is bool b && b ? "OK" : "Échec";
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}

public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => string.IsNullOrEmpty(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => throw new NotSupportedException();
}
