using System.Globalization;
using System.Text;

namespace OpenDIAL.Pipeline.Results;

/// <summary>What a library holds, read off the file itself rather than trusted from its name.</summary>
public sealed record LibrarySummary(
    string Path,
    int Records,
    int WithRetentionTime,
    double RetentionTimeLow,
    double RetentionTimeHigh,
    double MassLow,
    double MassHigh,
    int MedianPeakCount,
    IReadOnlyList<(string Adduct, int Count)> Adducts)
{
    public bool CarriesRetentionTimes => WithRetentionTime > Records / 2;

    /// <summary>
    /// Whether the library's retention times could belong to this run at all. A library built on an
    /// eighteen-minute gradient against injections that finish in eight has nothing to say about
    /// time, and scoring with it throws correct matches away.
    /// </summary>
    public bool RetentionTimesCouldFit(double runLow, double runHigh)
    {
        if (!CarriesRetentionTimes) return false;
        var overlap = Math.Min(RetentionTimeHigh, runHigh) - Math.Max(RetentionTimeLow, runLow);
        var span = Math.Max(1e-6, RetentionTimeHigh - RetentionTimeLow);
        return overlap > span * 0.25;
    }

    public string Sentence()
    {
        if (Records == 0) return "No records read.";
        var adducts = string.Join(", ", Adducts.Take(4).Select(a => $"{a.Adduct} ({a.Count:N0})"));
        var rt = CarriesRetentionTimes
            ? $"retention times from {RetentionTimeLow:0.##} to {RetentionTimeHigh:0.##} min"
            : "no retention times";
        return $"{Records:N0} records · m/z {MassLow:0.#}–{MassHigh:0.#} · {rt} · median {MedianPeakCount} peaks · {adducts}";
    }
}

/// <summary>The fit that puts a library's retention times on the gradient actually run.</summary>
public sealed record RetentionCalibration(int Pairs, double Slope, double Intercept, double RSquared, string WrittenTo)
{
    public double Apply(double libraryRt) => Math.Max(0, Slope * libraryRt + Intercept);
}

