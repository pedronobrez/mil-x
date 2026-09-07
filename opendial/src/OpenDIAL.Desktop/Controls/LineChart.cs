using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace OpenDIAL.Desktop.Controls;

/// <summary>One chromatogram trace. Points must be sorted by X. A null colour picks the palette by index.</summary>
public sealed record TraceSeries(string Label, IReadOnlyList<Point> Points, Color? Color = null, bool Fill = false, bool Dashed = false);

public enum WindowKind
{
    /// <summary>Expected retention-time window (accent, very light).</summary>
    Expected,
    /// <summary>Integration range of a peak (warm, translucent).</summary>
    Integration,
    /// <summary>Anything else (neutral).</summary>
    Neutral,
}

/// <summary>A shaded X interval.</summary>
public sealed record ShadedWindow(double Start, double End, WindowKind Kind = WindowKind.Neutral, string? Label = null);

/// <summary>
/// Line chart for chromatograms (TIC / BPC / EIC). Draws either a single <see cref="Points"/> trace or an overlay
/// of <see cref="Series"/>, with legend, shaded windows, stack / normalise modes, apex RT labels, a marker line,
/// Shift+drag range selection and drag-to-zoom (from <see cref="ChartBase"/>).
/// </summary>
public sealed class LineChart : ChartBase
{
    public static readonly StyledProperty<IReadOnlyList<Point>?> PointsProperty = AvaloniaProperty.Register<LineChart, IReadOnlyList<Point>?>(nameof(Points));
    public static readonly StyledProperty<IReadOnlyList<TraceSeries>?> SeriesProperty = AvaloniaProperty.Register<LineChart, IReadOnlyList<TraceSeries>?>(nameof(Series));
    public static readonly StyledProperty<IReadOnlyList<ShadedWindow>?> WindowsProperty = AvaloniaProperty.Register<LineChart, IReadOnlyList<ShadedWindow>?>(nameof(Windows));
    public static readonly StyledProperty<double> HighlightStartProperty = AvaloniaProperty.Register<LineChart, double>(nameof(HighlightStart), double.NaN);
    public static readonly StyledProperty<double> HighlightEndProperty = AvaloniaProperty.Register<LineChart, double>(nameof(HighlightEnd), double.NaN);
    public static readonly StyledProperty<double> MarkerXProperty = AvaloniaProperty.Register<LineChart, double>(nameof(MarkerX), double.NaN);
    public static readonly StyledProperty<bool> FillAreaProperty = AvaloniaProperty.Register<LineChart, bool>(nameof(FillArea), true);
    public static readonly StyledProperty<bool> StackProperty = AvaloniaProperty.Register<LineChart, bool>(nameof(Stack));
    public static readonly StyledProperty<bool> NormalizeProperty = AvaloniaProperty.Register<LineChart, bool>(nameof(Normalize));
    public static readonly StyledProperty<bool> ShowLegendProperty = AvaloniaProperty.Register<LineChart, bool>(nameof(ShowLegend), true);
    public static readonly StyledProperty<bool> ShowRtLabelsProperty = AvaloniaProperty.Register<LineChart, bool>(nameof(ShowRtLabels));
    public static readonly StyledProperty<bool> AutoZoomToHighlightProperty = AvaloniaProperty.Register<LineChart, bool>(nameof(AutoZoomToHighlight), true);

    static LineChart()
    {
        AffectsRender<LineChart>(PointsProperty, SeriesProperty, WindowsProperty, HighlightStartProperty, HighlightEndProperty, MarkerXProperty, FillAreaProperty,
            StackProperty, NormalizeProperty, ShowLegendProperty, ShowRtLabelsProperty);
    }

    public LineChart()
    {
        XLabel = "Retention time (min)";
        YLabel = "Intensity";
    }

