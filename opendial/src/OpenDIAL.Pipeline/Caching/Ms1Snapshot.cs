using CompMs.Common.DataObj;
using OpenDIAL.Pipeline.Results;

namespace OpenDIAL.Pipeline.Caching;

/// <summary>
/// The survey scans of one raw file reduced to what an extracted ion chromatogram needs: the
/// retention time of every MS1 scan and its centroids. It is a fraction of the raw file and needs
/// no vendor library to read, which is the point — reopening a project should not mean waiting for
/// the instrument's own reader again.
///
/// Masses are stored as tenths of a millidalton in a 32-bit integer. That is 0.0001 Da, two orders
/// finer than any tolerance a chromatogram is extracted with, and it halves both the file and the
/// time to read it.
/// </summary>
public sealed class Ms1Snapshot
{
    private const uint Magic = 0x314D5344;   // "DSM1"
    private const int Version = 1;
    private const double MassScale = 10000.0;

    private readonly double[] _retentionTimes;
    private readonly int[][] _masses;
    private readonly float[][] _intensities;

    private Ms1Snapshot(double[] retentionTimes, int[][] masses, float[][] intensities)
    {
        _retentionTimes = retentionTimes;
        _masses = masses;
        _intensities = intensities;
    }

    public int ScanCount => _retentionTimes.Length;
    public long PeakCount => _masses.Sum(m => (long)m.Length);

    public static Ms1Snapshot FromMeasurement(RawMeasurement raw, IReadOnlyList<int> ms1Indices)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(ms1Indices);
        var rts = new double[ms1Indices.Count];
        var masses = new int[ms1Indices.Count][];
        var intensities = new float[ms1Indices.Count][];
        for (var i = 0; i < ms1Indices.Count; i++)
        {
            var spectrum = raw.SpectrumList[ms1Indices[i]];
            rts[i] = RawExplorer.RtMinutes(spectrum);
            var peaks = spectrum.Spectrum ?? Array.Empty<RawPeakElement>();
            var m = new int[peaks.Length];
            var i2 = new float[peaks.Length];
            for (var j = 0; j < peaks.Length; j++)
            {
                m[j] = (int)Math.Round(peaks[j].Mz * MassScale);
                i2[j] = (float)peaks[j].Intensity;
            }
            masses[i] = m;
            intensities[i] = i2;
        }
        return new Ms1Snapshot(rts, masses, intensities);
    }

    /// <summary>
    /// The extracted ion chromatogram, summing every centroid inside the window of each scan —
    /// the same definition <see cref="RawExplorer.Xic"/> uses on the raw measurement.
    /// </summary>
    public Chromatogram Xic(double mz, double toleranceDa, string? label = null)
    {
        var lo = (int)Math.Round((mz - toleranceDa) * MassScale);
        var hi = (int)Math.Round((mz + toleranceDa) * MassScale);
        var points = new List<ChromatogramPoint>(ScanCount);
        for (var i = 0; i < ScanCount; i++)
        {
            var masses = _masses[i];
            var intensities = _intensities[i];
            double sum = 0;
            // the centroids of a scan are in ascending mass, so the window is one contiguous run
            var start = LowerBound(masses, lo);
            for (var j = start; j < masses.Length && masses[j] <= hi; j++) sum += intensities[j];
            points.Add(new ChromatogramPoint(_retentionTimes[i], sum));
        }
        return new Chromatogram(label ?? $"XIC {mz:F4} ± {toleranceDa:F4}", mz, toleranceDa, points);
    }

    private static int LowerBound(int[] values, int target)
    {
        int lo = 0, hi = values.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (values[mid] < target) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    public void Write(Stream stream)
    {
        using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        writer.Write(Magic);
        writer.Write(Version);
        writer.Write(ScanCount);
        foreach (var rt in _retentionTimes) writer.Write(rt);
        for (var i = 0; i < ScanCount; i++)
        {
            writer.Write(_masses[i].Length);
            foreach (var m in _masses[i]) writer.Write(m);
            foreach (var v in _intensities[i]) writer.Write(v);
        }
    }

    public static Ms1Snapshot? Read(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        if (reader.ReadUInt32() != Magic) return null;
        if (reader.ReadInt32() != Version) return null;
        var scans = reader.ReadInt32();
        if (scans < 0 || scans > 10_000_000) return null;
        var rts = new double[scans];
        for (var i = 0; i < scans; i++) rts[i] = reader.ReadDouble();
        var masses = new int[scans][];
        var intensities = new float[scans][];
        for (var i = 0; i < scans; i++)
        {
            var count = reader.ReadInt32();
            if (count < 0 || count > 50_000_000) return null;
            var m = new int[count];
            for (var j = 0; j < count; j++) m[j] = reader.ReadInt32();
            var v = new float[count];
            for (var j = 0; j < count; j++) v[j] = reader.ReadSingle();
            masses[i] = m;
            intensities[i] = v;
        }
        return new Ms1Snapshot(rts, masses, intensities);
    }
}