/// <summary>
/// Reading an `.msp` for what it is, and putting its retention times on your own gradient.
///
/// A library is the single biggest lever on how many features get a name, and the one thing the
/// interface never said anything about. Scanning is a plain pass over the file — a library of four
/// hundred thousand records reads in a second or two — and the calibration is a least-squares fit
/// of the library's times against the times the run actually saw for the compounds it identified.
/// </summary>
public static class SpectralLibrary
{
    public static LibrarySummary Scan(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new LibrarySummary(path ?? string.Empty, 0, 0, 0, 0, 0, 0, 0, Array.Empty<(string, int)>());
        }
        var records = 0;
        var withRt = 0;
        double rtLow = double.MaxValue, rtHigh = double.MinValue, mzLow = double.MaxValue, mzHigh = double.MinValue;
        var adducts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var peakCounts = new List<int>();
        var sawName = false;

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.AsSpan().Trim();
            if (line.IsEmpty) continue;
            if (StartsWith(line, "NAME:"))
            {
                if (sawName) records++;
                sawName = true;
            }
            else if (StartsWith(line, "PRECURSORMZ:"))
            {
                if (Number(line, out var mz)) { mzLow = Math.Min(mzLow, mz); mzHigh = Math.Max(mzHigh, mz); }
            }
            else if (StartsWith(line, "RETENTIONTIME:"))
            {
                if (Number(line, out var rt) && rt > 0)
                {
                    withRt++;
                    rtLow = Math.Min(rtLow, rt);
                    rtHigh = Math.Max(rtHigh, rt);
                }
            }
            else if (StartsWith(line, "PRECURSORTYPE:"))
            {
                var value = After(line);
                if (value.Length > 0) adducts[value] = adducts.TryGetValue(value, out var n) ? n + 1 : 1;
            }
            else if (StartsWith(line, "NUM PEAKS:"))
            {
                if (Number(line, out var count)) peakCounts.Add((int)count);
            }
        }
        if (sawName) records++;
        peakCounts.Sort();
        return new LibrarySummary(
            path,
            records,
            withRt,
            rtLow == double.MaxValue ? 0 : rtLow,
            rtHigh == double.MinValue ? 0 : rtHigh,
            mzLow == double.MaxValue ? 0 : mzLow,
            mzHigh == double.MinValue ? 0 : mzHigh,
            peakCounts.Count == 0 ? 0 : peakCounts[peakCounts.Count / 2],
            adducts.OrderByDescending(a => a.Value).Select(a => (a.Key, a.Value)).ToList());
    }

    /// <summary>
    /// Fits the library's retention times to the ones the run measured for the compounds it named,
    /// and writes a calibrated copy beside the original. Answers null when too few names are shared
    /// to fit anything honest.
    /// </summary>
    public static RetentionCalibration? Recalibrate(string path, IReadOnlyDictionary<string, double> observedByName, int minimumPairs = 20)
    {
        if (!File.Exists(path) || observedByName.Count == 0) return null;
        var libraryRt = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        string? name = null;
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.AsSpan().Trim();
            if (StartsWith(line, "NAME:")) name = After(line);
            else if (StartsWith(line, "RETENTIONTIME:") && name is { Length: > 0 } && Number(line, out var rt) && rt > 0)
            {
                if (!libraryRt.ContainsKey(name)) libraryRt[name] = rt;
            }
        }

        var xs = new List<double>();
        var ys = new List<double>();
        foreach (var (named, observed) in observedByName)
        {
            if (libraryRt.TryGetValue(named, out var reference)) { xs.Add(reference); ys.Add(observed); }
        }
        if (xs.Count < minimumPairs) return null;

        var meanX = xs.Average();
        var meanY = ys.Average();
        var sxx = xs.Sum(x => (x - meanX) * (x - meanX));
        if (sxx <= 1e-9) return null;
        var sxy = 0.0;
        for (var i = 0; i < xs.Count; i++) sxy += (xs[i] - meanX) * (ys[i] - meanY);
        var slope = sxy / sxx;
        var intercept = meanY - slope * meanX;
        var ssTot = ys.Sum(y => (y - meanY) * (y - meanY));
        var ssRes = 0.0;
        for (var i = 0; i < xs.Count; i++)
        {
            var fitted = slope * xs[i] + intercept;
            ssRes += (ys[i] - fitted) * (ys[i] - fitted);
        }
        var r2 = ssTot <= 1e-12 ? 0 : 1 - ssRes / ssTot;

        var written = System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(path) ?? ".",
            System.IO.Path.GetFileNameWithoutExtension(path) + "-rt-calibrated" + System.IO.Path.GetExtension(path));
        var calibration = new RetentionCalibration(xs.Count, slope, intercept, r2, written);

        using (var writer = new StreamWriter(written, false, Encoding.UTF8))
        {
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.AsSpan().Trim();
                if (StartsWith(line, "RETENTIONTIME:") && Number(line, out var rt))
                {
                    writer.WriteLine("RETENTIONTIME: " + calibration.Apply(rt).ToString("0.####", CultureInfo.InvariantCulture));
                }
                else
                {
                    writer.WriteLine(raw);
                }
            }
        }
        return calibration;
    }

    private static bool StartsWith(ReadOnlySpan<char> line, string prefix) =>
        line.Length >= prefix.Length && line[..prefix.Length].Equals(prefix, StringComparison.OrdinalIgnoreCase);

    private static string After(ReadOnlySpan<char> line)
    {
        var colon = line.IndexOf(':');
        return colon < 0 ? string.Empty : line[(colon + 1)..].Trim().ToString();
    }

    private static bool Number(ReadOnlySpan<char> line, out double value) =>
        double.TryParse(After(line), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
