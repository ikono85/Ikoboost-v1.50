using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace IkoboostWpf.Controls;

public sealed class SparklineControl : FrameworkElement
{
    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(nameof(Values), typeof(IEnumerable<double>), typeof(SparklineControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineBrushProperty =
        DependencyProperty.Register(nameof(LineBrush), typeof(Brush), typeof(SparklineControl),
            new FrameworkPropertyMetadata(Brushes.Cyan, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(nameof(MaxValue), typeof(double), typeof(SparklineControl),
            new FrameworkPropertyMetadata(100.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<double>? Values
    {
        get => (IEnumerable<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);

        var values = Values?.ToList();
        if (values == null || values.Count < 2) return;

        var w = ActualWidth;
        var h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        var max = MaxValue > 0 ? MaxValue : (values.Max() is > 0 ? values.Max() : 1);
        var n = values.Count;
        var xStep = w / (n - 1);

        double X(int i) => i * xStep;
        double Y(double v) => h - Math.Clamp(v / max, 0, 1) * h;

        // Build path
        var figure = new PathFigure { StartPoint = new Point(X(0), Y(values[0])), IsFilled = true };
        for (int i = 1; i < n; i++)
            figure.Segments.Add(new LineSegment(new Point(X(i), Y(values[i])), isStroked: true));

        // Close fill area
        var fillFigure = new PathFigure { StartPoint = new Point(X(0), h), IsFilled = true };
        fillFigure.Segments.Add(new LineSegment(new Point(X(0), Y(values[0])), isStroked: false));
        for (int i = 1; i < n; i++)
            fillFigure.Segments.Add(new LineSegment(new Point(X(i), Y(values[i])), isStroked: false));
        fillFigure.Segments.Add(new LineSegment(new Point(X(n - 1), h), isStroked: false));
        fillFigure.IsClosed = true;

        // Draw gradient fill
        var fillBrush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
        };

        var baseColor = LineBrush is SolidColorBrush scb ? scb.Color : Colors.Cyan;
        fillBrush.GradientStops.Add(new GradientStop(Color.FromArgb(50, baseColor.R, baseColor.G, baseColor.B), 0));
        fillBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, baseColor.R, baseColor.G, baseColor.B), 1));
        fillBrush.Freeze();

        var fillGeo = new PathGeometry(new[] { fillFigure });
        fillGeo.Freeze();
        dc.DrawGeometry(fillBrush, null, fillGeo);

        // Draw line
        var lineGeo = new PathGeometry(new[] { figure });
        lineGeo.Freeze();
        var pen = new Pen(LineBrush, 1.5) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        dc.DrawGeometry(null, pen, lineGeo);
    }
}
