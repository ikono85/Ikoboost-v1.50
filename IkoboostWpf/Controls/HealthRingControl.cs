// Aliases: project has UseWindowsForms=true, so several types collide with WPF equivalents.
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;
using WpfSize = System.Windows.Size;

using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace IkoboostWpf.Controls;

/// <summary>
/// Anneau circulaire de progression affichant un score de 0 à 100.
/// Utilise OnRender pour éviter toute dépendance externe.
/// </summary>
public sealed class HealthRingControl : FrameworkElement
{
    // ── Dependency Properties ─────────────────────────────────────────────

    public static readonly DependencyProperty ScoreProperty =
        DependencyProperty.Register(
            nameof(Score), typeof(int), typeof(HealthRingControl),
            new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingBrushProperty =
        DependencyProperty.Register(
            nameof(RingBrush), typeof(WpfBrush), typeof(HealthRingControl),
            new FrameworkPropertyMetadata(WpfBrushes.LightGreen, FrameworkPropertyMetadataOptions.AffectsRender));

    public int Score
    {
        get => (int)GetValue(ScoreProperty);
        set => SetValue(ScoreProperty, value);
    }

    public WpfBrush RingBrush
    {
        get => (WpfBrush)GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    // ── Render ────────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        const double strokeThickness = 9;
        const double padding = strokeThickness / 2 + 2;

        var cx = w / 2;
        var cy = h / 2;
        var radius = Math.Min(w, h) / 2 - padding;

        // ── Track (cercle complet, couleur atténuée) ──────────────────────
        var trackColor = System.Windows.Application.Current?.TryFindResource("BorderBrush")
                             is WpfBrush tb ? tb : new SolidColorBrush(WpfColor.FromArgb(50, 128, 128, 128));

        var trackPen = new WpfPen(trackColor, strokeThickness);
        trackPen.Freeze();
        dc.DrawEllipse(null, trackPen, new WpfPoint(cx, cy), radius, radius);

        // ── Arc de progression ────────────────────────────────────────────
        var score = Math.Clamp(Score, 0, 100);
        var arcBrush = RingBrush ?? WpfBrushes.LightGreen;

        if (score > 0)
        {
            var arcPen = new WpfPen(arcBrush, strokeThickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            };
            arcPen.Freeze();

            if (score >= 100)
            {
                // Cercle complet : DrawEllipse suffit
                dc.DrawEllipse(null, arcPen, new WpfPoint(cx, cy), radius, radius);
            }
            else
            {
                var angle = score / 100.0 * 360.0;
                var startPoint = CirclePoint(cx, cy, radius, -90);
                var endPoint = CirclePoint(cx, cy, radius, -90 + angle);

                var figure = new PathFigure { StartPoint = startPoint, IsClosed = false };
                figure.Segments.Add(new ArcSegment(
                    endPoint,
                    new WpfSize(radius, radius),
                    0,
                    angle > 180,
                    SweepDirection.Clockwise,
                    isStroked: true));

                var geometry = new PathGeometry([figure]);
                dc.DrawGeometry(null, arcPen, geometry);
            }
        }

        // ── Texte central (score) ─────────────────────────────────────────
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var fontSize = radius * 0.52;

        var typeface = new Typeface(
            new WpfFontFamily("Segoe UI"),
            FontStyles.Normal,
            FontWeights.Bold,
            FontStretches.Normal);

        var ft = new FormattedText(
            score.ToString(),
            CultureInfo.CurrentCulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            fontSize,
            arcBrush,
            dpi);

        dc.DrawText(ft, new WpfPoint(cx - ft.Width / 2, cy - ft.Height / 2));
    }

    // ── Helper ────────────────────────────────────────────────────────────

    private static WpfPoint CirclePoint(double cx, double cy, double r, double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180.0;
        return new WpfPoint(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }
}