    public IReadOnlyList<Point>? Points { get => GetValue(PointsProperty); set => SetValue(PointsProperty, value); }
    public IReadOnlyList<TraceSeries>? Series { get => GetValue(SeriesProperty); set => SetValue(SeriesProperty, value); }
    public IReadOnlyList<ShadedWindow>? Windows { get => GetValue(WindowsProperty); set => SetValue(WindowsProperty, value); }
    public double HighlightStart { get => GetValue(HighlightStartProperty); set => SetValue(HighlightStartProperty, value); }
    public double HighlightEnd { get => GetValue(HighlightEndProperty); set => SetValue(HighlightEndProperty, value); }
    public double MarkerX { get => GetValue(MarkerXProperty); set => SetValue(MarkerXProperty, value); }
    public bool FillArea { get => GetValue(FillAreaProperty); set => SetValue(FillAreaProperty, value); }
    /// <summary>Draws each series in its own horizontal band.</summary>
    public bool Stack { get => GetValue(StackProperty); set => SetValue(StackProperty, value); }
    /// <summary>Scales every series to 100 %.</summary>
    public bool Normalize { get => GetValue(NormalizeProperty); set => SetValue(NormalizeProperty, value); }
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    /// <summary>Labels the apex of each series in view with its retention time.</summary>
    public bool ShowRtLabels { get => GetValue(ShowRtLabelsProperty); set => SetValue(ShowRtLabelsProperty, value); }
    /// <summary>Zoom to the highlight range (with context) whenever the data changes.</summary>
    public bool AutoZoomToHighlight { get => GetValue(AutoZoomToHighlightProperty); set => SetValue(AutoZoomToHighlightProperty, value); }

    private IReadOnlyList<TraceSeries> Effective()
    {
        if (Series is { Count: > 0 } s) return s;
        if (Points is { Count: > 0 } p) return new[] { new TraceSeries(Title ?? "trace", p, null, FillArea) };
        return Array.Empty<TraceSeries>();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PointsProperty || change.Property == SeriesProperty || change.Property == StackProperty || change.Property == NormalizeProperty)
        {
            ViewXMin = double.NaN;
            ViewXMax = double.NaN;
            if (AutoZoomToHighlight && !double.IsNaN(HighlightStart) && !double.IsNaN(HighlightEnd) && HighlightEnd > HighlightStart && double.IsNaN(FixedXMin))
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
        var series = Effective();
        double min = double.MaxValue, max = double.MinValue;
        foreach (var s in series)
        {
            if (s.Points.Count == 0) continue;
            min = Math.Min(min, s.Points[0].X);
            max = Math.Max(max, s.Points[^1].X);
        }
        if (min == double.MaxValue) return (double.NaN, double.NaN);
        return (min, max);
    }

    private double SeriesScale(TraceSeries s)
    {
        if (!Normalize && !Stack) return 1;
        double max = 0;
        foreach (var p in s.Points) if (p.Y > max) max = p.Y;
        return max <= 0 ? 1 : 100.0 / max;
    }

    protected override (double Min, double Max) DataYExtent(double xMin, double xMax)
    {
        var series = Effective();
        if (series.Count == 0) return (0, 1);
        if (Stack) return (0, 100.0 * series.Count);
        double max = 0;
        foreach (var s in series)
        {
            var scale = SeriesScale(s);
            var (lo, hi) = VisibleRange(s.Points, xMin, xMax);
            for (var i = lo; i <= hi; i++)
            {
                var y = s.Points[i].Y * scale;
                if (y > max) max = y;
            }
        }
        return (0, max <= 0 ? 1 : max);
    }

