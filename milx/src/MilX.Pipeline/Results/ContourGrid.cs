using CompMs.Common.DataObj;

namespace MilX.Pipeline.Results;

/// <summary>
/// A channel as a surface: retention time across, m/z up, intensity as colour. A chromatogram is the
/// surface summed along one axis and a spectrum is it summed along the other, so everything here is
/// visible elsewhere one slice at a time; what the contour adds is adjacency — an interference half a
/// dalton from a target, a ridge running the whole run at one mass.
///
/// This is arithmetic over the spectra already read and is tested as arithmetic; nothing is quantified
/// from it. Its m/z bins are wider than the instrument's steps and its rows may be several scans
/// averaged, which is why "Extract this view" goes back to the reader for the XIC and the spectrum.
/// Ported from MIL-Q's contour.py, with the same defaults.
/// </summary>
public sealed class ContourGrid
{
    /// <summary>How finely m/z is divided: about a bin per screen pixel across a wide range.</summary>
    public const int DefaultBins = 1400;
    /// <summary>The most rows drawn; the scans over it are averaged into their row, never skipped.</summary>
    public const int DefaultRows = 900;
    public const int MinBins = 32;

    private ContourGrid(double[,] intensity, double[] rt, double[] mzEdges, int scans, int grouped, string note)
    {
        Intensity = intensity;
        Rt = rt;
        MzEdges = mzEdges;
        Scans = scans;
        Grouped = grouped;
        Note = note;
    }

    /// <summary>[row, bin]: rows in time order, bins in mass order.</summary>
    public double[,] Intensity { get; }
    /// <summary>The centre time of each row, in minutes.</summary>
    public double[] Rt { get; }
    /// <summary>bins + 1 edges, so a value can be placed without guessing the width.</summary>
    public double[] MzEdges { get; }
    /// <summary>How many scans were read into the grid.</summary>
    public int Scans { get; }
    /// <summary>How many scans were averaged into one row; 1 when none had to be.</summary>
    public int Grouped { get; }
    public string Note { get; }

    public int Rows => Intensity.GetLength(0);
    public int Bins => Intensity.GetLength(1);
    public bool IsEmpty => Rows == 0 || Bins == 0;
    public (double Lo, double Hi) MzRange => MzEdges.Length < 2 ? (0, 0) : (MzEdges[0], MzEdges[^1]);
    public (double Lo, double Hi) RtRange => Rt.Length == 0 ? (0, 0) : (Rt[0], Rt[^1]);

    public static ContourGrid Empty(string note) => new(new double[0, 0], Array.Empty<double>(), Array.Empty<double>(), 0, 1, note);

    public double MzCentre(int bin) => (MzEdges[bin] + MzEdges[bin + 1]) / 2.0;

    /// <summary>The column a mass falls in, or -1 outside the range. A value on an edge goes to the bin to its right, as a histogram puts it.</summary>
    public int BinOf(double mz)
    {
        if (MzEdges.Length < 2 || mz < MzEdges[0] || mz > MzEdges[^1]) return -1;
        var i = Array.BinarySearch(MzEdges, mz);
        var column = i >= 0 ? i : ~i - 1;
        return Math.Clamp(column, 0, Bins - 1);
    }

    /// <summary>The row whose centre time is nearest.</summary>
    public int RowOf(double rt)
    {
        if (Rt.Length == 0) return -1;
        var i = Array.BinarySearch(Rt, rt);
        if (i >= 0) return i;
        var after = ~i;
        if (after <= 0) return 0;
        if (after >= Rt.Length) return Rt.Length - 1;
        return rt - Rt[after - 1] <= Rt[after] - rt ? after - 1 : after;
    }

    /// <summary>The intensity of the cell a point falls in, or null outside the grid.</summary>
    public double? At(double rt, double mz)
    {
        if (IsEmpty) return null;
        var column = BinOf(mz);
        var row = RowOf(rt);
        if (column < 0 || row < 0) return null;
        return Intensity[row, column];
    }

    /// <summary>The chromatogram of an m/z band: the surface summed upwards. A picture of the grid, not a substitute for an XIC from the reader.</summary>
    public IReadOnlyList<ChromatogramPoint> SliceRt(double mzLo, double mzHi)
    {
        if (IsEmpty) return Array.Empty<ChromatogramPoint>();
        if (mzHi < mzLo) (mzLo, mzHi) = (mzHi, mzLo);
        var points = new ChromatogramPoint[Rows];
        for (var r = 0; r < Rows; r++)
        {
            double sum = 0;
            for (var b = 0; b < Bins; b++)
            {
                var c = MzCentre(b);
                if (c >= mzLo && c <= mzHi) sum += Intensity[r, b];
            }
            points[r] = new ChromatogramPoint(Rt[r], sum);
        }
        return points;
    }

    /// <summary>The averaged spectrum of a time band: the surface summed across, as bin centres.</summary>
    public IReadOnlyList<SpectrumPeakPoint> SliceMz(double rtLo, double rtHi)
    {
        if (IsEmpty) return Array.Empty<SpectrumPeakPoint>();
        if (rtHi < rtLo) (rtLo, rtHi) = (rtHi, rtLo);
        var rows = Enumerable.Range(0, Rows).Where(r => Rt[r] >= rtLo && Rt[r] <= rtHi).ToList();
        var points = new SpectrumPeakPoint[Bins];
        for (var b = 0; b < Bins; b++)
        {
            double sum = 0;
            foreach (var r in rows) sum += Intensity[r, b];
            points[b] = new SpectrumPeakPoint(MzCentre(b), rows.Count == 0 ? 0 : sum / rows.Count);
        }
        return points;
    }

