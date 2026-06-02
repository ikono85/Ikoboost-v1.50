using System.Windows;
using System.Windows.Media;

namespace IkoboostWpf.Controls;

public sealed class HealthRingControl : FrameworkElement
{
    public static readonly DependencyProperty ScoreProperty =
        DependencyProperty.Register(nameof(Score), typeof(double), typeof(HealthRingControl),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty RingBrushProperty =
        DependencyProperty.Register(nameof(RingBrush), typeof(Brush), typeof(HealthRingControl),
            new FrameworkPropertyMetadata(Brushes.Cyan, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty CompactProperty =
        DependencyProperty.Register(nameof(Compact), typeof(bool), typeof(HealthRingControl),
            new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Score
    {
        get => (double)GetValue(ScoreProperty);
        set => SetValue(ScoreProperty, value);
    }

    public Brush RingBrush
    {
        get => (Brush)GetValue(RingBrushProperty);
        set => SetValue(RingBrushProperty, value);
    }

    public bool Compact
    {
        get => (bool)GetValue(CompactProperty);
        set => SetValue(CompactProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        const double thickness = 10;
        var cx = w / 2;
        var cy = h / 2;
        var radius = Math.Min(w, h) / 2 - thickness / 2 - 2;
        if (radius <= 0) return;

        // Track
        var trackBrush = Application.Current?.TryFindResource("SurfaceAltBrush") as Brush
            ?? new SolidColorBrush(Color.FromRgb(22, 28, 38));
        var trackPen = new Pen(trackBrush, thickness);
        trackPen.Freeze();
        dc.DrawEllipse(null, trackPen, new Point(cx, cy), radius, radius);

        // Arc
        var frac = Math.Clamp(Score / 100.0, 0, 1);
        if (frac > 0)
        {
            var pen = new Pen(RingBrush ?? Brushes.Cyan, thickness)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round,
            };
            pen.Freeze();

            if (frac >= 0.999)
            {
                dc.DrawEllipse(null, pen, new Point(cx, cy), radius, radius);
            }
            else
            {
                var angle = frac * 360.0;
                var start = CirclePoint(cx, cy, radius, -90);
                var end = CirclePoint(cx, cy, radius, -90 + angle);
                var fig = new PathFigure { StartPoint = start, IsClosed = false };
                fig.Segments.Add(new ArcSegment(end, new Size(radius, radius), 0,
                    angle > 180, SweepDirection.Clockwise, true));
                var geo = new PathGeometry(new[] { fig });
                geo.Freeze();
                dc.DrawGeometry(null, pen, geo);
            }
        }

        var score = (int)Math.Round(Score);
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var scoreText = new FormattedText(score.ToString(),
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            Compact ? Math.Max(13, radius * 0.62) : Math.Max(18, radius * 0.58),
            RingBrush ?? Brushes.Cyan,
            dpi);

        if (Compact)
        {
            dc.DrawText(scoreText, new Point(cx - scoreText.Width / 2, cy - scoreText.Height / 2));
            return;
        }

        var percentText = new FormattedText("%",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Consolas"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            Math.Max(10, radius * 0.28),
            Application.Current?.TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.LightGray,
            dpi);

        var labelText = new FormattedText("SANTE",
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
            Math.Max(8, radius * 0.18),
            Application.Current?.TryFindResource("TextSecondaryBrush") as Brush ?? Brushes.LightGray,
            dpi);

        var numberWidth = scoreText.Width + percentText.Width;
        var numberLeft = cx - numberWidth / 2;
        var numberTop = cy - scoreText.Height * 0.58;

        dc.DrawText(scoreText, new Point(numberLeft, numberTop));
        dc.DrawText(percentText, new Point(numberLeft + scoreText.Width + 1, numberTop + scoreText.Height * 0.42));
        dc.DrawText(labelText, new Point(cx - labelText.Width / 2, cy + scoreText.Height * 0.28));
    }

    private static Point CirclePoint(double cx, double cy, double r, double angleDeg)
    {
        var rad = angleDeg * Math.PI / 180.0;
        return new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }
}