    private static (int Lo, int Hi) VisibleRange(IReadOnlyList<Point> pts, double xMin, double xMax)
    {
        if (pts.Count == 0) return (0, -1);
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
        DrawWindows(ctx, plot, tx);
        var series = Effective();
        var n = series.Count;
        var labels = new List<(double X, double Y, string Text, Color Color)>();
        for (var k = 0; k < n; k++)
        {
            var s = series[k];
            var pts = s.Points;
            if (pts.Count == 0) continue;
            var color = s.Color ?? SeriesColor(k);
            var scale = SeriesScale(s);
            var offset = Stack ? 100.0 * (n - 1 - k) : 0;
            var (lo, hi) = VisibleRange(pts, xMin, xMax);
            if (hi < lo) continue;

            double Y(int i) => ty(pts[i].Y * scale + offset);
            var line = new StreamGeometry();
            using (var g = line.Open())
            {
                g.BeginFigure(new Point(tx(pts[lo].X), Y(lo)), false);
                for (var i = lo + 1; i <= hi; i++) g.LineTo(new Point(tx(pts[i].X), Y(i)));
                g.EndFigure(false);
            }
            if (s.Fill || (n == 1 && FillArea))
            {
                var area = new StreamGeometry();
                using (var g = area.Open())
                {
                    g.BeginFigure(new Point(tx(pts[lo].X), ty(offset)), true);
                    for (var i = lo; i <= hi; i++) g.LineTo(new Point(tx(pts[i].X), Y(i)));
                    g.LineTo(new Point(tx(pts[hi].X), ty(offset)));
                    g.EndFigure(true);
                }
                ctx.DrawGeometry(new SolidColorBrush(Color.FromArgb(n == 1 ? (byte)34 : (byte)18, color.R, color.G, color.B)), null, area);
            }
            var pen = new Pen(new SolidColorBrush(color), Compact ? 1.2 : 1.5, s.Dashed ? DashStyle.Dash : null);
            ctx.DrawGeometry(null, pen, line);

            if (!Compact && plot.Width / Math.Max(1, hi - lo) > 12)
            {
                var brush = new SolidColorBrush(color);
                for (var i = lo; i <= hi; i++) ctx.DrawEllipse(brush, null, new Point(tx(pts[i].X), Y(i)), 2, 2);
            }
            if (Stack)
            {
                var baseline = ty(offset);
                ctx.DrawLine(new Pen(GridBrush, 1), new Point(plot.X, baseline), new Point(plot.Right, baseline));
                var ft = MakeText(s.Label, 10, new SolidColorBrush(color));
                ctx.DrawText(ft, new Point(plot.X + 6, ty(offset + 100) + 2));
            }
            if (ShowRtLabels)
            {
                var best = lo;
                for (var i = lo; i <= hi; i++) if (pts[i].Y > pts[best].Y) best = i;
                if (pts[best].Y > 0) labels.Add((tx(pts[best].X), Y(best), pts[best].X.ToString("F2", CultureInfo.InvariantCulture), color));
            }
        }

        if (!double.IsNaN(HighlightStart) && !double.IsNaN(HighlightEnd) && HighlightEnd > HighlightStart)
        {
            var x0 = tx(HighlightStart);
            var x1 = tx(HighlightEnd);
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(IsDark ? (byte)60 : (byte)40, 0xF2, 0x8E, 0x2B)), null, new Rect(x0, plot.Y, Math.Max(1, x1 - x0), plot.Height));
        }
        if (!double.IsNaN(MarkerX))
        {
            var mx = tx(MarkerX);
            ctx.DrawLine(new Pen(new SolidColorBrush(DangerColor), 1, dashStyle: DashStyle.Dash), new Point(mx, plot.Y), new Point(mx, plot.Bottom));
        }
        foreach (var (x, y, text, color) in labels)
        {
            var ft = MakeText(text, 9.5, new SolidColorBrush(color));
            ctx.DrawText(ft, new Point(Math.Clamp(x - ft.Width / 2, plot.X, plot.Right - ft.Width), Math.Max(plot.Y, y - ft.Height - 2)));
        }
    }

    private void DrawWindows(DrawingContext ctx, Rect plot, Func<double, double> tx)
    {
        var windows = Windows;
        if (windows is null) return;
        foreach (var w in windows)
        {
            if (double.IsNaN(w.Start) || double.IsNaN(w.End)) continue;
            var x0 = tx(Math.Min(w.Start, w.End));
            var x1 = tx(Math.Max(w.Start, w.End));
            IBrush fill = w.Kind switch
            {
                WindowKind.Expected => new SolidColorBrush(Color.FromArgb(IsDark ? (byte)120 : (byte)150, AccentSoftColor.R, AccentSoftColor.G, AccentSoftColor.B)),
                WindowKind.Integration => new SolidColorBrush(Color.FromArgb(IsDark ? (byte)34 : (byte)26, 0xF2, 0x8E, 0x2B)),
                _ => new SolidColorBrush(Color.FromArgb(40, MutedColor.R, MutedColor.G, MutedColor.B)),
            };
            ctx.DrawRectangle(fill, null, new Rect(x0, plot.Y, Math.Max(1, x1 - x0), plot.Height));
            if (w.Kind == WindowKind.Expected)
            {
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(120, AccentColor.R, AccentColor.G, AccentColor.B)), 1, dashStyle: DashStyle.Dot);
                ctx.DrawLine(pen, new Point(x0, plot.Y), new Point(x0, plot.Bottom));
                ctx.DrawLine(pen, new Point(x1, plot.Y), new Point(x1, plot.Bottom));
            }
            else if (w.Kind == WindowKind.Integration)
            {
                var pen = new Pen(new SolidColorBrush(Color.FromArgb(150, 0xF2, 0x8E, 0x2B)), 1);
                ctx.DrawLine(pen, new Point(x0, plot.Y), new Point(x0, plot.Bottom));
                ctx.DrawLine(pen, new Point(x1, plot.Y), new Point(x1, plot.Bottom));
            }
            if (!string.IsNullOrEmpty(w.Label) && !Compact)
            {
                var ft = MakeText(w.Label!, 9.5, MutedBrush);
                ctx.DrawText(ft, new Point(x0 + 3, plot.Bottom - ft.Height - 2));
            }
        }
    }

    protected override void RenderOverlay(DrawingContext ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, double xMin, double xMax)
    {
        var series = Series;
        if (!ShowLegend || Compact || series is null || series.Count < 1 || Stack) return;
        if (series.Count == 1 && string.IsNullOrEmpty(series[0].Label)) return;
        DrawLegend(ctx, plot, series.Select((s, i) => (s.Label, s.Color ?? SeriesColor(i))).ToList());
    }

    protected override IReadOnlyList<string>? GetTooltip(Point pos, Rect plot, Func<double, double> tx, Func<double, double> ty)
    {
        var series = Effective();
        if (series.Count == 0) return null;
        var x = XAt(pos, plot);
        // nearest point of the series whose curve is closest to the cursor
        TraceSeries? bestSeries = null;
        var bestI = -1;
        var bestD = double.MaxValue;
        var n = series.Count;
        for (var k = 0; k < n; k++)
        {
            var pts = series[k].Points;
            if (pts.Count == 0) continue;
            var i = LowerBound(pts, x);
            if (i > 0 && (i >= pts.Count || Math.Abs(pts[i - 1].X - x) < Math.Abs(pts[i].X - x))) i--;
            if (i < 0 || i >= pts.Count) continue;
            var scale = SeriesScale(series[k]);
            var offset = Stack ? 100.0 * (n - 1 - k) : 0;
            var d = Math.Abs(ty(pts[i].Y * scale + offset) - pos.Y);
            if (d < bestD) { bestD = d; bestSeries = series[k]; bestI = i; }
        }
        if (bestSeries is null) return null;
        var p = bestSeries.Points[bestI];
        var lines = new List<string>();
        if (n > 1 && !string.IsNullOrEmpty(bestSeries.Label)) lines.Add(bestSeries.Label);
        lines.Add($"RT {p.X.ToString("F3", CultureInfo.InvariantCulture)} min");
        lines.Add($"Intensity {FormatIntensity(p.Y)}");
        return lines;
    }
}
