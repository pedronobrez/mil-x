using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace OpenDIAL.Desktop.Controls;

/// <summary>One point of a metric plot; <see cref="Group"/> picks the colour, <see cref="Tag"/> travels back on click.</summary>
public sealed record ScatterPoint(double X, double Y, string Label, string Group, object? Tag = null)
{
    /// <summary>Drawn with its label whatever the chart's setting: the points worth naming.</summary>
    public bool Labelled { get; init; }
}

/// <summary>A line drawn across the plot at a fixed value: a threshold.</summary>
public sealed record ReferenceLine(double Value, bool Vertical, string? Label = null);

/// <summary>
/// Scatter plot with categorical colouring, legend, hover tooltip, click selection, threshold
/// lines, point labels and the 95 % confidence ellipse of each group — the one chart that serves
/// as a score plot, a volcano, a loadings plot and a metric plot.
/// </summary>
public sealed class ScatterChart : ChartBase
{
    public static readonly StyledProperty<IReadOnlyList<ScatterPoint>?> ItemsProperty = AvaloniaProperty.Register<ScatterChart, IReadOnlyList<ScatterPoint>?>(nameof(Items));
    public static readonly StyledProperty<ScatterPoint?> SelectedItemProperty = AvaloniaProperty.Register<ScatterChart, ScatterPoint?>(nameof(SelectedItem), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<bool> ConnectProperty = AvaloniaProperty.Register<ScatterChart, bool>(nameof(Connect));
    public static readonly StyledProperty<IReadOnlyList<ReferenceLine>?> ReferenceLinesProperty = AvaloniaProperty.Register<ScatterChart, IReadOnlyList<ReferenceLine>?>(nameof(ReferenceLines));
    public static readonly StyledProperty<bool> ShowEllipsesProperty = AvaloniaProperty.Register<ScatterChart, bool>(nameof(ShowEllipses));
    public static readonly StyledProperty<bool> ShowLegendProperty = AvaloniaProperty.Register<ScatterChart, bool>(nameof(ShowLegend), true);
    public static readonly StyledProperty<bool> YFromZeroProperty = AvaloniaProperty.Register<ScatterChart, bool>(nameof(YFromZero), true);
    public static readonly StyledProperty<IReadOnlyDictionary<string, Color>?> GroupColorOverridesProperty = AvaloniaProperty.Register<ScatterChart, IReadOnlyDictionary<string, Color>?>(nameof(GroupColorOverrides));

    static ScatterChart()
    {
        AffectsRender<ScatterChart>(ItemsProperty, SelectedItemProperty, ConnectProperty, ReferenceLinesProperty, ShowEllipsesProperty, ShowLegendProperty, YFromZeroProperty, GroupColorOverridesProperty);
    }

    public ScatterChart()
    {
        XLabel = "Order";
        YLabel = "Value";
        PointClicked += (_, e) => SelectNearest(e.X, e.Y);
    }

    public IReadOnlyList<ScatterPoint>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public ScatterPoint? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    /// <summary>Joins the points in X order with a thin line.</summary>
    public bool Connect { get => GetValue(ConnectProperty); set => SetValue(ConnectProperty, value); }
    /// <summary>Thresholds drawn across the plot, dashed.</summary>
    public IReadOnlyList<ReferenceLine>? ReferenceLines { get => GetValue(ReferenceLinesProperty); set => SetValue(ReferenceLinesProperty, value); }
    /// <summary>The 95 % confidence ellipse of every group with three points or more.</summary>
    public bool ShowEllipses { get => GetValue(ShowEllipsesProperty); set => SetValue(ShowEllipsesProperty, value); }
    public bool ShowLegend { get => GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    /// <summary>Whether the Y axis starts at zero (a metric) or at the data (a score plot).</summary>
    public bool YFromZero { get => GetValue(YFromZeroProperty); set => SetValue(YFromZeroProperty, value); }
    /// <summary>Fixed colours for named groups — "up", "down", "not significant" on a volcano.</summary>
    public IReadOnlyDictionary<string, Color>? GroupColorOverrides { get => GetValue(GroupColorOverridesProperty); set => SetValue(GroupColorOverridesProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty) { ViewXMin = double.NaN; ViewXMax = double.NaN; }
    }

    protected override (double Min, double Max) DataXExtent()
    {
        var items = Items;
        if (items is null || items.Count == 0) return (double.NaN, double.NaN);
        var xs = items.Where(i => !double.IsNaN(i.X) && !double.IsInfinity(i.X)).Select(i => i.X).ToList();
        if (xs.Count == 0) return (double.NaN, double.NaN);
        var min = xs.Min();
        var max = xs.Max();
        foreach (var line in ReferenceLines ?? Array.Empty<ReferenceLine>())
        {
            if (line.Vertical) { min = Math.Min(min, line.Value); max = Math.Max(max, line.Value); }
        }
        var pad = Math.Max(1e-9, (max - min) * 0.06);
        if (max - min < 1e-12) pad = Math.Max(0.5, Math.Abs(min) * 0.1);
        return (min - pad, max + pad);
    }

    protected override (double Min, double Max) DataYExtent(double xMin, double xMax)
    {
        var items = Items;
        if (items is null || items.Count == 0) return (0, 1);
        var vis = items.Where(i => i.X >= xMin && i.X <= xMax && !double.IsNaN(i.Y) && !double.IsInfinity(i.Y)).ToList();
        if (vis.Count == 0) return (0, 1);
        var min = YFromZero ? Math.Min(0, vis.Min(i => i.Y)) : vis.Min(i => i.Y);
        var max = vis.Max(i => i.Y);
        foreach (var line in ReferenceLines ?? Array.Empty<ReferenceLine>())
        {
            if (!line.Vertical) { min = Math.Min(min, line.Value); max = Math.Max(max, line.Value); }
        }
        if (!YFromZero)
        {
            var pad = Math.Max(1e-9, (max - min) * 0.08);
            min -= pad;
            max += pad;
        }
        if (max <= min) max = min + 1;
        return (min, max);
    }

    protected override bool PadYRange => YFromZero;

    private Dictionary<string, Color> GroupColors()
    {
        var map = new Dictionary<string, Color>(StringComparer.Ordinal);
        var overrides = GroupColorOverrides;
        foreach (var g in (Items ?? Array.Empty<ScatterPoint>()).Select(i => i.Group).Distinct())
        {
            map[g] = overrides is not null && overrides.TryGetValue(g, out var fixedColor) ? fixedColor : SeriesColor(map.Count);
        }
        return map;
    }

    protected override void RenderPlot(Charts.ChartCanvas ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, double xMin, double xMax, double yMin, double yMax)
    {
        var items = Items!;
        var colors = GroupColors();
        // what the legend has to stay clear of: the reference lines' labels, every point, and every label drawn beside one
        var occupied = new List<Rect>();

        foreach (var line in ReferenceLines ?? Array.Empty<ReferenceLine>())
        {
            var pen = new Pen(MutedBrush, 1, dashStyle: DashStyle.Dash);
            if (line.Vertical)
            {
                var px = tx(line.Value);
                ctx.DrawLine(pen, new Point(px, plot.Y), new Point(px, plot.Bottom));
                if (line.Label is not null)
                {
                    var t = Text(line.Label, 9.5, MutedBrush);
                    ctx.DrawText(t, new Point(px + 3, plot.Y + 2));
                    occupied.Add(new Rect(px + 3, plot.Y + 2, t.Width, t.Height));
                }
            }
            else
            {
                var py = ty(line.Value);
                ctx.DrawLine(pen, new Point(plot.X, py), new Point(plot.Right, py));
                if (line.Label is not null)
                {
                    var t = Text(line.Label, 9.5, MutedBrush);
                    ctx.DrawText(t, new Point(plot.X + 4, py - 12 * FontScale));
                    occupied.Add(new Rect(plot.X + 4, py - 12 * FontScale, t.Width, t.Height));
                }
            }
        }

        if (ShowEllipses)
        {
            foreach (var group in items.GroupBy(i => i.Group))
            {
                var pts = group.Where(i => !double.IsNaN(i.Y) && !double.IsNaN(i.X)).ToList();
                if (pts.Count < 3) continue;
                var c = colors[group.Key];
                var outline = ConfidenceEllipse(pts.Select(i => (i.X, i.Y)).ToList()).Select(p => new Point(tx(p.X), ty(p.Y))).ToList();
                if (outline.Count == 0) continue;
                var geo = Polyline(outline, close: true);
                ctx.DrawGeometry(new SolidColorBrush(Color.FromArgb(26, c.R, c.G, c.B)), new Pen(new SolidColorBrush(Color.FromArgb(150, c.R, c.G, c.B)), 1), geo);
            }
        }

        if (Connect)
        {
            foreach (var group in items.GroupBy(i => i.Group))
            {
                var ordered = group.Where(i => !double.IsNaN(i.Y)).OrderBy(i => i.X).ToList();
                if (ordered.Count < 2) continue;
                var c = colors[group.Key];
                var geo = Polyline(ordered.Select(i => new Point(tx(i.X), ty(i.Y))).ToList());
                ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(120, c.R, c.G, c.B)), 1), geo);
            }
        }
        var radius = PointSize;
        foreach (var item in items)
        {
            if (double.IsNaN(item.Y) || double.IsNaN(item.X)) continue;
            var c = colors[item.Group];
            var center = new Point(tx(item.X), ty(item.Y));
            var selected = ReferenceEquals(item, SelectedItem);
            var r = selected ? radius * 1.45 : radius;
            ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(selected ? (byte)255 : (byte)190, c.R, c.G, c.B)), selected ? new Pen(TextBrush, 1.5) : null, center, r, r);
            occupied.Add(new Rect(center.X - r, center.Y - r, 2 * r, 2 * r));
        }
        if (ShowPointLabels || items.Any(i => i.Labelled))
        {
            var placed = new List<Rect>();
            foreach (var item in items.Where(i => (ShowPointLabels || i.Labelled) && !double.IsNaN(i.Y) && !double.IsNaN(i.X)).OrderByDescending(i => i.Labelled))
            {
                var label = item.Label.Length > 28 ? item.Label[..27] + "…" : item.Label;
                var ft = Text(label, 9.5, TextBrush);
                var at = new Point(tx(item.X) + radius + 2, ty(item.Y) - ft.Height / 2);
                var box = new Rect(at, new Size(ft.Width, ft.Height));
                if (placed.Any(p => p.Intersects(box))) continue;   // one label per patch of plot
                placed.Add(box);
                occupied.Add(box);
                ctx.DrawText(ft, at);
            }
        }
        if (ShowLegend) DrawLegend(ctx, plot, colors.Select(kv => (kv.Key, kv.Value)).ToList(), occupied);
    }

    /// <summary>The 95 % confidence ellipse of a cloud of points: the covariance's axes, scaled by χ²(2) at 5 %.</summary>
    private static IReadOnlyList<(double X, double Y)> ConfidenceEllipse(IReadOnlyList<(double X, double Y)> pts)
    {
        var n = pts.Count;
        var mx = pts.Average(p => p.X);
        var my = pts.Average(p => p.Y);
        double sxx = 0, syy = 0, sxy = 0;
        foreach (var (x, y) in pts) { sxx += (x - mx) * (x - mx); syy += (y - my) * (y - my); sxy += (x - mx) * (y - my); }
        sxx /= n - 1; syy /= n - 1; sxy /= n - 1;
        var trace = sxx + syy;
        var det = sxx * syy - sxy * sxy;
        var disc = Math.Sqrt(Math.Max(0, trace * trace / 4 - det));
        var l1 = trace / 2 + disc;
        var l2 = Math.Max(1e-12, trace / 2 - disc);
        var angle = Math.Abs(sxy) < 1e-12 ? (sxx >= syy ? 0 : Math.PI / 2) : Math.Atan2(l1 - sxx, sxy);
        const double chi = 5.991;   // χ² with 2 degrees of freedom at 95 %
        var a = Math.Sqrt(chi * l1);
        var b = Math.Sqrt(chi * l2);
        var outline = new List<(double, double)>(64);
        for (var k = 0; k < 64; k++)
        {
            var t = 2 * Math.PI * k / 64;
            var ex = a * Math.Cos(t);
            var ey = b * Math.Sin(t);
            outline.Add((mx + ex * Math.Cos(angle) - ey * Math.Sin(angle), my + ex * Math.Sin(angle) + ey * Math.Cos(angle)));
        }
        return outline;
    }

    private void SelectNearest(double x, double y)
    {
        var items = Items;
        if (items is null || items.Count == 0) return;
        var plot = PlotRect;
        var (xMin, xMax) = CurrentXRange();
        var (yMin, yMax) = DataYExtent(xMin, xMax);
        double Px(double v) => plot.X + (v - xMin) / (xMax - xMin) * plot.Width;
        double Py(double v) => plot.Bottom - (v - yMin) / (yMax - yMin) * plot.Height;
        ScatterPoint? best = null;
        var bestD = 14.0;
        foreach (var item in items)
        {
            if (double.IsNaN(item.Y)) continue;
            var d = Math.Sqrt(Math.Pow(Px(item.X) - Px(x), 2) + Math.Pow(Py(item.Y) - Py(y), 2));
            if (d < bestD) { bestD = d; best = item; }
        }
        if (best is not null) SelectedItem = best;
    }

    protected override IReadOnlyList<string>? GetTooltip(Point pos, Rect plot, Func<double, double> tx, Func<double, double> ty)
    {
        var items = Items;
        if (items is null || items.Count == 0) return null;
        ScatterPoint? best = null;
        var bestD = 12.0;
        foreach (var item in items)
        {
            if (double.IsNaN(item.Y)) continue;
            var d = Math.Sqrt(Math.Pow(tx(item.X) - pos.X, 2) + Math.Pow(ty(item.Y) - pos.Y, 2));
            if (d < bestD) { bestD = d; best = item; }
        }
        if (best is null) return null;
        return new[] { best.Label, $"{XLabel}: {best.X.ToString("0.###", CultureInfo.InvariantCulture)}", $"{YLabel}: {best.Y.ToString("0.###", CultureInfo.InvariantCulture)}", best.Group };
    }
}
