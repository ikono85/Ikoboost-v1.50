using System.Collections.Specialized;
using System.Windows;
using System.Windows.Media;

// The project references both System.Windows.Forms and System.Windows.Media,
// so several WPF types collide with System.Drawing equivalents in implicit usings.
// We alias them here to stay unambiguous without polluting other files.
using WpfBrush = System.Windows.Media.Brush;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;

namespace IkoboostWpf.Controls;

/// <summary>
/// Lightweight sparkline — renders a scaled area+line from a sequence of doubles.
/// No third-party library required.
/// Redraws automatically whenever the bound INotifyCollectionChanged collection changes.
///
/// Usage (XAML):
///   &lt;controls:SparklineControl Values="{Binding CpuHistory}"
///                              LineBrush="{DynamicResource AccentBrush}"
///                              MaxValue="100"
///                              Height="48"/&gt;
///
/// MaxValue: fixed Y ceiling (e.g. 100 for % metrics).
///           When 0 (default), auto-scales to the maximum in the current window.
/// </summary>
public sealed class SparklineControl : FrameworkElement
{
    // ── Dependency Properties ─────────────────────────────────────────────

    public static readonly DependencyProperty ValuesProperty =
        DependencyProperty.Register(
            nameof(Values), typeof(IEnumerable<double>), typeof(SparklineControl),
            new FrameworkPropertyMetadata(null, OnValuesChanged));

    public static readonly DependencyProperty LineBrushProperty =
        DependencyProperty.Register(
            nameof(LineBrush), typeof(WpfBrush), typeof(SparklineControl),
            new FrameworkPropertyMetadata(WpfBrushes.Cyan,
                FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Fixed Y-axis ceiling. Set to 100 for percentage metrics (CPU, RAM, GPU).
    /// When 0 (default), auto-scales to the maximum value in the current window.
    /// </summary>
    public static readonly DependencyProperty MaxValueProperty =
        DependencyProperty.Register(
            nameof(MaxValue), typeof(double), typeof(SparklineControl),
            new FrameworkPropertyMetadata(0.0,
                FrameworkPropertyMetadataOptions.AffectsRender));

    public IEnumerable<double>? Values
    {
        get => (IEnumerable<double>?)GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public WpfBrush LineBrush
    {
        get => (WpfBrush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }

    public double MaxValue
    {
        get => (double)GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    // ── Collection change wiring ──────────────────────────────────────────

    private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctrl = (SparklineControl)d;
        if (e.OldValue is INotifyCollectionChanged oldCol)
            oldCol.CollectionChanged -= ctrl.OnCollectionChanged;
        if (e.NewValue is INotifyCollectionChanged newCol)
            newCol.CollectionChanged += ctrl.OnCollectionChanged;
        ctrl.InvalidateVisual();
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => InvalidateVisual();

    // ── Rendering ────────────────────────────────────────────────────────

    protected override void OnRender(DrawingContext dc)
    {
        var pts = Values?.ToList();
        if (pts is null || pts.Count < 2) return;

        double w = ActualWidth;
        double h = ActualHeight;
        if (w <= 0 || h <= 0) return;

        // Leave 2px at top so the line is never clipped at the boundary
        const double topPad = 2.0;
        double drawH = h - topPad;

        double ceiling = MaxValue > 0 ? MaxValue : Math.Max(pts.Max(), 1.0);

        WpfPoint Pt(int i, double v)
            => new(w * i / (pts.Count - 1),
                   drawH - Math.Clamp(v / ceiling, 0.0, 1.0) * drawH + topPad);

        // ── Filled area under the curve ───────────────────────────────
        var fillGeo = new StreamGeometry();
        using (var ctx = fillGeo.Open())
        {
            ctx.BeginFigure(Pt(0, pts[0]), isFilled: true, isClosed: true);
            for (int i = 1; i < pts.Count; i++)
                ctx.LineTo(Pt(i, pts[i]), isStroked: false, isSmoothJoin: false);
            // Close the polygon: down to bottom-right, across to bottom-left.
            // isClosed: true draws back to the starting point automatically.
            ctx.LineTo(new WpfPoint(w, h), isStroked: false, isSmoothJoin: false);
            ctx.LineTo(new WpfPoint(0, h), isStroked: false, isSmoothJoin: false);
        }
        fillGeo.Freeze();

        dc.PushOpacity(0.15);
        dc.DrawGeometry(LineBrush, null, fillGeo);
        dc.Pop();

        // ── Stroke line ───────────────────────────────────────────────
        var lineGeo = new StreamGeometry();
        using (var ctx = lineGeo.Open())
        {
            ctx.BeginFigure(Pt(0, pts[0]), isFilled: false, isClosed: false);
            for (int i = 1; i < pts.Count; i++)
                ctx.LineTo(Pt(i, pts[i]), isStroked: true, isSmoothJoin: true);
        }
        lineGeo.Freeze();

        var pen = new WpfPen(LineBrush, 1.5) { LineJoin = PenLineJoin.Round };
        pen.Freeze();
        dc.DrawGeometry(null, pen, lineGeo);
    }
}
