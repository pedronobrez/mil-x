using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace OpenDIAL.Desktop.Controls;

/// <summary>
/// Line chart for chromatograms (EIC/TIC). Points must be sorted by X. Supports a highlighted
/// X range (the integrated peak), a vertical marker (the apex) and a nearest-point tooltip.
/// </summary>
public sealed class LineChart : ChartBase
{
    public static readonly StyledProperty<IReadOnlyList<Point>?> PointsProperty = AvaloniaProperty.Register<LineChart, IReadOnlyList<Point>?>(nameof(Points));
    public static readonly StyledProperty<double> HighlightStartProperty = AvaloniaProperty.Register<LineChart, double>(nameof(HighlightStart), double.NaN);
    public static readonly StyledProperty<double> HighlightEndProperty = AvaloniaProperty.Register<LineChart, double>(nameof(HighlightEnd), double.NaN);
    public static readonly StyledProperty<double> MarkerXProperty = AvaloniaProperty.Register<LineChart, double>(nameof(MarkerX), double.NaN);
    public static readonly StyledProperty<bool> FillAreaProperty = AvaloniaProperty.Register<LineChart, bool>(nameof(FillArea), true);

    static LineChart()
    {
        AffectsRender<LineChart>(PointsProperty, HighlightStartProperty, HighlightEndProperty, MarkerXProperty, FillAreaProperty);
    }

    public LineChart()
    {
        XLabel = "Retention time (min)";
        YLabel = "Intensity";
    }

    public IReadOnlyList<Point>? Points { get => GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public double HighlightStart { get => GetValue(HighlightStartProperty); set => SetValue(HighlightStartProperty, value); }
    public double HighlightEnd { get => GetValue(HighlightEndProperty); set => SetValue(HighlightEndProperty, value); }
    public double MarkerX { get => GetValue(MarkerXProperty); set => SetValue(MarkerXProperty, value); }
    public bool FillArea { get => GetValue(FillAreaProperty); set => SetValue(FillAreaProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PointsProperty)
        {
            ViewXMin = double.NaN;
            ViewXMax = double.NaN;
            // zoom to the highlighted peak (with context) when a peak range is given
            if (!double.IsNaN(HighlightStart) && !double.IsNaN(HighlightEnd) && HighlightEnd > HighlightStart)
            {
                var width = HighlightEnd - HighlightStart;
                var (min, max) = DataXExtent();
                if (!double.IsNaN(min))
                {
                    ViewXMin = Math.Max(min, HighlightStart - width * 2);
                    ViewXMax = Math.Min(max, HighlightEnd + width * 2);
                }
            }
        }
    }

    protected override (double Min, double Max) DataXExtent()
    {
        var pts = Points;
        if (pts is null || pts.Count == 0) return (double.NaN, double.NaN);
        return (pts[0].X, pts[^1].X);
    }

    protected override (double Min, double Max) DataYExtent(double xMin, double xMax)
    {
        var pts = Points;
        if (pts is null || pts.Count == 0) return (0, 1);
        double max = 0;
        var (lo, hi) = VisibleRange(pts, xMin, xMax);
        for (var i = lo; i <= hi; i++)
        {
            if (pts[i].Y > max) max = pts[i].Y;
        }
        return (0, max <= 0 ? 1 : max);
    }

    private static (int Lo, int Hi) VisibleRange(IReadOnlyList<Point> pts, double xMin, double xMax)
    {
        var lo = LowerBound(pts, xMin);
        var hi = LowerBound(pts, xMax);
        lo = Math.Max(0, lo - 1);
        hi = Math.Min(pts.Count - 1, hi + 1);
        return (lo, hi);
    }

    private static int LowerBound(IReadOnlyList<Point> pts, double x)
    {
        int left = 0, right = pts.Count;
        while (left < right)
        {
            var mid = (left + right) >> 1;
            if (pts[mid].X < x) left = mid + 1; else right = mid;
        }
        return left;
    }

    protected override void RenderPlot(DrawingContext ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, double xMin, double xMax, double yMin, double yMax)
    {
        var pts = Points!;
        var (lo, hi) = VisibleRange(pts, xMin, xMax);

        if (!double.IsNaN(HighlightStart) && !double.IsNaN(HighlightEnd) && HighlightEnd > HighlightStart)
        {
            var x0 = tx(HighlightStart);
            var x1 = tx(HighlightEnd);
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(IsDark ? (byte)70 : (byte)45, 0xF2, 0x8E, 0x2B)), null, new Rect(x0, plot.Y, Math.Max(1, x1 - x0), plot.Height));
        }

        var accent = AccentColor;
        if (hi >= lo)
        {
            var line = new StreamGeometry();
            using (var g = line.Open())
            {
                g.BeginFigure(new Point(tx(pts[lo].X), ty(pts[lo].Y)), false);
                for (var i = lo + 1; i <= hi; i++)
                {
                    g.LineTo(new Point(tx(pts[i].X), ty(pts[i].Y)));
                }
                g.EndFigure(false);
            }
            if (FillArea)
            {
                var area = new StreamGeometry();
                using (var g = area.Open())
                {
                    g.BeginFigure(new Point(tx(pts[lo].X), ty(0)), true);
                    for (var i = lo; i <= hi; i++)
                    {
                        g.LineTo(new Point(tx(pts[i].X), ty(pts[i].Y)));
                    }
                    g.LineTo(new Point(tx(pts[hi].X), ty(0)));
                    g.EndFigure(true);
                }
                ctx.DrawGeometry(new SolidColorBrush(Color.FromArgb(40, accent.R, accent.G, accent.B)), null, area);
            }
            ctx.DrawGeometry(null, new Pen(new SolidColorBrush(accent), 1.5), line);

            // draw points when zoomed in far enough
            if (plot.Width / Math.Max(1, hi - lo) > 12)
            {
                var brush = new SolidColorBrush(accent);
                for (var i = lo; i <= hi; i++)
                {
                    ctx.DrawEllipse(brush, null, new Point(tx(pts[i].X), ty(pts[i].Y)), 2.2, 2.2);
                }
            }
        }

        if (!double.IsNaN(MarkerX))
        {
            var mx = tx(MarkerX);
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.Parse("#E15759")), 1, dashStyle: DashStyle.Dash), new Point(mx, plot.Y), new Point(mx, plot.Bottom));
        }
    }

    protected override IReadOnlyList<string>? GetTooltip(Point pos, Rect plot, Func<double, double> tx, Func<double, double> ty)
    {
        var pts = Points;
        if (pts is null || pts.Count == 0) return null;
        var (min, max) = DataXExtent();
        var xMin = double.IsNaN(ViewXMin) ? min : ViewXMin;
        var xMax = double.IsNaN(ViewXMax) ? max : ViewXMax;
        var x = xMin + (pos.X - plot.X) / plot.Width * (xMax - xMin);
        var i = LowerBound(pts, x);
        if (i > 0 && (i >= pts.Count || Math.Abs(pts[i - 1].X - x) < Math.Abs(pts[i].X - x))) i--;
        if (i < 0 || i >= pts.Count) return null;
        var p = pts[i];
        return new[]
        {
            $"RT {p.X.ToString("F3", CultureInfo.InvariantCulture)} min",
            $"Intensity {FormatIntensity(p.Y)}",
        };
    }
}
