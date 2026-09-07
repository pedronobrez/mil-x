using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace OpenDIAL.Desktop.Controls;

/// <summary>One point of a metric plot; <see cref="Group"/> picks the colour, <see cref="Tag"/> travels back on click.</summary>
public sealed record ScatterPoint(double X, double Y, string Label, string Group, object? Tag = null);

/// <summary>Scatter plot with categorical colouring, legend, hover tooltip and click selection.</summary>
public sealed class ScatterChart : ChartBase
{
    public static readonly StyledProperty<IReadOnlyList<ScatterPoint>?> ItemsProperty = AvaloniaProperty.Register<ScatterChart, IReadOnlyList<ScatterPoint>?>(nameof(Items));
    public static readonly StyledProperty<ScatterPoint?> SelectedItemProperty = AvaloniaProperty.Register<ScatterChart, ScatterPoint?>(nameof(SelectedItem), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<bool> ConnectProperty = AvaloniaProperty.Register<ScatterChart, bool>(nameof(Connect));

    static ScatterChart()
    {
        AffectsRender<ScatterChart>(ItemsProperty, SelectedItemProperty, ConnectProperty);
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

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty) { ViewXMin = double.NaN; ViewXMax = double.NaN; }
    }

    protected override (double Min, double Max) DataXExtent()
    {
        var items = Items;
        if (items is null || items.Count == 0) return (double.NaN, double.NaN);
        var min = items.Min(i => i.X);
        var max = items.Max(i => i.X);
        var pad = Math.Max(1e-9, (max - min) * 0.06);
        if (max - min < 1e-12) pad = Math.Max(0.5, Math.Abs(min) * 0.1);
        return (min - pad, max + pad);
    }

    protected override (double Min, double Max) DataYExtent(double xMin, double xMax)
    {
        var items = Items;
        if (items is null || items.Count == 0) return (0, 1);
        var vis = items.Where(i => i.X >= xMin && i.X <= xMax && !double.IsNaN(i.Y)).ToList();
        if (vis.Count == 0) return (0, 1);
        var min = Math.Min(0, vis.Min(i => i.Y));
        var max = vis.Max(i => i.Y);
        if (max <= min) max = min + 1;
        return (min, max);
    }

    private Dictionary<string, Color> GroupColors()
    {
        var map = new Dictionary<string, Color>(StringComparer.Ordinal);
        foreach (var g in (Items ?? Array.Empty<ScatterPoint>()).Select(i => i.Group).Distinct())
        {
            map[g] = SeriesColor(map.Count);
        }
        return map;
    }

    protected override void RenderPlot(DrawingContext ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, double xMin, double xMax, double yMin, double yMax)
    {
        var items = Items!;
        var colors = GroupColors();
        if (Connect)
        {
            foreach (var group in items.GroupBy(i => i.Group))
            {
                var ordered = group.Where(i => !double.IsNaN(i.Y)).OrderBy(i => i.X).ToList();
                if (ordered.Count < 2) continue;
                var c = colors[group.Key];
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    g.BeginFigure(new Point(tx(ordered[0].X), ty(ordered[0].Y)), false);
                    for (var i = 1; i < ordered.Count; i++) g.LineTo(new Point(tx(ordered[i].X), ty(ordered[i].Y)));
                    g.EndFigure(false);
                }
                ctx.DrawGeometry(null, new Pen(new SolidColorBrush(Color.FromArgb(120, c.R, c.G, c.B)), 1), geo);
            }
        }
        foreach (var item in items)
        {
            if (double.IsNaN(item.Y)) continue;
            var c = colors[item.Group];
            var center = new Point(tx(item.X), ty(item.Y));
            var selected = ReferenceEquals(item, SelectedItem);
            ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(selected ? (byte)255 : (byte)190, c.R, c.G, c.B)), selected ? new Pen(TextBrush, 1.5) : null, center, selected ? 6 : 4.2, selected ? 6 : 4.2);
        }
        DrawLegend(ctx, plot, colors.Select(kv => (kv.Key, kv.Value)).ToList());
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
        return new[] { best.Label, $"{XLabel}: {best.X.ToString("0.###", CultureInfo.InvariantCulture)}", $"{YLabel}: {best.Y.ToString("0.###", CultureInfo.InvariantCulture)}", $"Class {best.Group}" };
    }
}
