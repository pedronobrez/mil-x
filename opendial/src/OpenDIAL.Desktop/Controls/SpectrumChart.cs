using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace OpenDIAL.Desktop.Controls;

/// <summary>
/// Centroid (stick) mass spectrum. Intensities are normalised to 100 %. When <see cref="ReferencePeaks"/>
/// is set the reference is drawn mirrored below the axis (measured up, library down). The N most intense
/// peaks of each spectrum are labelled with their m/z.
/// </summary>
public sealed class SpectrumChart : ChartBase
{
    public static readonly StyledProperty<IReadOnlyList<Point>?> PeaksProperty = AvaloniaProperty.Register<SpectrumChart, IReadOnlyList<Point>?>(nameof(Peaks));
    public static readonly StyledProperty<IReadOnlyList<Point>?> ReferencePeaksProperty = AvaloniaProperty.Register<SpectrumChart, IReadOnlyList<Point>?>(nameof(ReferencePeaks));
    public static readonly StyledProperty<int> TopLabelsProperty = AvaloniaProperty.Register<SpectrumChart, int>(nameof(TopLabels), 8);
    public static readonly StyledProperty<double> PrecursorMzProperty = AvaloniaProperty.Register<SpectrumChart, double>(nameof(PrecursorMz), double.NaN);
    public static readonly StyledProperty<bool> NormalizeProperty = AvaloniaProperty.Register<SpectrumChart, bool>(nameof(Normalize), true);

    static SpectrumChart()
    {
        AffectsRender<SpectrumChart>(PeaksProperty, ReferencePeaksProperty, TopLabelsProperty, PrecursorMzProperty, NormalizeProperty);
    }

    public SpectrumChart()
    {
        XLabel = "m/z";
        YLabel = "Relative intensity (%)";
    }

    public IReadOnlyList<Point>? Peaks { get => GetValue(PeaksProperty); set => SetValue(PeaksProperty, value); }
    public IReadOnlyList<Point>? ReferencePeaks { get => GetValue(ReferencePeaksProperty); set => SetValue(ReferencePeaksProperty, value); }
    public int TopLabels { get => GetValue(TopLabelsProperty); set => SetValue(TopLabelsProperty, value); }
    public double PrecursorMz { get => GetValue(PrecursorMzProperty); set => SetValue(PrecursorMzProperty, value); }
    public bool Normalize { get => GetValue(NormalizeProperty); set => SetValue(NormalizeProperty, value); }

    private bool HasReference => ReferencePeaks is { Count: > 0 };

