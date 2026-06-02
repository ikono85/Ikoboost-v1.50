using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace IkoboostWpf.Views;

public partial class ConfirmationDialog : Window
{
    public bool? UserConfirmed { get; private set; }
    public bool CreateRestorePoint => RestoreCheckBox.IsChecked == true;

    public ConfirmationDialog(string title, string message, string confirmText = "Confirmer",
        bool isDanger = true, IEnumerable<string>? items = null, bool showRestoreOption = false)
    {
        InitializeComponent();

        TitleText.Text = title;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;

        if (isDanger)
        {
            IconCircle.Background = new SolidColorBrush(Color.FromArgb(40, 251, 94, 99));
            IconText.Text = "!";
            IconText.Foreground = new SolidColorBrush(Color.FromRgb(251, 94, 99));
        }
        else
        {
            IconCircle.Background = new SolidColorBrush(Color.FromArgb(40, 47, 230, 242));
            IconText.Text = "i";
            IconText.Foreground = new SolidColorBrush(Color.FromRgb(47, 230, 242));
        }

        if (items != null)
        {
            ItemsList.ItemsSource = items;
            ItemsBorder.Visibility = Visibility.Visible;
        }

        if (showRestoreOption)
            RestoreCheckBox.Visibility = Visibility.Visible;
    }

    private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        => DragMove();

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        UserConfirmed = true;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        UserConfirmed = false;
        DialogResult = false;
        Close();
    }
}
