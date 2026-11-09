using System.Globalization;

namespace MilX.Pipeline.Results;

/// <summary>How the noise of a region is measured. Both are in use in laboratories, so the one a number came from is always stated beside it.</summary>
public enum NoiseMode
{
    /// <summary>The full swing of the region: the stricter, more conservative reading.</summary>
    PeakToPeak,
    /// <summary>The standard deviation of the region's points: the gentler one.</summary>
    StandardDeviation,
}

/// <summary>A noise measurement: the value, where and how it was taken, and how many points it stood on.</summary>
public sealed record NoiseEstimate(double Noise, double Start, double End, NoiseMode Mode, int Points)
{
    public string Method => Mode == NoiseMode.PeakToPeak ? "peak-to-peak" : "standard deviation";
    public string Describe() => $"noise {Start.ToString("F2", CultureInfo.InvariantCulture)}–{End.ToString("F2", CultureInfo.InvariantCulture)} min, {Method}";
}

/// <summary>
/// MIL-Q's signal-to-noise convention: a peak's height divided by a measurement of the noise taken
/// over a stretch of baseline the reviewer chose — never inside the peak's own window, where the
/// point-to-point spread reports the peak's slope. MS-DIAL's own S/N (its estimate at peak-picking
/// time) is kept untouched beside it; the two are labelled.
///
/// When the region holds nothing to measure — fewer than two points, or a trace of exact zeros — the
/// answer is "not measured" rather than a number: a baseline below the instrument's reporting
/// threshold has no noise to divide by, and a ratio invented for it would be a ratio to nothing.
/// </summary>
public static class SignalToNoise
{
    /// <summary>The noise of the points of <paramref name="trace"/> between two retention times, or null when it cannot be measured.</summary>
    public static NoiseEstimate? Measure(IReadOnlyList<ChromatogramPoint> trace, double start, double end, NoiseMode mode)
    {
        if (trace is null || double.IsNaN(start) || double.IsNaN(end)) return null;
        if (end < start) (start, end) = (end, start);
        var values = new List<double>();
        foreach (var p in trace)
        {
            if (p.Rt >= start && p.Rt <= end) values.Add(p.Intensity);
        }
        if (values.Count < 2) return null;
        double noise;
        if (mode == NoiseMode.PeakToPeak)
        {
            noise = values.Max() - values.Min();
        }
        else
        {
            var mean = values.Average();
            var ss = values.Sum(v => (v - mean) * (v - mean));
            noise = Math.Sqrt(ss / (values.Count - 1));
        }
        if (!(noise > 0)) return null;
        return new NoiseEstimate(noise, start, end, mode, values.Count);
    }

    /// <summary>
    /// The automatic estimate over a whole trace, when no region is set: the median absolute deviation of
    /// the point-to-point differences scaled to a standard deviation (a robust estimator a peak cannot
    /// inflate), with the standard deviation of the lower half of the points as the fallback when more
    /// than half the differences are exactly zero. Null when neither can be measured.
    /// </summary>
    public static double? Automatic(IReadOnlyList<ChromatogramPoint> trace)
    {
        if (trace is null || trace.Count < 3) return null;
        var diffs = new List<double>(trace.Count - 1);
        for (var i = 1; i < trace.Count; i++) diffs.Add(Math.Abs(trace[i].Intensity - trace[i - 1].Intensity));
        var median = Median(diffs);
        if (median > 0)
        {
            var deviations = diffs.Select(d => Math.Abs(d - median)).ToList();
            var mad = Median(deviations);
            // differences of two independent noise samples carry √2 of the noise; 1.4826 scales a MAD to a standard deviation
            var sd = mad * 1.4826 / Math.Sqrt(2);
            if (sd > 0) return sd;
        }
        var sorted = trace.Select(p => p.Intensity).OrderBy(v => v).ToList();
        var lower = sorted.Take(Math.Max(2, sorted.Count / 2)).ToList();
        var mean = lower.Average();
        var variance = lower.Sum(v => (v - mean) * (v - mean)) / (lower.Count - 1);
        var fallback = Math.Sqrt(variance);
        return fallback > 0 ? fallback : null;
    }

    /// <summary>Height over noise; null when the noise is not measured.</summary>
    public static double? Ratio(double height, NoiseEstimate? noise) => noise is null || noise.Noise <= 0 || height <= 0 ? null : height / noise.Noise;

    private static double Median(List<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        var n = sorted.Count;
        if (n == 0) return 0;
        return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) / 2;
    }
}
