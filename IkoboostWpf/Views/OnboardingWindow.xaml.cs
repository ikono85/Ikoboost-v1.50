using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IkoboostWpf.Views;

public partial class OnboardingWindow : Window
{
    private int _currentStep = 1;
    private string _selectedLanguage = "";
    private string _selectedSource = "";
    private string _selectedTheme = "";
    private Button? _langBtn;
    private Button? _sourceBtn;
    private Button? _themeBtn;

    public OnboardingWindow()
    {
        InitializeComponent();
    }

    private void OnboardingWindow_Loaded(object sender, RoutedEventArgs e)
    {
        BuildLanguageOptions();
        BuildDiscoveryOptions();
        BuildThemeOptions();
        GoToStep(1);
    }

    private void BuildLanguageOptions()
    {
        foreach (var (name, code) in new[] { ("Français", "FR"), ("English", "EN"), ("Español", "ES") })
        {
            var btn = CreateChoiceButton(name);
            var captureCode = code;
            btn.Click += (_, _) => OnLanguageSelected(captureCode, btn);
            LanguageOptionsGrid.Children.Add(btn);
        }
    }

    private void BuildDiscoveryOptions()
    {
        foreach (var (label, key) in new[]
        {
            ("Déjà utilisateur", "existing"),
            ("Recommandation", "friend"),
            ("Recherche web", "web"),
            ("Problème PC", "issue"),
        })
        {
            var btn = CreateChoiceButton(label);
            var captureKey = key;
            btn.Click += (_, _) => OnSourceSelected(captureKey, btn);
            DiscoveryOptionsGrid.Children.Add(btn);
        }
    }

    private void BuildThemeOptions()
    {
        foreach (var (label, key) in new[] { ("Cyberpunk", "cyberpunk"), ("Sombre", "dark") })
        {
            var btn = CreateChoiceButton(label);
            var captureKey = key;
            btn.Click += (_, _) => OnThemeSelected(captureKey, btn);
            ThemeOptionsGrid.Children.Add(btn);
        }
    }

    private Button CreateChoiceButton(string label) => new()
    {
        Style = (Style)Resources["OnboardingChoiceButtonStyle"],
        Margin = new Thickness(4),
        Content = new TextBlock
        {
            Text = label,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
        }
    };

    private void OnLanguageSelected(string code, Button btn)
    {
        _selectedLanguage = code;
        Highlight(ref _langBtn, btn);
        ContinueLanguageButton.IsEnabled = true;
        ContinueLanguageButton.Content = code == "FR" ? "Continuer en Français" : $"Continue in {code}";
    }

    private void OnSourceSelected(string key, Button btn)
    {
        _selectedSource = key;
        Highlight(ref _sourceBtn, btn);
        ContinueDiscoveryButton.IsEnabled = true;
    }

    private void OnThemeSelected(string key, Button btn)
    {
        _selectedTheme = key;
        Highlight(ref _themeBtn, btn);
        ContinueThemeButton.IsEnabled = true;
    }

    private void Highlight(ref Button? previous, Button next)
    {
        if (previous != null)
            previous.BorderBrush = (Brush)Application.Current.Resources["BorderBrush"];
        previous = next;
        next.BorderBrush = (Brush)Application.Current.Resources["AccentBrush"];
    }

    private void GoToStep(int step)
    {
        _currentStep = step;
        var accent = (Brush)Application.Current.Resources["AccentBrush"];
        var surface = (Brush)Application.Current.Resources["SurfaceBrush"];

        ProgressOne.Background   = step >= 1 ? accent : surface;
        ProgressTwo.Background   = step >= 2 ? accent : surface;
        ProgressThree.Background = step >= 3 ? accent : surface;
        ProgressFour.Background  = step >= 4 ? accent : surface;

        LanguageStep.Visibility  = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        DiscoveryStep.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        ThemeStep.Visibility     = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        ConfirmStep.Visibility   = step == 4 ? Visibility.Visible : Visibility.Collapsed;

        switch (step)
        {
            case 1:
                EyebrowText.Text  = "ÉTAPE 1 / 4 • LANGUE";
                TitleText.Text    = "Choisissez votre langue";
                SubtitleText.Text = "Sélectionnez la langue d'interface d'Ikoboost.";
                break;
            case 2:
                EyebrowText.Text  = "ÉTAPE 2 / 4 • DÉCOUVERTE";
                TitleText.Text    = "Comment avez-vous découvert Ikoboost ?";
                SubtitleText.Text = "Aidez-nous à mieux vous connaître.";
                BackDiscoveryButton.Content     = "← Retour";
                SkipDiscoveryButton.Content     = "Passer";
                ContinueDiscoveryButton.Content = "Continuer";
                break;
            case 3:
                EyebrowText.Text  = "ÉTAPE 3 / 4 • THÈME";
                TitleText.Text    = "Choisissez votre thème";
                SubtitleText.Text = "Personnalisez l'apparence d'Ikoboost.";
                BackThemeButton.Content     = "← Retour";
                ContinueThemeButton.Content = "Continuer";
                break;
            case 4:
                EyebrowText.Text  = "ÉTAPE 4 / 4 • PRÊT";
                TitleText.Text    = "Tout est prêt !";
                SubtitleText.Text = "Votre configuration Ikoboost est enregistrée.";
                SummaryTitleText.Text    = "Récapitulatif";
                SummaryLanguageText.Text = $"Langue : {LanguageLabel(_selectedLanguage)}";
                SummarySourceText.Text   = $"Découverte : {SourceLabel(_selectedSource)}";
                SummaryThemeText.Text    = $"Thème : {(_selectedTheme == "cyberpunk" ? "Cyberpunk" : "Sombre")}";
                EnterAppButton.Content   = "Entrer dans Ikoboost →";
                break;
        }
    }

    private static string LanguageLabel(string code) => code switch
    {
        "FR" => "Français",
        "EN" => "English",
        "ES" => "Español",
        _    => "Français"
    };

    private static string SourceLabel(string key) => key switch
    {
        "existing" => "Déjà utilisateur",
        "friend"   => "Recommandation",
        "web"      => "Recherche web",
        "issue"    => "Problème PC",
        _          => "Non renseigné"
    };

    private void ContinueLanguageButton_Click(object sender, RoutedEventArgs e) => GoToStep(2);
    private void BackButton_Click(object sender, RoutedEventArgs e)              => GoToStep(_currentStep - 1);
    private void SkipDiscoveryButton_Click(object sender, RoutedEventArgs e)     { _selectedSource = ""; GoToStep(3); }
    private void ContinueDiscoveryButton_Click(object sender, RoutedEventArgs e) => GoToStep(3);
    private void ContinueThemeButton_Click(object sender, RoutedEventArgs e)     => GoToStep(4);

    private void EnterAppButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
