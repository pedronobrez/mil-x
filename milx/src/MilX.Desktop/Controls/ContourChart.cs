using System.Globalization;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MilX.Pipeline.Results;

namespace MilX.Desktop.Controls;

/// <summary>
/// The contour: a channel as retention time across, m/z up and intensity as colour. The grid is a
/// <see cref="ContourGrid"/> built elsewhere; this control turns it into a raster once per change of
/// grid, scale or palette and draws that raster stretched into the plot, so zooming the time axis
/// (the usual chart gestures) costs nothing. The intensity scale is square root by default: a linear
/// ramp over four decades shows the base peak and nothing else, and a log ramp flattens everything
/// into the same grey. The top of the scale is the 99.7th percentile, not the maximum.
/// </summary>
public sealed class ContourChart : ChartBase
{
    public static readonly StyledProperty<ContourGrid?> GridProperty = AvaloniaProperty.Register<ContourChart, ContourGrid?>(nameof(Grid));
    public static readonly StyledProperty<string> IntensityScaleProperty = AvaloniaProperty.Register<ContourChart, string>(nameof(IntensityScale), "sqrt");
    public static readonly StyledProperty<string> ColoursProperty = AvaloniaProperty.Register<ContourChart, string>(nameof(Colours), "inferno");
    public static readonly StyledProperty<double> MarkerRtProperty = AvaloniaProperty.Register<ContourChart, double>(nameof(MarkerRt), double.NaN);

    public static readonly string[] IntensityScales = { "sqrt", "log", "linear" };
    public static readonly string[] ColourMaps = { "inferno", "viridis", "magma", "turbo", "greys" };

    private WriteableBitmap? _raster;
    private object? _rasterKey;
    private double _top;

    static ContourChart()
    {
        AffectsRender<ContourChart>(GridProperty, IntensityScaleProperty, ColoursProperty, MarkerRtProperty);
    }

    public ContourChart()
    {
        XLabel = "Retention time (min)";
        YLabel = "m/z";
        ShowGrid = false;
    }

    public ContourGrid? Grid { get => GetValue(GridProperty); set => SetValue(GridProperty, value); }
    /// <summary>"sqrt", "log" or "linear".</summary>
    public string IntensityScale { get => GetValue(IntensityScaleProperty); set => SetValue(IntensityScaleProperty, value); }
    /// <summary>"inferno", "viridis", "magma", "turbo" or "greys".</summary>
    public string Colours { get => GetValue(ColoursProperty); set => SetValue(ColoursProperty, value); }
    /// <summary>The retention time of the scan the spectrum pane is showing.</summary>
    public double MarkerRt { get => GetValue(MarkerRtProperty); set => SetValue(MarkerRtProperty, value); }

    /// <summary>The top of the colour scale, after the last render (the tests ask).</summary>
    internal double ScaleTop => _top;

