using System.Text.Json.Nodes;
using MilX.Pipeline.Results;

namespace MilX.Cli.Commands;

/// <summary>
/// The few numbers that answer the usual question about a chromatogram, so an agent reads a dozen
/// lines rather than a thousand points. The definitions are stated in the document, because MIL-Q
/// has to compute the same ones from the same trace:
///   apex      the point of greatest intensity inside the time window (the whole trace when none);
///   bounds    walked outward from the apex: the first point at or below 5 % of the apex height, or
///             the point after a valley where the trace rises again by a tenth of the apex, is the edge;
///   area      trapezoid over the points between the bounds, in intensity × minutes;
///   fwhm      the width at half the apex height, the two crossings interpolated linearly, in minutes;
///   points    the number of trace points between the bounds, apex included;
///   snr       apex height over the noise of the whole trace, MIL-Q's automatic estimate: the median
///             absolute deviation of the point-to-point differences scaled to a standard deviation,
///             with the standard deviation of the lower half of the points as the fallback; null when
///             nothing can be measured (a trace of exact zeros).
/// </summary>
internal sealed class PeakSummary
{
    public const string SnrMethod = "apex height / noise; noise = MAD of point-to-point differences scaled to SD (1.4826/√2), lower-half SD when more than half the differences are zero; over the whole trace in the window";
    public const string BoundsRule = "from the apex outward to the first point at or below 5 % of the apex, or to a rise of a tenth of the apex after a valley";

    public bool Found { get; private set; }
    public int ApexIndex { get; private set; } = -1;
    public double ApexRt { get; private set; }
    public double ApexHeight { get; private set; }
    public int Left { get; private set; }
    public int Right { get; private set; }
    public double Area { get; private set; }
    public double? Fwhm { get; private set; }
    public int Points { get; private set; }
    public double? Noise { get; private set; }
    public double? Snr { get; private set; }
    public IReadOnlyList<ChromatogramPoint> Trace { get; private set; } = Array.Empty<ChromatogramPoint>();

    public static PeakSummary Of(IReadOnlyList<ChromatogramPoint> trace)
    {
        var s = new PeakSummary { Trace = trace };
        if (trace.Count == 0) return s;
        var apex = -1;
        double max = 0;
        for (var i = 0; i < trace.Count; i++)
        {
            if (trace[i].Intensity > max) { max = trace[i].Intensity; apex = i; }
        }
        if (apex < 0 || max <= 0) return s;
        s.Found = true;
        s.ApexIndex = apex;
        s.ApexRt = trace[apex].Rt;
        s.ApexHeight = max;
        var floor = max * 0.05;
        var rise = max * 0.10;
        // left edge
        var left = apex;
        var valley = max;
        for (var i = apex - 1; i >= 0; i--)
        {
            var y = trace[i].Intensity;
            if (y <= floor) { left = i; break; }
            if (y < valley) valley = y;
            else if (y > valley + rise) { left = i + 1; break; }
            left = i;
        }
        var right = apex;
        valley = max;
        for (var i = apex + 1; i < trace.Count; i++)
        {
            var y = trace[i].Intensity;
            if (y <= floor) { right = i; break; }
            if (y < valley) valley = y;
            else if (y > valley + rise) { right = i - 1; break; }
            right = i;
        }
        s.Left = left;
        s.Right = right;
        s.Points = right - left + 1;
        double area = 0;
        for (var i = left; i < right; i++)
        {
            area += (trace[i + 1].Rt - trace[i].Rt) * (trace[i].Intensity + trace[i + 1].Intensity) / 2;
        }
        s.Area = area;
        s.Fwhm = WidthAtHalf(trace, apex, left, right, max);
        var noise = SignalToNoise.Automatic(trace);
        s.Noise = noise;
        s.Snr = noise is { } n && n > 0 ? max / n : null;
        return s;
    }

    private static double? WidthAtHalf(IReadOnlyList<ChromatogramPoint> trace, int apex, int left, int right, double max)
    {
        var half = max / 2;
        double? l = null, r = null;
        for (var i = apex; i > left; i--)
        {
            if (trace[i - 1].Intensity <= half)
            {
                l = Cross(trace[i - 1], trace[i], half);
                break;
            }
        }
        for (var i = apex; i < right; i++)
        {
            if (trace[i + 1].Intensity <= half)
            {
                r = Cross(trace[i], trace[i + 1], half);
                break;
            }
        }
        return l is { } a && r is { } b ? b - a : null;
    }

    private static double Cross(ChromatogramPoint p, ChromatogramPoint q, double level)
    {
        var dy = q.Intensity - p.Intensity;
        if (Math.Abs(dy) < 1e-12) return (p.Rt + q.Rt) / 2;
        return p.Rt + (level - p.Intensity) / dy * (q.Rt - p.Rt);
    }

    /// <summary>Pearson's r between this trace and another over this peak's bounds, the other read at the nearest time; null when fewer than three points or a flat side.</summary>
    public double? CorrelationWith(IReadOnlyList<ChromatogramPoint> other)
    {
        if (!Found || other.Count == 0) return null;
        var xs = new List<double>();
        var ys = new List<double>();
        for (var i = Left; i <= Right; i++)
        {
            var rt = Trace[i].Rt;
            xs.Add(Trace[i].Intensity);
            ys.Add(Nearest(other, rt));
        }
        if (xs.Count < 3) return null;
        var mx = xs.Average();
        var my = ys.Average();
        double sxy = 0, sxx = 0, syy = 0;
        for (var i = 0; i < xs.Count; i++)
        {
            sxy += (xs[i] - mx) * (ys[i] - my);
            sxx += (xs[i] - mx) * (xs[i] - mx);
            syy += (ys[i] - my) * (ys[i] - my);
        }
        if (sxx <= 0 || syy <= 0) return null;
        return sxy / Math.Sqrt(sxx * syy);
    }

    private static double Nearest(IReadOnlyList<ChromatogramPoint> points, double rt)
    {
        int lo = 0, hi = points.Count - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) >> 1;
            if (points[mid].Rt < rt) lo = mid + 1; else hi = mid;
        }
        if (lo > 0 && Math.Abs(points[lo - 1].Rt - rt) < Math.Abs(points[lo].Rt - rt)) lo--;
        return points[lo].Intensity;
    }

    public JsonObject ToJson()
    {
        var o = new JsonObject { ["found"] = Found };
        if (!Found)
        {
            o["points_in_window"] = Trace.Count;
            return o;
        }
        o["apex_rt"] = Num.Rt(ApexRt);
        o["apex_height"] = Num.Intensity(ApexHeight);
        o["area"] = Num.Intensity(Math.Round(Area, 6));
        o["fwhm"] = Fwhm is { } f ? Num.Rt(f) : null;
        o["points_across_peak"] = Points;
        o["bounds"] = new JsonObject { ["low"] = Num.Rt(Trace[Left].Rt), ["high"] = Num.Rt(Trace[Right].Rt) };
        o["noise"] = Noise is { } n ? Num.Intensity(Math.Round(n, 6)) : null;
        o["snr"] = Snr is { } s ? Num.Ratio(s) : null;
        o["points_in_window"] = Trace.Count;
        return o;
    }
}
