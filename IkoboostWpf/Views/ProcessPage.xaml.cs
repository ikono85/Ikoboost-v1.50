using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using IkoboostWpf.Models;
using IkoboostWpf.ViewModels;

namespace IkoboostWpf.Views;

public partial class ProcessPage : Page
{
    private ProcessViewModel _vm = null!;
    private Popup? _processMenuPopup;

    public ProcessPage(ProcessViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
    }

    private void ProcessesGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not DataGrid grid) return;

        var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.Item is ProcessItem clicked)
        {
            grid.SelectedItem = clicked;
            _vm.SelectedProcess = clicked;
        }

        if (_vm.SelectedProcess is not ProcessItem proc) return;

        e.Handled = true;
        ShowProcessMenu(proc);
    }

    private void ProcessesGrid_LoadingRow(object sender, DataGridRowEventArgs e) { }

    private void ShowProcessMenu(ProcessItem proc)
    {
        if (_processMenuPopup != null)
            _processMenuPopup.IsOpen = false;

        var panel = new StackPanel
        {
            Background = MakeBrush("#10151D"),
        };

        panel.Children.Add(new TextBlock
        {
            Text = $"{proc.Name}  •  {proc.Pid}",
            Foreground = MakeBrush("#5A6675"),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Margin = new Thickness(16, 13, 16, 10),
        });

        panel.Children.Add(MenuButton("\uE74D", "Terminer la tâche", MakeBrush("#FB5E63"), () =>
        {
            CloseProcessMenu();
            var msg = _vm.KillProcess(proc.Pid);
            MessageBox.Show(msg, "Ikoboost", MessageBoxButton.OK, MessageBoxImage.Information);
        }));

        panel.Children.Add(new Border
        {
            Height = 1,
            Background = MakeBrush("#222A36"),
            Margin = new Thickness(8, 8, 8, 8),
        });

        panel.Children.Add(new TextBlock
        {
            Text = "PRIORITÉ",
            Foreground = MakeBrush("#5A6675"),
            FontFamily = new FontFamily("Consolas"),
            FontSize = 11,
            Margin = new Thickness(16, 0, 16, 6),
        });

        foreach (var (label, priority) in new (string, ProcessPriorityClass)[]
        {
            ("Temps réel", ProcessPriorityClass.RealTime),
            ("Élevée", ProcessPriorityClass.High),
            ("Normale", ProcessPriorityClass.Normal),
            ("Inférieure", ProcessPriorityClass.BelowNormal),
        })
        {
            var isCurrent = string.Equals(proc.PriorityKey, priority.ToString(), StringComparison.OrdinalIgnoreCase);
            panel.Children.Add(PriorityButton(label, isCurrent, () =>
            {
                CloseProcessMenu();
                _vm.SetPriority(proc.Pid, priority);
            }));
        }

        _processMenuPopup = new Popup
        {
            PlacementTarget = ProcessesGrid,
            Placement = PlacementMode.MousePoint,
            AllowsTransparency = true,
            StaysOpen = false,
            Child = new Border
            {
                MinWidth = 210,
                CornerRadius = new CornerRadius(8),
                Background = MakeBrush("#10151D"),
                BorderBrush = MakeBrush("#2C3643"),
                BorderThickness = new Thickness(1),
                Child = panel,
            },
        };
        _processMenuPopup.IsOpen = true;
    }

    private void CloseProcessMenu()
    {
        if (_processMenuPopup != null)
            _processMenuPopup.IsOpen = false;
    }

    private static Border MenuButton(string icon, string text, Brush foreground, Action click)
    {
        var border = CreateMenuRow();
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        row.ColumnDefinitions.Add(new ColumnDefinition());

        row.Children.Add(new TextBlock
        {
            Text = icon,
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            Foreground = foreground,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var label = new TextBlock
        {
            Text = text,
            Foreground = foreground,
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        };
        Grid.SetColumn(label, 1);
        row.Children.Add(label);

        border.Child = row;
        border.MouseLeftButtonDown += (_, _) => click();
        return border;
    }

    private static Border PriorityButton(string text, bool isCurrent, Action click)
    {
        var border = CreateMenuRow();
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = MakeBrush("#E9EFF6"),
            FontSize = 13,
            FontWeight = FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(8, 0, 0, 0),
        });

        if (isCurrent)
        {
            var check = new TextBlock
            {
                Text = "\uE73E",
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                Foreground = MakeBrush("#2FE6F2"),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(22, 0, 2, 0),
            };
            Grid.SetColumn(check, 1);
            row.Children.Add(check);
        }

        border.Child = row;
        border.MouseLeftButtonDown += (_, _) => click();
        return border;
    }

    private static Border CreateMenuRow()
    {
        var border = new Border
        {
            Height = 36,
            Margin = new Thickness(8, 0, 8, 0),
            Padding = new Thickness(8, 0, 10, 0),
            CornerRadius = new CornerRadius(6),
            Cursor = Cursors.Hand,
            Background = Brushes.Transparent,
        };
        border.MouseEnter += (_, _) => border.Background = MakeBrush("#1C2531");
        border.MouseLeave += (_, _) => border.Background = Brushes.Transparent;
        return border;
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child != null)
        {
            if (child is T typed) return typed;
            child = VisualTreeHelper.GetParent(child);
        }
        return null;
    }

    private static Brush MakeBrush(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
}
