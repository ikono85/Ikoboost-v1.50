using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using IkoboostWpf.Services;
using MediaBrush = System.Windows.Media.Brush;
using WpfButton = System.Windows.Controls.Button;

namespace IkoboostWpf.Views;

public partial class OnboardingWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly List<WpfButton> _languageButtons = [];
    private readonly List<WpfButton> _discoveryButtons = [];
    private readonly List<WpfButton> _themeButtons = [];
    private int _step = 1;
    private string? _language;
    private string? _discoverySource;
    private string? _theme;

    private sealed record Copy(
        string Step,
        string LanguageTitle,
        string LanguageSubtitle,
        string Continue,
        string Back,
        string Skip,
        string DiscoveryTitle,
        string DiscoverySubtitle,
        string ThemeTitle,
        string ThemeSubtitle,
        string ConfirmTitle,
        string ConfirmSubtitle,
        string Summary,
        string SummaryLanguage,
        string SummarySource,
        string SummaryTheme,
        string NotProvided,
        string EnterApp);

    private static readonly Copy FrenchCopy = new(
        "Étape {0} sur 4",
        "Choisissez votre langue",
        "Vous pourrez changer ce choix plus tard dans les paramètres.",
        "Continuer",
        "Retour",
        "Passer",
        "Comment avez-vous découvert l'app ?",
        "Une seule réponse suffit, cela nous aide à mieux comprendre votre parcours.",
        "Choisissez votre thème",
        "Le thème est appliqué instantanément dans toute l'interface.",
        "Configuration terminée !",
        "Votre application est prête à être utilisée.",
        "Récapitulatif",
        "Langue : {0}",
        "Source : {0}",
        "Thème : {0}",
        "Non renseigné",
        "Entrer dans l'application");

    private static readonly Copy EnglishCopy = new(
        "Step {0} of 4",
        "Choose your language",
        "You can change this later in settings.",
        "Continue",
        "Back",
        "Skip",
        "How did you discover the app?",
        "One answer is enough and helps us understand your path.",
        "Choose your theme",
        "The theme is applied instantly across the interface.",
        "Setup complete!",
        "Your application is ready to use.",
        "Summary",
        "Language: {0}",
        "Source: {0}",
        "Theme: {0}",
        "Not provided",
        "Enter the application");
    private static readonly Copy SpanishCopy = new(
        "Paso {0} de 4",
        "Elige tu idioma",
        "Podrás cambiar esta opción más tarde en los ajustes.",
        "Continuar",
        "Atrás",
        "Omitir",
        "¿Cómo descubriste la aplicación?",
        "Una sola respuesta basta y nos ayuda a entender tu recorrido.",
        "Elige tu tema",
        "El tema se aplica al instante en toda la interfaz.",
        "¡Configuración terminada!",
        "Tu aplicación está lista para usarse.",
        "Resumen",
        "Idioma: {0}",
        "Fuente: {0}",
        "Tema: {0}",
        "No indicado",
        "Entrar en la aplicación");

    private static readonly Copy PortugueseBrazilCopy = new(
        "Etapa {0} de 4",
        "Escolha seu idioma",
        "Você poderá alterar esta opção depois nas configurações.",
        "Continuar",
        "Voltar",
        "Pular",
        "Como você descobriu o aplicativo?",
        "Uma resposta é suficiente e nos ajuda a entender seu caminho.",
        "Escolha seu tema",
        "O tema é aplicado instantaneamente em toda a interface.",
        "Configuração concluída!",
        "Seu aplicativo está pronto para uso.",
        "Resumo",
        "Idioma: {0}",
        "Fonte: {0}",
        "Tema: {0}",
        "Não informado",
        "Entrar no aplicativo");

    private static readonly Copy GermanCopy = new(
        "Schritt {0} von 4",
        "Wähle deine Sprache",
        "Du kannst diese Auswahl später in den Einstellungen ändern.",
        "Weiter",
        "Zurück",
        "Überspringen",
        "Wie hast du die App entdeckt?",
        "Eine Antwort reicht und hilft uns, deinen Weg besser zu verstehen.",
        "Wähle dein Design",
        "Das Design wird sofort auf die gesamte Oberfläche angewendet.",
        "Einrichtung abgeschlossen!",
        "Deine Anwendung ist bereit.",
        "Zusammenfassung",
        "Sprache: {0}",
        "Quelle: {0}",
        "Design: {0}",
        "Nicht angegeben",
        "Anwendung öffnen");

    private static readonly Copy ArabicCopy = new(
        "الخطوة {0} من 4",
        "اختر لغتك",
        "يمكنك تغيير هذا الخيار لاحقا من الإعدادات.",
        "متابعة",
        "رجوع",
        "تخطي",
        "كيف اكتشفت التطبيق؟",
        "إجابة واحدة تكفي وتساعدنا على فهم تجربتك.",
        "اختر السمة",
        "يتم تطبيق السمة فورا على الواجهة كلها.",
        "اكتمل الإعداد!",
        "التطبيق جاهز للاستخدام.",
        "الملخص",
        "اللغة: {0}",
        "المصدر: {0}",
        "السمة: {0}",
        "غير محدد",
        "الدخول إلى التطبيق");

    private static readonly Copy RussianCopy = new(
        "Шаг {0} из 4",
        "Выберите язык",
        "Вы сможете изменить этот выбор позже в настройках.",
        "Продолжить",
        "Назад",
        "Пропустить",
        "Как вы узнали о приложении?",
        "Одного ответа достаточно, это поможет нам понять ваш путь.",
        "Выберите тему",
        "Тема мгновенно применяется ко всему интерфейсу.",
        "Настройка завершена!",
        "Приложение готово к использованию.",
        "Сводка",
        "Язык: {0}",
        "Источник: {0}",
        "Тема: {0}",
        "Не указано",
        "Войти в приложение");

    private static readonly Copy SimplifiedChineseCopy = new(
        "第 {0} / 4 步",
        "选择你的语言",
        "稍后可在设置中更改此选项。",
        "继续",
        "返回",
        "跳过",
        "你是如何发现这个应用的？",
        "一个答案即可，这能帮助我们了解你的使用路径。",
        "选择你的主题",
        "主题会立即应用到整个界面。",
        "设置完成！",
        "你的应用已准备好使用。",
        "摘要",
        "语言：{0}",
        "来源：{0}",
        "主题：{0}",
        "未填写",
        "进入应用");

    private static readonly Copy JapaneseCopy = new(
        "ステップ {0} / 4",
        "言語を選択してください",
        "この選択は後で設定から変更できます。",
        "続ける",
        "戻る",
        "スキップ",
        "このアプリをどこで知りましたか？",
        "1つ選ぶだけで、利用経路の理解に役立ちます。",
        "テーマを選択してください",
        "テーマはすぐにインターフェース全体へ適用されます。",
        "設定が完了しました！",
        "アプリケーションを使用する準備ができました。",
        "概要",
        "言語: {0}",
        "きっかけ: {0}",
        "テーマ: {0}",
        "未入力",
        "アプリケーションに入る");

    private sealed record DiscoveryOption(string Code, string Icon, string Fr, string En);
    private sealed record LanguageOption(string Code, string Icon, string Label);
    private sealed record ThemeOption(string Code, string Emoji, string Fr, string En, string Accent, string Dim);

    private static readonly LanguageOption[] LanguageOptions =
    [
        new("fr", "🇫🇷", "Français"),
        new("en", "🇬🇧", "English"),
        new("es", "🇪🇸", "Español"),
        new("pt-BR", "🇧🇷", "Português (Brasil)"),
        new("de", "🇩🇪", "Deutsch"),
        new("ar", "🇸🇦", "العربية"),
        new("ru", "🇷🇺", "Русский"),
        new("zh-Hans", "🇨🇳", "中文简体"),
        new("ja", "🇯🇵", "日本語")
    ];

    private static readonly DiscoveryOption[] DiscoveryOptions =
    [
        new("social", "📱", "Réseaux sociaux", "Social media"),
        new("friend", "👥", "Ami ou collègue", "Friend or colleague"),
        new("search", "🔎", "Moteur de recherche", "Search engine"),
        new("podcast", "🎙️", "Podcast ou vidéo", "Podcast or video"),
        new("press", "📰", "Presse ou article", "Press or article"),
        new("other", "✨", "Autre", "Other")
    ];

    private static readonly ThemeOption[] ThemeOptions =
    [
        new("Dark", "🌙", "Sombre", "Dark", "#00E5FF", "#0097A7"),
        new("Light", "☀️", "Clair", "Light", "#7C3AED", "#A855F7"),
        new("Cybertek", "⚡", "Cybertek", "Cybertek", "#F0C040", "#A855F7"),
        new("Video", "🎬", "Vidéo", "Video", "#F0C040", "#A855F7")
    ];

    public OnboardingWindow(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        BuildOptionCards();
        ApplyLanguageSelection(null);
        ShowStep(1, animate: false);
    }

    private void OnboardingWindow_Loaded(object sender, RoutedEventArgs e)
    {
        CoverOwnerWindow();
    }

    private void CoverOwnerWindow()
    {
        if (Owner == null)
            return;

        Left = Owner.Left;
        Top = Owner.Top;
        Width = Math.Max(Owner.ActualWidth, MinWidth);
        Height = Math.Max(Owner.ActualHeight, MinHeight);
        WindowState = Owner.WindowState == WindowState.Maximized ? WindowState.Maximized : WindowState.Normal;
    }

    private Copy Texts => _language switch
    {
        "en" => EnglishCopy,
        "es" => SpanishCopy,
        "pt-BR" => PortugueseBrazilCopy,
        "de" => GermanCopy,
        "ar" => ArabicCopy,
        "ru" => RussianCopy,
        "zh-Hans" => SimplifiedChineseCopy,
        "ja" => JapaneseCopy,
        _ => FrenchCopy
    };

    private void BuildOptionCards()
    {
        foreach (var option in LanguageOptions)
        {
            var button = CreateChoiceButton(option.Code, CreateOptionContent(option.Icon, option.Label, option.Code));
            button.Click += LanguageButton_Click;
            LanguageOptionsGrid.Children.Add(button);
            _languageButtons.Add(button);
        }

        foreach (var option in DiscoveryOptions)
        {
            var button = CreateChoiceButton(option.Code, CreateOptionContent(option.Icon, option.Fr, option.En));
            button.Click += DiscoveryButton_Click;
            DiscoveryOptionsGrid.Children.Add(button);
            _discoveryButtons.Add(button);
        }

        foreach (var option in ThemeOptions)
        {
            var button = CreateChoiceButton(option.Code, CreateThemeContent(option));
            button.Click += ThemeButton_Click;
            ThemeOptionsGrid.Children.Add(button);
            _themeButtons.Add(button);
        }
    }

    private WpfButton CreateChoiceButton(string tag, object content)
        => new()
        {
            Tag = tag,
            Content = content,
            MinHeight = 98,
            Margin = new Thickness(5),
            Style = (Style)FindResource("OnboardingChoiceButtonStyle"),
            Cursor = System.Windows.Input.Cursors.Hand
        };

    private static StackPanel CreateOptionContent(string icon, string primary, string secondary)
        => new()
        {
            Children =
            {
                new TextBlock
                {
                    Text = icon,
                    FontSize = 26,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                },
                new TextBlock
                {
                    Text = primary,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                },
                new TextBlock
                {
                    Text = secondary,
                    Opacity = 0.7,
                    FontSize = 11,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                }
            }
        };

    private static StackPanel CreateThemeContent(ThemeOption option)
    {
        var swatch = new Border
        {
            Height = 28,
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 0, 0, 8),
            Background = new LinearGradientBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(option.Accent),
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(option.Dim),
                0)
        };

        return new StackPanel
        {
            Children =
            {
                swatch,
                new TextBlock
                {
                    Text = option.Emoji,
                    FontSize = 22,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                },
                new TextBlock
                {
                    Text = option.Fr,
                    Tag = option,
                    FontWeight = FontWeights.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                }
            }
        };
    }

    private void LanguageButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: string code })
            return;

        _language = code;
        App.ApplyLanguage(code);
        ApplyLanguageSelection(code);
        ContinueLanguageButton.IsEnabled = true;
        UpdateSelection(_languageButtons, code);
    }

    private void DiscoveryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: string code })
            return;

        _discoverySource = code;
        ContinueDiscoveryButton.IsEnabled = true;
        UpdateSelection(_discoveryButtons, code);
    }

    private void ThemeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton { Tag: string code })
            return;

        _theme = code;
        App.ApplyTheme(code);
        ContinueThemeButton.IsEnabled = true;
        UpdateSelection(_themeButtons, code);
    }

    private void ContinueLanguageButton_Click(object sender, RoutedEventArgs e) => ShowStep(2);
    private void ContinueDiscoveryButton_Click(object sender, RoutedEventArgs e) => ShowStep(3);
    private void ContinueThemeButton_Click(object sender, RoutedEventArgs e) => ShowStep(4);
    private void SkipDiscoveryButton_Click(object sender, RoutedEventArgs e)
    {
        _discoverySource = null;
        UpdateSelection(_discoveryButtons, null);
        ShowStep(3);
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => ShowStep(Math.Max(1, _step - 1));

    private void EnterAppButton_Click(object sender, RoutedEventArgs e)
    {
        var existing = _settingsService.Load();
        _settingsService.Save(new AppSettings
        {
            Theme = _theme ?? existing.Theme,
            Language = _language ?? "fr",
            AccentTheme = existing.AccentTheme,
            DiscoverySource = _discoverySource ?? string.Empty,
            OnboardingCompleted = true,
            RefreshIntervalSeconds = existing.RefreshIntervalSeconds,
            TempAlertCelsius = existing.TempAlertCelsius,
            MinimizeToTray = existing.MinimizeToTray,
            AlertNetwork = existing.AlertNetwork,
            AlertStorage = existing.AlertStorage
        });

        DialogResult = true;
        Close();
    }

    private void ShowStep(int step, bool animate = true)
    {
        _step = step;
        LanguageStep.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        DiscoveryStep.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        ThemeStep.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        ConfirmStep.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;
        UpdateTexts();
        UpdateProgress();

        if (!animate)
            return;

        Card.BeginAnimation(OpacityProperty, new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(180)));
    }

    private void ApplyLanguageSelection(string? code)
    {
        UpdateSelection(_languageButtons, code);
        UpdateTexts();
        UpdateDynamicOptionLabels();
    }

    private void UpdateTexts()
    {
        var copy = Texts;
        EyebrowText.Text = string.Format(copy.Step, _step);
        FlowDirection = _language == "ar" ? System.Windows.FlowDirection.RightToLeft : System.Windows.FlowDirection.LeftToRight;

        (TitleText.Text, SubtitleText.Text) = _step switch
        {
            1 => (copy.LanguageTitle, copy.LanguageSubtitle),
            2 => (copy.DiscoveryTitle, copy.DiscoverySubtitle),
            3 => (copy.ThemeTitle, copy.ThemeSubtitle),
            _ => (copy.ConfirmTitle, copy.ConfirmSubtitle)
        };

        ContinueLanguageButton.Content = _language == null ? "Continuer / Continue" : copy.Continue;
        BackDiscoveryButton.Content = copy.Back;
        SkipDiscoveryButton.Content = copy.Skip;
        ContinueDiscoveryButton.Content = copy.Continue;
        BackThemeButton.Content = copy.Back;
        ContinueThemeButton.Content = copy.Continue;
        EnterAppButton.Content = copy.EnterApp;

        SummaryTitleText.Text = copy.Summary;
        SummaryLanguageText.Text = string.Format(copy.SummaryLanguage, GetLanguageLabel(_language));
        SummarySourceText.Text = string.Format(copy.SummarySource, GetDiscoveryLabel(_discoverySource) ?? copy.NotProvided);
        SummaryThemeText.Text = string.Format(copy.SummaryTheme, GetThemeLabel(_theme));
    }

    private void UpdateDynamicOptionLabels()
    {
        for (var i = 0; i < DiscoveryOptions.Length; i++)
            _discoveryButtons[i].Content = CreateOptionContent(DiscoveryOptions[i].Icon, GetDiscoveryLabel(DiscoveryOptions[i].Code)!, DiscoveryOptions[i].Code);

        foreach (var button in _themeButtons)
        {
            if (button.Tag is not string code)
                continue;

            var option = ThemeOptions.First(item => item.Code == code);
            button.Content = CreateThemeContent(option);
            if (button.Content is StackPanel panel && panel.Children[^1] is TextBlock label)
                label.Text = GetThemeLabel(code);
        }
    }

    private void UpdateProgress()
    {
        var bars = new[] { ProgressOne, ProgressTwo, ProgressThree, ProgressFour };
        for (var i = 0; i < bars.Length; i++)
        {
            bars[i].Background = i < _step
                ? (MediaBrush)FindResource("AccentBrush")
                : (MediaBrush)FindResource("BorderBrush");
            bars[i].Opacity = i < _step ? 1 : 0.8;
        }
    }

    private void UpdateSelection(IEnumerable<WpfButton> buttons, string? selected)
    {
        foreach (var button in buttons)
        {
            var isSelected = selected != null && Equals(button.Tag, selected);
            button.Background = isSelected ? (MediaBrush)FindResource("AccentDimBrush") : (MediaBrush)FindResource("SurfaceAltBrush");
            button.Foreground = (MediaBrush)FindResource("TextPrimaryBrush");
            button.BorderBrush = isSelected ? (MediaBrush)FindResource("AccentBrush") : (MediaBrush)FindResource("BorderBrush");
            button.BorderThickness = new Thickness(isSelected ? 2 : 1);
        }
    }

    private string? GetDiscoveryLabel(string? code)
        => (code, _language) switch
        {
            ("social", "en") => "Social media",
            ("social", "es") => "Redes sociales",
            ("social", "pt-BR") => "Redes sociais",
            ("social", "de") => "Soziale Medien",
            ("social", "ar") => "وسائل التواصل الاجتماعي",
            ("social", "ru") => "Социальные сети",
            ("social", "zh-Hans") => "社交媒体",
            ("social", "ja") => "ソーシャルメディア",
            ("social", _) => "Réseaux sociaux",

            ("friend", "en") => "Friend or colleague",
            ("friend", "es") => "Amigo o colega",
            ("friend", "pt-BR") => "Amigo ou colega",
            ("friend", "de") => "Freund oder Kollege",
            ("friend", "ar") => "صديق أو زميل",
            ("friend", "ru") => "Друг или коллега",
            ("friend", "zh-Hans") => "朋友或同事",
            ("friend", "ja") => "友人または同僚",
            ("friend", _) => "Ami ou collègue",

            ("search", "en") => "Search engine",
            ("search", "es") => "Motor de búsqueda",
            ("search", "pt-BR") => "Mecanismo de busca",
            ("search", "de") => "Suchmaschine",
            ("search", "ar") => "محرك بحث",
            ("search", "ru") => "Поисковая система",
            ("search", "zh-Hans") => "搜索引擎",
            ("search", "ja") => "検索エンジン",
            ("search", _) => "Moteur de recherche",

            ("podcast", "en") => "Podcast or video",
            ("podcast", "es") => "Podcast o vídeo",
            ("podcast", "pt-BR") => "Podcast ou vídeo",
            ("podcast", "de") => "Podcast oder Video",
            ("podcast", "ar") => "بودكاست أو فيديو",
            ("podcast", "ru") => "Подкаст или видео",
            ("podcast", "zh-Hans") => "播客或视频",
            ("podcast", "ja") => "ポッドキャストまたは動画",
            ("podcast", _) => "Podcast ou vidéo",

            ("press", "en") => "Press or article",
            ("press", "es") => "Prensa o artículo",
            ("press", "pt-BR") => "Imprensa ou artigo",
            ("press", "de") => "Presse oder Artikel",
            ("press", "ar") => "صحافة أو مقال",
            ("press", "ru") => "Пресса или статья",
            ("press", "zh-Hans") => "媒体或文章",
            ("press", "ja") => "記事またはメディア",
            ("press", _) => "Presse ou article",

            ("other", "en") => "Other",
            ("other", "es") => "Otro",
            ("other", "pt-BR") => "Outro",
            ("other", "de") => "Andere",
            ("other", "ar") => "أخرى",
            ("other", "ru") => "Другое",
            ("other", "zh-Hans") => "其他",
            ("other", "ja") => "その他",
            ("other", _) => "Autre",
            _ => null
        };

    private string GetThemeLabel(string? code)
        => (code, _language) switch
        {
            ("Dark", "en") => "Dark",
            ("Dark", "es") => "Oscuro",
            ("Dark", "pt-BR") => "Escuro",
            ("Dark", "de") => "Dunkel",
            ("Dark", "ar") => "داكن",
            ("Dark", "ru") => "Темная",
            ("Dark", "zh-Hans") => "深色",
            ("Dark", "ja") => "ダーク",
            ("Dark", _) => "Sombre",

            ("Light", "en") => "Light",
            ("Light", "es") => "Claro",
            ("Light", "pt-BR") => "Claro",
            ("Light", "de") => "Hell",
            ("Light", "ar") => "فاتح",
            ("Light", "ru") => "Светлая",
            ("Light", "zh-Hans") => "浅色",
            ("Light", "ja") => "ライト",
            ("Light", _) => "Clair",

            ("Video", "ar") => "فيديو",
            ("Video", "ru") => "Видео",
            ("Video", "zh-Hans") => "视频",
            ("Video", "ja") => "ビデオ",
            ("Video", _) => "Video",
            ("Cybertek", _) => "Cybertek",
            _ => GetThemeLabel("Dark")
        };

    private static string GetLanguageLabel(string? code)
        => LanguageOptions.FirstOrDefault(item => item.Code == code)?.Label ?? "Français";
}


