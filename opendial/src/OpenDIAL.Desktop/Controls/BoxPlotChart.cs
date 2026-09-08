using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace OpenDIAL.Desktop.Controls;

/// <summary>One box of a box plot: a named set of values, coloured by its group.</summary>
public sealed record BoxGroup(string Label, IReadOnlyList<double> Values, string Group);

/// <summary>
/// Box plots side by side: the box from the first to the third quartile, the median across it,
/// whiskers to the last value within one and a half interquartile ranges, the points beyond drawn
/// on their own, and every value jittered over the box so a box of three is seen to be three.
/// </summary>
public sealed class BoxPlotChart : ChartBase
{
    public static readonly StyledProperty<IReadOnlyList<BoxGroup>?> GroupsProperty = AvaloniaProperty.Register<BoxPlotChart, IReadOnlyList<BoxGroup>?>(nameof(Groups));
    public static readonly StyledProperty<bool> ShowPointsProperty = AvaloniaProperty.Register<BoxPlotChart, bool>(nameof(ShowPoints), true);

    static BoxPlotChart()
    {
        AffectsRender<BoxPlotChart>(GroupsProperty, ShowPointsProperty);
    }

    public BoxPlotChart()
    {
        XLabel = string.Empty;
        YLabel = "Value";
    }

    public IReadOnlyList<BoxGroup>? Groups { get => GetValue(GroupsProperty); set => SetValue(GroupsProperty, value); }
    public bool ShowPoints { get => GetValue(ShowPointsProperty); set => SetValue(ShowPointsProperty, value); }

    protected override bool ShowXTicks => false;
    protected override bool PadYRange => true;

    protected override (double Min, double Max) DataXExtent()
    {
        var groups = Groups;
        if (groups is null || groups.Count == 0) return (double.NaN, double.NaN);
        return (-0.5, groups.Count - 0.5);
    }

    protected override (double Min, double Max) DataYExtent(double xMin, double xMax)
    {
        var values = (Groups ?? Array.Empty<BoxGroup>()).SelectMany(g => g.Values).Where(v => !double.IsNaN(v)).ToList();
        if (values.Count == 0) return (0, 1);
        var min = values.Min();
        var max = values.Max();
        if (max <= min) max = min + 1;
        var pad = (max - min) * 0.05;
        return (min - pad, max + pad);
    }

    protected override void RenderPlot(Charts.ChartCanvas ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, double xMin, double xMax, double yMin, double yMax)
    {
        var groups = Groups!;
        var colours = new Dictionary<string, Color>(StringComparer.Ordinal);
        foreach (var g in groups.Select(g => g.Group).Distinct()) colours[g] = SeriesColor(colours.Count);
        var slot = plot.Width / Math.Max(1, xMax - xMin);
        var boxW = Math.Max(6, Math.Min(slot * 0.55, 60));
        var rng = new Random(11);
        for (var i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            var values = g.Values.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToList();
            if (values.Count == 0) continue;
            var c = colours[g.Group];
            var cx = tx(i);
            var brush = new SolidColorBrush(Color.FromArgb(70, c.R, c.G, c.B));
            var pen = new Pen(new SolidColorBrush(c), 1.4);
            var q1 = Quantile(values, 0.25);
            var q2 = Quantile(values, 0.5);
            var q3 = Quantile(values, 0.75);
            var iqr = q3 - q1;
            var low = values.Where(v => v >= q1 - 1.5 * iqr).DefaultIfEmpty(q1).Min();
            var high = values.Where(v => v <= q3 + 1.5 * iqr).DefaultIfEmpty(q3).Max();
            if (values.Count >= 2)
            {
                ctx.DrawRectangle(brush, pen, new Rect(cx - boxW / 2, ty(q3), boxW, Math.Max(1, ty(q1) - ty(q3))));
                ctx.DrawLine(new Pen(new SolidColorBrush(c), 2.2), new Point(cx - boxW / 2, ty(q2)), new Point(cx + boxW / 2, ty(q2)));
                ctx.DrawLine(pen, new Point(cx, ty(q3)), new Point(cx, ty(high)));
                ctx.DrawLine(pen, new Point(cx, ty(q1)), new Point(cx, ty(low)));
                ctx.DrawLine(pen, new Point(cx - boxW / 4, ty(high)), new Point(cx + boxW / 4, ty(high)));
                ctx.DrawLine(pen, new Point(cx - boxW / 4, ty(low)), new Point(cx + boxW / 4, ty(low)));
            }
            if (ShowPoints || values.Count < 2)
            {
                foreach (var v in values)
                {
                    var jitter = (rng.NextDouble() - 0.5) * boxW * 0.6;
                    var outlier = v < q1 - 1.5 * iqr || v > q3 + 1.5 * iqr;
                    ctx.DrawEllipse(new SolidColorBrush(Color.FromArgb(outlier ? (byte)255 : (byte)170, c.R, c.G, c.B)), outlier ? new Pen(TextBrush, 1) : null, new Point(cx + jitter, ty(v)), PointSize * 0.8, PointSize * 0.8);
                }
            }
        }
        if (colours.Count > 1 && groups.Select(g => g.Group).Distinct().Count() != groups.Count)
        {
            DrawLegend(ctx, plot, colours.Select(kv => (kv.Key, kv.Value)).ToList());
        }
    }

    protected override void RenderOverlay(Charts.ChartCanvas ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, double xMin, double xMax)
    {
        var groups = Groups;
        if (groups is null || groups.Count == 0) return;
        var slot = plot.Width / Math.Max(1, xMax - xMin);
        var every = Math.Max(1, (int)Math.Ceiling(70 * FontScale / slot));
        for (var i = 0; i < groups.Count; i += every)
        {
            var text = groups[i].Label.Length > 16 ? groups[i].Label[..15] + "…" : groups[i].Label;
            var ft = Text(text, 9.5, MutedBrush);
            var cx = tx(i);
            if (cx < plot.X || cx > plot.Right) continue;
            ctx.DrawText(ft, new Point(cx - ft.Width / 2, plot.Bottom + 5));
        }
    }

    protected override IReadOnlyList<string>? GetTooltip(Point pos, Rect plot, Func<double, double> tx, Func<double, double> ty)
    {
        var groups = Groups;
        if (groups is null || groups.Count == 0) return null;
        var (xMin, xMax) = CurrentXRange();
        var x = xMin + (pos.X - plot.X) / plot.Width * (xMax - xMin);
        var i = (int)Math.Round(x);
        if (i < 0 || i >= groups.Count) return null;
        var values = groups[i].Values.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToList();
        if (values.Count == 0) return null;
        string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);
        return new[] { groups[i].Label, $"n {values.Count} · median {F(Quantile(values, 0.5))}", $"Q1 {F(Quantile(values, 0.25))} · Q3 {F(Quantile(values, 0.75))}", $"min {F(values[0])} · max {F(values[^1])}" };
    }

    private static double Quantile(IReadOnlyList<double> sorted, double q)
    {
        if (sorted.Count == 0) return double.NaN;
        var position = (sorted.Count - 1) * q;
        var low = (int)Math.Floor(position);
        var high = Math.Min(sorted.Count - 1, low + 1);
        return sorted[low] + (sorted[high] - sorted[low]) * (position - low);
    }
}
