using IkoboostWpf.Services;
using IkoboostWpf.ViewModels;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using DrawingIcon = System.Drawing.Icon;

namespace IkoboostWpf.Views;

public partial class WingetPage : Page
{
    private readonly WingetViewModel _vm;

    public WingetPage(WingetService winget)
    {
        _vm = new WingetViewModel(winget);
        Resources.Add("AppIconConverter", new AppIconConverter());
        InitializeComponent();
        DataContext = _vm;
        Loaded += async (_, _) => await _vm.InitializeAsync();
    }
}

public sealed class AppIconConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var iconPath = NormalizeIconPath(value?.ToString());
        if (string.IsNullOrWhiteSpace(iconPath) || !File.Exists(iconPath))
            return CreateFallbackIcon();

        try
        {
            if (IsBitmapIcon(iconPath))
                return LoadBitmap(iconPath);

            using var icon = DrawingIcon.ExtractAssociatedIcon(iconPath);
            if (icon == null)
                return null;

            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(48, 48));
            source.Freeze();
            return source;
        }
        catch
        {
            return CreateFallbackIcon();
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static string NormalizeIconPath(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var path = Environment.ExpandEnvironmentVariables(raw.Trim().Trim('"'));

        if (File.Exists(path))
            return path;

        var commaIndex = path.LastIndexOf(',');
        if (commaIndex > 0)
        {
            var withoutIndex = path[..commaIndex].Trim().Trim('"');
            if (File.Exists(withoutIndex))
                return withoutIndex;
        }

        return path;
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.DecodePixelWidth = 96;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static bool IsBitmapIcon(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ico", StringComparison.OrdinalIgnoreCase);
    }

    private static BitmapSource CreateFallbackIcon()
    {
        using var icon = DrawingIcon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty)
            ?? System.Drawing.SystemIcons.Application;
        var source = Imaging.CreateBitmapSourceFromHIcon(
            icon.Handle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromWidthAndHeight(48, 48));
        source.Freeze();
        return source;
    }
}
