using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using IkoboostWpf.Services;
using IkoboostWpf.Views;
using MessageBox = System.Windows.MessageBox;
using MediaColor = System.Windows.Media.Color;

namespace IkoboostWpf;

public partial class App : System.Windows.Application
{
    public static event Action<string>? ThemeChanged;
    public static event Action<string>? LanguageChanged;

    protected override async void OnStartup(StartupEventArgs e)
    {
        var settings = new SettingsService().Load();
        ApplyTheme(settings.Theme);
        ApplyLanguage(settings.Language);
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (s, ex) =>
        {
            ex.Handled = true;

            // Ignorer silencieusement les erreurs de cycle de vie (navigation rapide)
            if (ex.Exception is ObjectDisposedException or OperationCanceledException)
                return;

            System.Windows.MessageBox.Show(
                string.Format(LocalizationService.Get("App.UnexpectedError"), ex.Exception.Message),
                "Ikoboost", MessageBoxButton.OK, MessageBoxImage.Error);
        };

        var loadingWindow = new LoadingWindow();
        loadingWindow.Show();
        loadingWindow.Activate();

        await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await Task.Delay(3600);

        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        mainWindow.Show();
        loadingWindow.Close();
        ShutdownMode = ShutdownMode.OnMainWindowClose;

        if (!settings.OnboardingCompleted)
        {
            var onboarding = new OnboardingWindow(new SettingsService())
            {
                Owner = mainWindow
            };
            var completed = onboarding.ShowDialog() == true;
            if (!completed)
                mainWindow.Close();
        }
    }

    public static void ApplyTheme(string theme)
    {
        var source = theme switch
        {
            "Light" => "Themes/LightTheme.xaml",
            "Video" => "Themes/VideoTheme.xaml",
            "Cybertek" or "Cyberpunk" => "Themes/CyberpunkTheme.xaml",
            _ => "Themes/DarkTheme.xaml"
        };

        Current.Resources.MergedDictionaries.Clear();
        Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri(source, UriKind.Relative)
        });
        Current.Resources.MergedDictionaries.Add(new ResourceDictionary
        {
            Source = new Uri("Themes/Controls.xaml", UriKind.Relative)
        });
        ThemeChanged?.Invoke(theme);
    }

    public static void ApplyLanguage(string language)
    {
        LocalizationService.Apply(language);
        LanguageChanged?.Invoke(language);
    }

    public static void ApplyAccentTheme(string accentTheme)
    {
        var (accent, dim) = accentTheme switch
        {
            "ocean" => (MediaColor.FromRgb(14, 165, 233), MediaColor.FromRgb(3, 105, 161)),
            "forest" => (MediaColor.FromRgb(34, 197, 94), MediaColor.FromRgb(21, 128, 61)),
            "sunset" => (MediaColor.FromRgb(249, 115, 22), MediaColor.FromRgb(194, 65, 12)),
            "violet" => (MediaColor.FromRgb(139, 92, 246), MediaColor.FromRgb(109, 40, 217)),
            "rose" => (MediaColor.FromRgb(244, 63, 94), MediaColor.FromRgb(190, 18, 60)),
            _ => (MediaColor.FromRgb(0, 229, 255), MediaColor.FromRgb(0, 151, 167))
        };

        Current.Resources["AccentColor"] = accent;
        Current.Resources["AccentDimColor"] = dim;
        Current.Resources["AccentBrush"] = new SolidColorBrush(accent);
        Current.Resources["AccentDimBrush"] = new SolidColorBrush(dim);
    }
}