    protected override bool PadYRange => false;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == GridProperty)
        {
            ViewXMin = double.NaN;
            ViewXMax = double.NaN;
        }
    }

    protected override (double Min, double Max) DataXExtent()
    {
        if (Grid is not { IsEmpty: false } g) return (double.NaN, double.NaN);
        var (lo, hi) = g.RtRange;
        if (hi <= lo) hi = lo + 1e-3;
        // half a row either side, so the first and last rows are drawn whole
        var half = g.Rows > 1 ? (hi - lo) / (g.Rows - 1) / 2 : 1e-3;
        return (Math.Max(0, lo - half), hi + half);
    }

    protected override (double Min, double Max) DataYExtent(double xMin, double xMax)
    {
        if (Grid is not { IsEmpty: false } g) return (0, 1);
        return g.MzRange;
    }

    protected override void RenderPlot(Charts.ChartCanvas ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, double xMin, double xMax, double yMin, double yMax)
    {
        if (Grid is not { IsEmpty: false } g) return;
        var raster = Raster(g);
        if (raster is null) return;
        var (xLo, xHi) = DataXExtent();
        var left = tx(xLo);
        var right = tx(xHi);
        var top = ty(yMax);
        var bottom = ty(yMin);
        ctx.DrawImage(raster, new Rect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top)));
        if (!double.IsNaN(MarkerRt) && MarkerRt >= xMin && MarkerRt <= xMax)
        {
            var px = tx(MarkerRt);
            ctx.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0xC0, 0xFF, 0xFF, 0xFF)), 1, dashStyle: DashStyle.Dash), new Point(px, plot.Y), new Point(px, plot.Bottom));
        }
        if (!string.IsNullOrEmpty(g.Note))
        {
            var ft = Text(g.Note, 9.5, new SolidColorBrush(Color.FromArgb(0xD0, 0xFF, 0xFF, 0xFF)));
            ctx.DrawText(ft, new Point(plot.X + 6, plot.Bottom - ft.Height - 4));
        }
    }

    protected override void RenderOverlay(Charts.ChartCanvas ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, double xMin, double xMax)
    {
        if (Grid is not { IsEmpty: false } || Compact) return;
        // the colour bar: a strip at the right edge of the plot, with the top of the scale and its law
        var bar = new Rect(plot.Right - 14, plot.Y + 6, 8, Math.Max(10, Math.Min(120, plot.Height * 0.4)));
        const int steps = 24;
        for (var i = 0; i < steps; i++)
        {
            var f = 1 - i / (double)(steps - 1);
            var c = Colour(f);
            var y = bar.Y + bar.Height * i / steps;
            ctx.DrawRectangle(new SolidColorBrush(c), null, new Rect(bar.X, y, bar.Width, bar.Height / steps + 0.5));
        }
        ctx.DrawRectangle(null, new Pen(AxisBrush, 1), bar);
        var label = Text($"{IntensityScale} · top {ChartBase.FormatIntensity(_top)}", 9, MutedBrush);
        ctx.DrawText(label, new Point(bar.X - label.Width - 4, bar.Y));
    }

    protected override IReadOnlyList<string>? GetTooltip(Point pos, Rect plot, Func<double, double> tx, Func<double, double> ty)
    {
        if (Grid is not { IsEmpty: false } g) return null;
        var (xMin, xMax) = CurrentXRange();
        var rt = xMin + (pos.X - plot.X) / plot.Width * (xMax - xMin);
        var (mzLo, mzHi) = g.MzRange;
        var mz = mzLo + (plot.Bottom - pos.Y) / plot.Height * (mzHi - mzLo);
        var value = g.At(rt, mz);
        if (value is null) return null;
        return new[]
        {
            $"RT {rt.ToString("F3", CultureInfo.InvariantCulture)} min",
            $"m/z {mz.ToString("F3", CultureInfo.InvariantCulture)}",
            $"intensity {FormatIntensity(value.Value)}",
        };
    }

    /// <summary>The m/z under a plot position, for the view that asks where a click landed.</summary>
    public double MzAt(Point pos)
    {
        if (Grid is not { IsEmpty: false } g) return double.NaN;
        var plot = PlotRect;
        var (mzLo, mzHi) = g.MzRange;
        return mzLo + (plot.Bottom - pos.Y) / plot.Height * (mzHi - mzLo);
    }

    private WriteableBitmap? Raster(ContourGrid g)
    {
        var key = (g, IntensityScale, Colours);
        if (_raster is not null && Equals(_rasterKey, key)) return _raster;
        _raster?.Dispose();
        _raster = null;
        _rasterKey = key;
        var rows = g.Rows;
        var bins = g.Bins;
        if (rows == 0 || bins == 0) return null;
        _top = g.Percentile(99.7);
        if (_top <= 0) _top = 1;
        var bitmap = new WriteableBitmap(new PixelSize(rows, bins), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var buffer = bitmap.Lock())
        {
            var stride = buffer.RowBytes;
            var pixels = new byte[stride * bins];
            var scale = IntensityScale;
            for (var b = 0; b < bins; b++)
            {
                var y = bins - 1 - b;   // mass increases upwards
                for (var r = 0; r < rows; r++)
                {
                    var f = Scaled(g.Intensity[r, b], _top, scale);
                    var c = Colour(f);
                    var o = y * stride + r * 4;
                    pixels[o] = c.B; pixels[o + 1] = c.G; pixels[o + 2] = c.R; pixels[o + 3] = 255;
                }
            }
            Marshal.Copy(pixels, 0, buffer.Address, pixels.Length);
        }
        _raster = bitmap;
        return _raster;
    }

    /// <summary>Intensity to a 0–1 position on the colour ramp, under the chosen law, clipped at the scale's top.</summary>
    internal static double Scaled(double value, double top, string scale)
    {
        if (value <= 0 || top <= 0) return 0;
        var f = Math.Min(1, value / top);
        return scale switch
        {
            "linear" => f,
            "log" => Math.Log10(1 + 999 * f) / 3,    // three decades, 0 at nothing and 1 at the top
            _ => Math.Sqrt(f),
        };
    }

    private Color Colour(double f) => Sample(Colours, f);

    private static readonly Dictionary<string, Color[]> Ramps = new(StringComparer.OrdinalIgnoreCase)
    {
        ["inferno"] = Stops("#000004", "#1b0c41", "#4a0c6b", "#781c6d", "#a52c60", "#cf4446", "#ed6925", "#fb9b06", "#f7d13d", "#fcffa4"),
        ["viridis"] = Stops("#440154", "#482878", "#3e4989", "#31688e", "#26828e", "#1f9e89", "#35b779", "#6ece58", "#b5de2b", "#fde725"),
        ["magma"] = Stops("#000004", "#180f3d", "#440f76", "#721f81", "#9e2f7f", "#cd4071", "#f1605d", "#fd9668", "#feca8d", "#fcfdbf"),
        ["turbo"] = Stops("#30123b", "#4662d7", "#36aaf9", "#1ae4b6", "#72fe5e", "#c7ef34", "#faba39", "#f66b19", "#ca2a04", "#7a0403"),
        ["greys"] = Stops("#ffffff", "#e0e0e0", "#c0c0c0", "#a0a0a0", "#808080", "#606060", "#404040", "#202020", "#101010", "#000000"),
    };

    private static Color[] Stops(params string[] hex) => hex.Select(Color.Parse).ToArray();

    /// <summary>A colour on one of the ramps, by linear interpolation between its stops.</summary>
    internal static Color Sample(string ramp, double f)
    {
        var stops = Ramps.TryGetValue(ramp ?? string.Empty, out var r) ? r : Ramps["inferno"];
        f = Math.Clamp(f, 0, 1);
        var x = f * (stops.Length - 1);
        var i = Math.Min(stops.Length - 2, (int)Math.Floor(x));
        var t = x - i;
        var a = stops[i];
        var b = stops[i + 1];
        return Color.FromArgb(255, (byte)Math.Round(a.R + (b.R - a.R) * t), (byte)Math.Round(a.G + (b.G - a.G) * t), (byte)Math.Round(a.B + (b.B - a.B) * t));
    }
}