    protected override bool PadYRange => false;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PeaksProperty || change.Property == ReferencePeaksProperty)
        {
            ViewXMin = double.NaN;
            ViewXMax = double.NaN;
            if (change.Property == PeaksProperty)
            {
                YLabel = Normalize ? "Relative intensity (%)" : "Intensity";
            }
        }
    }

    protected override (double Min, double Max) DataXExtent()
    {
        var all = Enumerable.Empty<Point>();
        if (Peaks is { Count: > 0 } p) all = all.Concat(p);
        if (ReferencePeaks is { Count: > 0 } r) all = all.Concat(r);
        var list = all.ToList();
        if (list.Count == 0) return (double.NaN, double.NaN);
        var min = list.Min(q => q.X);
        var max = list.Max(q => q.X);
        if (!double.IsNaN(PrecursorMz)) { min = Math.Min(min, PrecursorMz); max = Math.Max(max, PrecursorMz); }
        var pad = Math.Max(1, (max - min) * 0.04);
        return (Math.Max(0, min - pad), max + pad);
    }

    protected override (double Min, double Max) DataYExtent(double xMin, double xMax)
    {
        if (Normalize)
        {
            return (HasReference ? -118 : 0, 118); // head-room for labels
        }
        var max = Peaks is { Count: > 0 } p ? p.Max(q => q.Y) : 1;
        return (HasReference ? -max * 1.18 : 0, max * 1.18);
    }

    private double Scale(IReadOnlyList<Point> peaks)
    {
        if (!Normalize) return 1;
        var max = peaks.Count == 0 ? 0 : peaks.Max(p => p.Y);
        return max <= 0 ? 1 : 100.0 / max;
    }

    protected override void RenderPlot(Charts.ChartCanvas ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, double xMin, double xMax, double yMin, double yMax)
    {
        var zero = ty(0);
        var measuredColor = AccentColor;
        var referenceColor = Categorical[0];

        if (Peaks is { Count: > 0 } peaks)
        {
            DrawSticks(ctx, plot, tx, ty, peaks, Scale(peaks), measuredColor, xMin, xMax, mirrored: false);
        }
        if (ReferencePeaks is { Count: > 0 } reference)
        {
            DrawSticks(ctx, plot, tx, ty, reference, Scale(reference), referenceColor, xMin, xMax, mirrored: true);
            ctx.DrawLine(new Pen(AxisBrush, 1), new Point(plot.X, zero), new Point(plot.Right, zero));
            var up = MakeText("measured", 10, new SolidColorBrush(measuredColor));
            var down = MakeText("reference", 10, new SolidColorBrush(referenceColor));
            ctx.DrawText(up, new Point(plot.Right - up.Width - 6, plot.Y + 4));
            ctx.DrawText(down, new Point(plot.Right - down.Width - 6, plot.Bottom - down.Height - 4));
        }

        if (!double.IsNaN(PrecursorMz) && PrecursorMz >= xMin && PrecursorMz <= xMax)
        {
            var px = tx(PrecursorMz);
            var pen = new Pen(new SolidColorBrush(Categorical[1]), 1, dashStyle: DashStyle.Dot);
            ctx.DrawLine(pen, new Point(px, plot.Y), new Point(px, plot.Bottom));
            var tri = Polyline(new[] { new Point(px, zero - 7), new Point(px - 5, zero - 1), new Point(px + 5, zero - 1) }, close: true);
            ctx.DrawGeometry(new SolidColorBrush(Categorical[1]), null, tri);
        }
    }

    private void DrawSticks(Charts.ChartCanvas ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, IReadOnlyList<Point> peaks, double scale, Color color,
        double xMin, double xMax, bool mirrored)
    {
        var pen = new Pen(new SolidColorBrush(color), Compact ? 1 : 1.2);
        var zero = ty(0);
        var visible = new List<(double X, double Y)>();
        foreach (var p in peaks)
        {
            if (p.X < xMin || p.X > xMax) continue;
            var y = p.Y * scale * (mirrored ? -1 : 1);
            visible.Add((p.X, y));
            ctx.DrawLine(pen, new Point(tx(p.X), zero), new Point(tx(p.X), ty(y)));
        }

        // labels for the most intense visible peaks, skipping overlaps
        var labelBrush = new SolidColorBrush(color);
        var placed = new List<double>();
        foreach (var (x, y) in visible.OrderByDescending(v => Math.Abs(v.Y)).Take(Math.Max(0, TopLabels)))
        {
            var px = tx(x);
            if (placed.Any(q => Math.Abs(q - px) < 48)) continue;
            var ft = MakeText(x.ToString("F4", CultureInfo.InvariantCulture), Compact ? 9 : 9.5, labelBrush);
            var py = mirrored ? ty(y) + 2 : ty(y) - ft.Height - 2;
            var lx = Math.Clamp(px - ft.Width / 2, plot.X, plot.Right - ft.Width);
            ctx.DrawText(ft, new Point(lx, py));
            placed.Add(px);
        }
    }

    protected override IReadOnlyList<string>? GetTooltip(Point pos, Rect plot, Func<double, double> tx, Func<double, double> ty)
    {
        var mirrored = HasReference && pos.Y > ty(0);
        var peaks = mirrored ? ReferencePeaks : Peaks;
        if (peaks is null || peaks.Count == 0) return null;
        var scale = Scale(peaks);
        Point? best = null;
        var bestDist = 8.0;
        foreach (var p in peaks)
        {
            var d = Math.Abs(tx(p.X) - pos.X);
            if (d < bestDist) { bestDist = d; best = p; }
        }
        if (best is not { } b) return null;
        var lines = new List<string> { (mirrored ? "reference  " : string.Empty) + $"m/z {b.X.ToString("F4", CultureInfo.InvariantCulture)}" };
        lines.Add(Normalize ? $"{(b.Y * scale).ToString("F1", CultureInfo.InvariantCulture)} %  (abs {FormatIntensity(b.Y)})" : $"Intensity {FormatIntensity(b.Y)}");
        return lines;
    }
}
