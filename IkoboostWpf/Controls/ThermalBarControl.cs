// Aliases (le projet a UseWindowsForms=true → collisions de types avec WPF).
using WpfBrush = System.Windows.Media.Brush;
using WpfColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace IkoboostWpf.Controls;

/// <summary>
/// Barre thermique horizontale à dégradé (vert → ambre → rouge), arrondie.
/// Prend directement le texte de température du ViewModel (ex. "58°C", "58.0 C")
/// et en extrait la valeur — AUCUN binding numérique requis.
///
/// Exemple :
///   <controls:ThermalBarControl TempText="{Binding CpuTempText}" Height="8"/>
/// </summary>
public sealed class ThermalBarControl : FrameworkElement
{
    public static readonly DependencyProperty TempTextProperty =
        DependencyProperty.Register(nameof(TempText), typeof(string), typeof(ThermalBarControl),
            new FrameworkPropertyMetadata("N/A", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxProperty =
        DependencyProperty.Register(nameof(Max), typeof(double), typeof(ThermalBarControl),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public string TempText { get => (string)GetValue(TempTextProperty); set => SetValue(TempTextProperty, value); }
    public double Max { get => (double)GetValue(MaxProperty); set => SetValue(MaxProperty, value); }

    private static double ParseCelsius(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return -1;
        var m = Regex.Match(text, @"-?\d+([.,]\d+)?");
        if (!m.Success) return -1;
        return double.TryParse(m.Value.Replace(',', '.'),
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : -1;
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var r = h / 2.0;

        // Piste (fond sombre arrondi)
        var trackBrush = System.Windows.Application.Current?.TryFindResource("SurfaceHoverBrush")
                             is WpfBrush tb ? tb : new SolidColorBrush(WpfColor.FromRgb(26, 35, 48));
        dc.DrawRoundedRectangle(trackBrush, null, new WpfRect(0, 0, w, h), r, r);

        var celsius = ParseCelsius(TempText);
        if (celsius <= 0) return;

        var frac = Math.Clamp(celsius / (Max <= 0 ? 100 : Max), 0, 1);
        var fillW = Math.Max(h, frac * w); // au moins une "pastille" ronde

        // Dégradé absolu sur toute la largeur → couleur cohérente quelle que soit la valeur
        var grad = new LinearGradientBrush
        {
            StartPoint = new WpfPoint(0, 0),
            EndPoint = new WpfPoint(w, 0),
            MappingMode = BrushMappingMode.Absolute,
        };
        grad.GradientStops.Add(new GradientStop(WpfColor.FromRgb(0x38, 0xD9, 0x96), 0.0));   // vert
        grad.GradientStops.Add(new GradientStop(WpfColor.FromRgb(0xF5, 0xB5, 0x3D), 0.62));  // ambre
        grad.GradientStops.Add(new GradientStop(WpfColor.FromRgb(0xFB, 0x5E, 0x63), 1.0));   // rouge
        grad.Freeze();

        dc.DrawRoundedRectangle(grad, null, new WpfRect(0, 0, fillW, h), r, r);
    }
}
