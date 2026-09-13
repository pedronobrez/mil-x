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

        var measuredInk = new List<Rect>();
        if (Peaks is { Count: > 0 } peaks)
        {
            measuredInk = DrawSticks(ctx, plot, tx, ty, peaks, Scale(peaks), measuredColor, xMin, xMax, mirrored: false);
        }
        if (ReferencePeaks is { Count: > 0 } reference)
        {
            var referenceInk = DrawSticks(ctx, plot, tx, ty, reference, Scale(reference), referenceColor, xMin, xMax, mirrored: true);
            ctx.DrawLine(new Pen(AxisBrush, 1), new Point(plot.X, zero), new Point(plot.Right, zero));
            // each label goes where its own half has nothing drawn: an edge when an edge is free, and
            // the emptiest place along the band otherwise. Choosing by peak height alone was not
            // enough — what collides is the m/z written above a peak, which is wider than the peak.
            var up = MakeText("measured", 10, new SolidColorBrush(measuredColor));
            var down = MakeText("reference", 10, new SolidColorBrush(referenceColor));
            var upY = plot.Y + 4;
            var downY = plot.Bottom - down.Height - 4;
            ctx.DrawText(up, new Point(LabelX(plot, up.Width, upY, upY + up.Height, measuredInk), upY));
            ctx.DrawText(down, new Point(LabelX(plot, down.Width, downY, downY + down.Height, referenceInk), downY));
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

    /// <summary>
    /// Where to start a corner label so that it sits over nothing. The band it would occupy is slid
    /// across the plot and scored by how much drawn ink it covers; ties go to whichever edge is
    /// nearer, so a free corner still wins and only a crowded half pushes the label inwards.
    /// </summary>
    internal static double LabelX(Rect plot, double width, double top, double bottom, IReadOnlyList<Rect> ink)
    {
        var span = Math.Max(0, plot.Width - width - 12);
        var best = plot.X + 6;
        var bestScore = double.NegativeInfinity;
        for (var i = 0; i <= 24; i++)
        {
            var x = plot.X + 6 + span * i / 24.0;
            var box = new Rect(x, top, width, bottom - top);
            var covered = 0.0;
            foreach (var r in ink) covered += Covered(r, box);
            // a free edge beats a free middle: the closer to an edge, the smaller the penalty
            var penalty = Math.Min(x - plot.X, plot.Right - (x + width)) * 0.001;
            var score = -covered - penalty;
            if (score > bestScore) { bestScore = score; best = x; }
        }
        return best;
    }

    private static double Covered(Rect a, Rect b)
    {
        var w = Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X);
        var h = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);
        return w <= 0 || h <= 0 ? 0 : w * h;
    }

    /// <summary>Draws one half of the mirror and answers with the rectangles it drew ink into.</summary>
    private List<Rect> DrawSticks(Charts.ChartCanvas ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, IReadOnlyList<Point> peaks, double scale, Color color,
        double xMin, double xMax, bool mirrored)
    {
        var ink = new List<Rect>();
        var pen = new Pen(new SolidColorBrush(color), Compact ? 1 : 1.2);
        var zero = ty(0);
        var visible = new List<(double X, double Y)>();
        foreach (var p in peaks)
        {
            if (p.X < xMin || p.X > xMax) continue;
            var y = p.Y * scale * (mirrored ? -1 : 1);
            visible.Add((p.X, y));
            var px0 = tx(p.X);
            var py0 = ty(y);
            ctx.DrawLine(pen, new Point(px0, zero), new Point(px0, py0));
            ink.Add(new Rect(px0 - 1, Math.Min(zero, py0), 2, Math.Abs(zero - py0)));
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
            ink.Add(new Rect(lx, py, ft.Width, ft.Height));
            placed.Add(px);
        }
        return ink;
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
