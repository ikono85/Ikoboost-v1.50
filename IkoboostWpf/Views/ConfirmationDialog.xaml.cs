using System.Windows;
using System.Windows.Input;

// Aliases: project has UseWindowsForms=true, so several types collide with WPF equivalents.
using WpfApp = System.Windows.Application;
using WpfColor = System.Windows.Media.Color;
using WpfSCBrush = System.Windows.Media.SolidColorBrush;

namespace IkoboostWpf.Views;

/// <summary>
/// Dialog de confirmation thémé, réutilisable dans toute l'application.
///
/// API statique :
///   bool confirmed = ConfirmationDialog.Show("Titre", "Message", "Confirmer", isDanger: true);
///   var (ok, restore) = ConfirmationDialog.ShowWithRestoreOption("Titre", "Message", items: list);
/// </summary>
public partial class ConfirmationDialog : Window
{
    private bool _confirmed;
    private bool _createRestorePoint;

    // ── Constructeur (privé — utiliser les méthodes statiques) ───────────

    private ConfirmationDialog(
        string title,
        string message,
        string confirmLabel,
        bool isDanger,
        IEnumerable<string>? items,
        bool showRestoreOption)
    {
        InitializeComponent();

        // Centrer sur la fenêtre principale
        if (WpfApp.Current?.MainWindow is Window owner && !ReferenceEquals(owner, this))
            Owner = owner;

        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmLabel;

        ApplyIconStyle(isDanger);

        ConfirmButton.Style = isDanger
            ? (Style)FindResource("DangerButtonStyle")
            : (Style)FindResource("PrimaryButtonStyle");

        // Liste d'éléments
        if (items != null)
        {
            var list = items.ToList();
            if (list.Count > 0)
            {
                ItemsList.ItemsSource = list;
                ItemsBorder.Visibility = Visibility.Visible;
            }
        }

        // Checkbox point de restauration
        if (showRestoreOption)
            RestoreCheckBox.Visibility = Visibility.Visible;
    }

    // ── Icône et couleurs ────────────────────────────────────────────────

    private void ApplyIconStyle(bool isDanger)
    {
        WpfColor color;

        if (isDanger)
        {
            IconText.Text = "✕";
            color = (WpfApp.Current?.TryFindResource("ErrorBrush") as WpfSCBrush)?.Color
                    ?? WpfColor.FromRgb(0xF8, 0x51, 0x49);
        }
        else
        {
            IconText.Text = "⚠";
            color = (WpfApp.Current?.TryFindResource("WarningBrush") as WpfSCBrush)?.Color
                    ?? WpfColor.FromRgb(0xD2, 0x99, 0x22);
        }

        // Cercle : couleur à 15 % d'opacité
        IconCircle.Background = new WpfSCBrush(WpfColor.FromArgb(38, color.R, color.G, color.B));
        // Icône : couleur pleine
        IconText.Foreground = new WpfSCBrush(color);
    }

    // ── Handlers ─────────────────────────────────────────────────────────

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        => DragMove();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        _confirmed = false;
        Close();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        _confirmed = true;
        _createRestorePoint = RestoreCheckBox.IsChecked == true;
        Close();
    }

    // ── API publique statique ─────────────────────────────────────────────

    /// <summary>
    /// Affiche un dialog de confirmation thémé.
    /// Retourne <c>true</c> si l'utilisateur a confirmé.
    /// </summary>
    /// <param name="isDanger"><c>true</c> → bouton rouge (Danger) ; <c>false</c> → bouton primary (Warning).</param>
    public static bool Show(
        string title,
        string message,
        string confirmLabel = "Confirmer",
        bool isDanger = true,
        IEnumerable<string>? items = null)
    {
        var dlg = new ConfirmationDialog(title, message, confirmLabel, isDanger, items, false);
        dlg.ShowDialog();
        return dlg._confirmed;
    }

    /// <summary>
    /// Affiche un dialog de confirmation avec checkbox « Créer un point de restauration ».
    /// Retourne <c>(Confirmed, CreateRestorePoint)</c>.
    /// </summary>
    public static (bool Confirmed, bool CreateRestorePoint) ShowWithRestoreOption(
        string title,
        string message,
        string confirmLabel = "Désinstaller",
        IEnumerable<string>? items = null)
    {
        var dlg = new ConfirmationDialog(title, message, confirmLabel, true, items, true);
        dlg.ShowDialog();
        return (dlg._confirmed, dlg._createRestorePoint);
    }
}
