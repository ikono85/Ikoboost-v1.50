// Aliases (le projet a UseWindowsForms=true → collisions de types avec WPF).
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

using System;
using System.Windows;
using System.Windows.Media;

namespace IkoboostWpf.Controls;

/// <summary>
/// Anneau de progression SANS texte central — conçu pour placer une icône
/// (ou tout contenu) au milieu via un Grid. Utilisé par les cartes de métriques
/// du Dashboard (CPU / RAM / GPU / Réseau).
///
/// Exemple :
///   <Grid Width="52" Height="52">
///     <controls:GaugeRingControl Percent="{Binding CpuPercent}" RingBrush="{DynamicResource AccentBrush}"/>
///     <Path .../>  <!-- icône centrée -->
///   </Grid>
/// </summary>
public sealed class GaugeRingControl : FrameworkElement
{
    public static readonly DependencyProperty PercentProperty =
        DependencyProperty.Register(nameof(Percent), typeof(double), typeof(GaugeRingControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(GaugeRingControl),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingBrushProperty =
        DependencyProperty.Register(nameof(RingBrush), typeof(WpfBrush), typeof(GaugeRingControl),
            new FrameworkPropertyMetadata(WpfBrushes.Cyan, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ThicknessProperty =
        DependencyProperty.Register(nameof(Thickness), typeof(double), typeof(GaugeRingControl),
            new FrameworkPropertyMetadata(6.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Percent { get => (double)GetValue(PercentProperty); set => SetValue(PercentProperty, value); }
    public double Maximum { get => (double)GetValue(MaximumProperty); set => SetValue(MaximumProperty, value); }
    public WpfBrush RingBrush { get => (WpfBrush)GetValue(RingBrushProperty); set => SetValue(RingBrushProperty, value); }
    public double Thickness { get => (double)GetValue(ThicknessProperty); set => SetValue(ThicknessProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var stroke = Thickness;
        var padding = stroke / 2 + 1;
        var cx = w / 2;
        var cy = h / 2;
        var radius = Math.Min(w, h) / 2 - padding;
        if (radius <= 0) return;

        // Track (cercle complet atténué)
        var trackColor = System.Windows.Application.Current?.TryFindResource("BorderStrongBrush")
                             is WpfBrush tb ? tb : new SolidColorBrush(WpfColor.FromArgb(60, 128, 128, 128));
        var trackPen = new WpfPen(trackColor, stroke);
        trackPen.Freeze();
        dc.DrawEllipse(null, trackPen, new WpfPoint(cx, cy), radius, radius);

        // Arc de progression
        var max = Maximum <= 0 ? 100 : Maximum;
        var frac = Math.Clamp(Percent / max, 0, 1);
        if (frac <= 0) return;

        var arcBrush = RingBrush ?? WpfBrushes.Cyan;
        var arcPen = new WpfPen(arcBrush, stroke)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        arcPen.Freeze();

        if (frac >= 0.999)
        {
            dc.DrawEllipse(null, arcPen, new WpfPoint(cx, cy), radius, radius);
            return;
        }

        var angle = frac * 360.0;
        var start = CirclePoint(cx, cy, radius, -90);
        var end = CirclePoint(cx, cy, radius, -90 + angle);

        var figure = new PathFigure { StartPoint = start, IsClosed = false };
        figure.Segments.Add(new ArcSegment(end, new WpfSize(radius, radius), 0,
            angle > 180, SweepDirection.Clockwise, isStroked: true));
        var geo = new PathGeometry(new[] { figure });
        geo.Freeze();
        dc.DrawGeometry(null, arcPen, geo);
    }

    private static WpfPoint CirclePoint(double cx, double cy, double r, double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180.0;
        return new WpfPoint(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }
}