    /// <summary>
    /// The top of a colour scale: a high percentile of the non-zero cells rather than the maximum, so one
    /// saturated or spiking scan does not set the scale for everything else and leave the surface black.
    /// </summary>
    public double Percentile(double percent = 99.7)
    {
        var values = new List<double>();
        foreach (var v in Intensity) if (v > 0) values.Add(v);
        if (values.Count == 0) return 0;
        values.Sort();
        var k = Math.Clamp((int)Math.Round((values.Count - 1) * percent / 100.0), 0, values.Count - 1);
        return values[k];
    }

    /// <summary>
    /// Reads a channel's scans into a retention time by m/z grid. <paramref name="indices"/> are the scans
    /// of the channel in time order; the mass range defaults to the scan window the first spectrum
    /// declares, or to the observed range. Zeros are not restored: a histogram of intensities is
    /// unchanged by points of intensity zero.
    /// </summary>
    public static ContourGrid Build(RawMeasurement raw, IReadOnlyList<int> indices, (double Lo, double Hi)? rtRange = null, (double Lo, double Hi)? mzRange = null,
        int bins = DefaultBins, int maxRows = DefaultRows, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(indices);
        if (indices.Count == 0) return Empty("the channel has no scans");
        var spectra = raw.SpectrumList;

        var scans = indices.ToList();
        if (rtRange is { } rr)
        {
            var (lo, hi) = rr.Lo <= rr.Hi ? rr : (rr.Hi, rr.Lo);
            scans = scans.Where(i => { var t = RawExplorer.RtMinutes(spectra[i]); return t >= lo && t <= hi; }).ToList();
            if (scans.Count == 0) return Empty($"no scans between {lo:F2} and {hi:F2} min");
        }

        double mzLo, mzHi;
        if (mzRange is { } mr)
        {
            (mzLo, mzHi) = mr.Lo <= mr.Hi ? mr : (mr.Hi, mr.Lo);
        }
        else
        {
            var first = spectra[scans[0]];
            if (first.ScanWindowUpperLimit > first.ScanWindowLowerLimit && first.ScanWindowUpperLimit > 0)
            {
                (mzLo, mzHi) = (first.ScanWindowLowerLimit, first.ScanWindowUpperLimit);
            }
            else
            {
                mzLo = double.MaxValue; mzHi = 0;
                foreach (var i in scans)
                {
                    var s = spectra[i];
                    if (s.Spectrum is not { Length: > 0 } p) continue;
                    mzLo = Math.Min(mzLo, p[0].Mz);
                    mzHi = Math.Max(mzHi, p[^1].Mz);
                }
            }
        }
        if (double.IsInfinity(mzLo) || double.IsNaN(mzLo) || mzLo == double.MaxValue || mzHi <= mzLo) return Empty("the channel does not declare a mass range");

        bins = Math.Max(bins, MinBins);
        var edges = new double[bins + 1];
        for (var b = 0; b <= bins; b++) edges[b] = mzLo + (mzHi - mzLo) * b / bins;

        var group = Math.Max(1, (int)Math.Ceiling(scans.Count / (double)Math.Max(maxRows, 1)));
        var rows = (int)Math.Ceiling(scans.Count / (double)group);
        var grid = new double[rows, bins];
        var counts = new double[rows];
        var rowTime = new double[rows];
        var read = 0;
        var stopped = false;
        var width = (mzHi - mzLo) / bins;
        for (var number = 0; number < scans.Count; number++)
        {
            if (ct.IsCancellationRequested) { stopped = true; break; }
            var row = number / group;
            var s = spectra[scans[number]];
            if (s.Spectrum is { Length: > 0 } peaks)
            {
                foreach (var p in peaks)
                {
                    if (p.Mz < mzLo || p.Mz > mzHi || p.Intensity <= 0) continue;
                    var b = (int)((p.Mz - mzLo) / width);
                    if (b >= bins) b = bins - 1;   // the top edge belongs to the last bin, as numpy's histogram has it
                    grid[row, b] += p.Intensity;
                }
            }
            counts[row] += 1;
            rowTime[row] += RawExplorer.RtMinutes(s);
            read++;
        }
        var keep = counts.Count(c => c > 0);
        var intensity = new double[keep, bins];
        var rt = new double[keep];
        for (var r = 0; r < keep; r++)
        {
            // the mean, not the sum: the last group is usually short, and a row dimmer only for that would be a lie about the data
            var n = counts[r] > 0 ? counts[r] : 1;
            for (var b = 0; b < bins; b++) intensity[r, b] = grid[r, b] / n;
            rt[r] = rowTime[r] / n;
        }
        var note = stopped ? $"stopped after {read} of {scans.Count} scans" : group > 1 ? $"{group} scans averaged into each row" : string.Empty;
        return new ContourGrid(intensity, rt, edges, read, group, note);
    }
}
