using CompMs.Common.DataObj;
using CompMs.Common.Enum;

namespace MilX.Pipeline.Results;

public enum RawChannelKind
{
    /// <summary>MS1 survey scans.</summary>
    Ms1,
    /// <summary>All data-dependent MS/MS events of the file (precursor varies scan to scan).</summary>
    Ms2Events,
    /// <summary>One data-independent isolation window (SWATH/AIF), constant across the run.</summary>
    Ms2Window,
}

/// <summary>A set of spectra that form one chromatographic channel of a raw file.</summary>
public sealed class RawChannel
{
    public RawChannel(RawChannelKind kind, string label, IReadOnlyList<int> spectrumIndices, double precursorMz, double collisionEnergy, int experimentId)
    {
        Kind = kind;
        Label = label;
        SpectrumIndices = spectrumIndices;
        PrecursorMz = precursorMz;
        CollisionEnergy = collisionEnergy;
        ExperimentId = experimentId;
    }

    public RawChannelKind Kind { get; }
    public string Label { get; }
    /// <summary>Indices into <see cref="RawMeasurement.SpectrumList"/>, ascending in time.</summary>
    public IReadOnlyList<int> SpectrumIndices { get; }
    public double PrecursorMz { get; }
    public double CollisionEnergy { get; }
    public int ExperimentId { get; }
    public int Count => SpectrumIndices.Count;
}

/// <summary>Summary of one raw spectrum for tables/labels.</summary>
public readonly record struct RawScanInfo(int Index, double Rt, int MsLevel, double PrecursorMz, double CollisionEnergy, double TotalIonCurrent, double BasePeakIntensity, int PeakCount);

/// <summary>
/// In-memory chromatogram and spectrum extraction over a <see cref="RawMeasurement"/> for qualitative review
/// (TIC, BPC, XIC, channel discovery, scan lookup and range averaging). Everything is pure and thread safe.
/// </summary>
public static class RawExplorer
{
    public static double RtMinutes(RawSpectrum s) => s.ScanStartTimeUnit == Units.Second ? s.ScanStartTime / 60.0 : s.ScanStartTime;

    public static RawScanInfo Describe(RawMeasurement raw, int index)
    {
        var s = raw.SpectrumList[index];
        return new RawScanInfo(index, RtMinutes(s), s.MsLevel, s.Precursor?.SelectedIonMz ?? 0, s.CollisionEnergy > 0 ? s.CollisionEnergy : (s.Precursor?.CollisionEnergy ?? 0),
            s.TotalIonCurrent, s.BasePeakIntensity, s.Spectrum?.Length ?? 0);
    }

    /// <summary>
    /// Splits the spectra of a file into channels: the MS1 scans, plus either one "MS2 events" channel (DDA)
    /// or one channel per isolation window (SWATH / AIF). Windows are recognised by a constant isolation target
    /// (and collision energy) repeated over the run.
    /// </summary>
    public static IReadOnlyList<RawChannel> DiscoverChannels(RawMeasurement raw)
    {
        ArgumentNullException.ThrowIfNull(raw);
        var spectra = raw.SpectrumList;
        if (HasExperiments(raw))
        {
            return DiscoverExperimentChannels(raw);
        }
        var channels = new List<RawChannel>();
        var ms1 = new List<int>();
        var ms2 = new List<int>();
        for (var i = 0; i < spectra.Count; i++)
        {
            var s = spectra[i];
            if (s.MsLevel <= 1) ms1.Add(i); else ms2.Add(i);
        }
        if (ms1.Count > 0)
        {
            channels.Add(new RawChannel(RawChannelKind.Ms1, "MS1", ms1, 0, 0, 0));
        }
        if (ms2.Count == 0)
        {
            return channels;
        }

        // group MS2 scans by (isolation target rounded to 0.1, CE rounded)
        var groups = new Dictionary<(long Mz, int Ce), List<int>>();
        foreach (var i in ms2)
        {
            var s = spectra[i];
            var target = s.Precursor?.IsolationTargetMz > 0 ? s.Precursor.IsolationTargetMz : (s.Precursor?.SelectedIonMz ?? 0);
            var ce = s.CollisionEnergy > 0 ? s.CollisionEnergy : (s.Precursor?.CollisionEnergy ?? 0);
            var key = ((long)Math.Round(target * 10), (int)Math.Round(ce));
            if (!groups.TryGetValue(key, out var list))
            {
                groups[key] = list = new List<int>();
            }
            list.Add(i);
        }

        // DDA: many different precursors, each seen a handful of times; DIA: few windows, each seen many times
        var isDda = groups.Count > 64 || groups.Count > ms2.Count / 4.0;
        if (isDda)
        {
            channels.Add(new RawChannel(RawChannelKind.Ms2Events, $"MS2 events ({ms2.Count})", ms2, 0, 0, 0));
            return channels;
        }
        foreach (var (key, list) in groups.OrderBy(g => g.Key.Mz).ThenBy(g => g.Key.Ce))
        {
            var first = spectra[list[0]];
            var lower = first.Precursor?.IsolationWindowLowerOffset ?? 0;
            var upper = first.Precursor?.IsolationWindowUpperOffset ?? 0;
            var target = key.Mz / 10.0;
            var label = lower > 0 || upper > 0
                ? $"MS2 {target - lower:F1}–{target + upper:F1}"
                : $"MS2 {target:F1}";
            if (key.Ce > 0) label += $", CE {key.Ce}";
            channels.Add(new RawChannel(RawChannelKind.Ms2Window, label, list, target, key.Ce, first.ExperimentID));
        }
        return channels;
    }

    /// <summary>True when the reader tagged spectra with acquisition-method experiments (SCIEX .wiff: 0 = survey, 1.. = product-ion / SWATH channels).</summary>
    public static bool HasExperiments(RawMeasurement raw)
    {
        foreach (var s in raw.SpectrumList)
        {
            if (s.ExperimentID > 0) return true;
        }
        return false;
    }

    /// <summary>
    /// One channel per acquisition experiment, in method order, labelled the way OpenQuant does:
    /// "TOF MS  100–2000" for MS1 experiments, "MS2 351.20 → 50–360, CE -30" for product-ion / SWATH
    /// experiments (an experiment whose precursor changes scan to scan is an IDA dependent-scan slot).
    /// </summary>
    public static IReadOnlyList<RawChannel> DiscoverExperimentChannels(RawMeasurement raw)
    {
        var spectra = raw.SpectrumList;
        var byExperiment = new SortedDictionary<int, List<int>>();
        for (var i = 0; i < spectra.Count; i++)
        {
            var e = Math.Max(0, spectra[i].ExperimentID);
            if (!byExperiment.TryGetValue(e, out var list)) byExperiment[e] = list = new List<int>();
            list.Add(i);
        }
        var channels = new List<RawChannel>();
        foreach (var (e, list) in byExperiment)
        {
            var first = spectra[list[0]];
            var (lo, hi) = MassRange(spectra, list);
            var range = hi > lo ? $"{lo:F0}–{hi:F0}" : string.Empty;
            if (first.MsLevel <= 1)
            {
                channels.Add(new RawChannel(RawChannelKind.Ms1, string.IsNullOrEmpty(range) ? "TOF MS" : $"TOF MS  {range}", list, 0, 0, e));
                continue;
            }
            var precursors = new HashSet<long>();
            double ceSum = 0;
            var ceCount = 0;
            foreach (var i in list)
            {
                var s = spectra[i];
                var p = s.Precursor?.IsolationTargetMz > 0 ? s.Precursor.IsolationTargetMz : (s.Precursor?.SelectedIonMz ?? 0);
                precursors.Add((long)Math.Round(p * 10));
                var ce = s.CollisionEnergy != 0 ? s.CollisionEnergy : (s.Precursor?.CollisionEnergy ?? 0);
                if (ce != 0) { ceSum += ce; ceCount++; }
            }
            var meanCe = ceCount > 0 ? ceSum / ceCount : 0;
            var ceLabel = ceCount > 0 ? $", CE {meanCe:F0}" : string.Empty;
            if (precursors.Count > Math.Max(4, list.Count / 4.0))
            {
                channels.Add(new RawChannel(RawChannelKind.Ms2Events, $"IDA MS2 slot {e}" + (string.IsNullOrEmpty(range) ? string.Empty : $" → {range}") + ceLabel, list, 0, meanCe, e));
                continue;
            }
            var target = first.Precursor?.IsolationTargetMz > 0 ? first.Precursor.IsolationTargetMz : (first.Precursor?.SelectedIonMz ?? 0);
            var lower = first.Precursor?.IsolationWindowLowerOffset ?? 0;
            var upper = first.Precursor?.IsolationWindowUpperOffset ?? 0;
            var isWide = lower + upper > 2.5;
            var head = isWide ? $"MS2 {target - lower:F1}–{target + upper:F1}" : $"MS2 {target:F2}";
            var label = head + (string.IsNullOrEmpty(range) ? string.Empty : $" → {range}") + ceLabel;
            channels.Add(new RawChannel(RawChannelKind.Ms2Window, label, list, target, meanCe, e));
        }
        return channels;
    }

    private static (double Lo, double Hi) MassRange(List<RawSpectrum> spectra, List<int> indices)
    {
        var first = spectra[indices[0]];
        if (first.ScanWindowUpperLimit > first.ScanWindowLowerLimit && first.ScanWindowUpperLimit > 0)
        {
            return (first.ScanWindowLowerLimit, first.ScanWindowUpperLimit);
        }
        double lo = double.MaxValue, hi = 0;
        foreach (var i in indices)
        {
            var s = spectra[i];
            if (s.LowestObservedMz > 0 && s.LowestObservedMz < lo) lo = s.LowestObservedMz;
            if (s.HighestObservedMz > hi) hi = s.HighestObservedMz;
        }
        return lo == double.MaxValue ? (0, 0) : (lo, hi);
    }

    /// <summary>
    /// The whole-sample TIC. For experiment-tagged files every experiment of a cycle is summed into one point
    /// (like OpenQuant's sample TIC); otherwise it is the TIC of the MS1 scans.
    /// </summary>
    public static Chromatogram SampleTic(RawMeasurement raw, IReadOnlyList<int> ms1Indices)
    {
        if (!HasExperiments(raw))
        {
            return Tic(raw, ms1Indices);
        }
        var spectra = raw.SpectrumList;
        var cycles = new SortedDictionary<int, (double Rt, double Sum, bool HasSurvey)>();
        foreach (var s in spectra)
        {
            var tic = s.TotalIonCurrent > 0 ? s.TotalIonCurrent : Sum(s.Spectrum);
            var rt = RtMinutes(s);
            if (cycles.TryGetValue(s.ScanNumber, out var c))
            {
                // the survey scan's time stands for the cycle; otherwise the earliest experiment's
                var useRt = s.ExperimentID == 0 && !c.HasSurvey ? rt : (c.HasSurvey ? c.Rt : Math.Min(c.Rt, rt));
                cycles[s.ScanNumber] = (useRt, c.Sum + tic, c.HasSurvey || s.ExperimentID == 0);
            }
            else
            {
                cycles[s.ScanNumber] = (rt, tic, s.ExperimentID == 0);
            }
        }
        var points = cycles.Values.OrderBy(c => c.Rt).Select(c => new ChromatogramPoint(c.Rt, c.Sum)).ToList();
        return new Chromatogram("TIC", 0, 0, points);
    }

    /// <summary>Total ion current over the given scans.</summary>
    public static Chromatogram Tic(RawMeasurement raw, IReadOnlyList<int> indices, string label = "TIC")
    {
        var points = new List<ChromatogramPoint>(indices.Count);
        foreach (var i in indices)
        {
            var s = raw.SpectrumList[i];
            var tic = s.TotalIonCurrent > 0 ? s.TotalIonCurrent : Sum(s.Spectrum);
            points.Add(new ChromatogramPoint(RtMinutes(s), tic));
        }
        return new Chromatogram(label, 0, 0, points);
    }

    /// <summary>Base peak chromatogram over the given scans.</summary>
    public static Chromatogram Bpc(RawMeasurement raw, IReadOnlyList<int> indices, string label = "BPC")
    {
        var points = new List<ChromatogramPoint>(indices.Count);
        foreach (var i in indices)
        {
            var s = raw.SpectrumList[i];
            var bp = s.BasePeakIntensity > 0 ? s.BasePeakIntensity : Max(s.Spectrum);
            points.Add(new ChromatogramPoint(RtMinutes(s), bp));
        }
        return new Chromatogram(label, 0, 0, points);
    }

    /// <summary>Extracted ion chromatogram (sum of intensity within ±tolerance) over the given scans.</summary>
    public static Chromatogram Xic(RawMeasurement raw, IReadOnlyList<int> indices, double mz, double toleranceDa, string? label = null)
    {
        var lo = mz - toleranceDa;
        var hi = mz + toleranceDa;
        var points = new List<ChromatogramPoint>(indices.Count);
        foreach (var i in indices)
        {
            var s = raw.SpectrumList[i];
            points.Add(new ChromatogramPoint(RtMinutes(s), s.Spectrum is null ? 0 : SumRange(s.Spectrum, lo, hi)));
        }
        return new Chromatogram(label ?? $"XIC {mz:F4} ± {toleranceDa:F4}", mz, toleranceDa, points);
    }

    /// <summary>Converts a ppm tolerance to Da at the given m/z.</summary>
    public static double PpmToDa(double mz, double ppm) => mz * ppm * 1e-6;

    /// <summary>Index (into <paramref name="indices"/>) of the scan closest to <paramref name="rt"/>.</summary>
    public static int NearestScan(RawMeasurement raw, IReadOnlyList<int> indices, double rt)
    {
        if (indices.Count == 0) return -1;
        int left = 0, right = indices.Count - 1;
        while (left < right)
        {
            var mid = (left + right) >> 1;
            if (RtMinutes(raw.SpectrumList[indices[mid]]) < rt) left = mid + 1; else right = mid;
        }
        if (left > 0 && Math.Abs(RtMinutes(raw.SpectrumList[indices[left - 1]]) - rt) < Math.Abs(RtMinutes(raw.SpectrumList[indices[left]]) - rt))
        {
            left--;
        }
        return left;
    }

    /// <summary>The centroid peaks of one spectrum.</summary>
    public static MsSpectrum Spectrum(RawMeasurement raw, int spectrumIndex)
    {
        var s = raw.SpectrumList[spectrumIndex];
        var peaks = (s.Spectrum ?? Array.Empty<RawPeakElement>()).Where(p => p.Intensity > 0).Select(p => new SpectrumPeakPoint(p.Mz, p.Intensity)).ToList();
        var precursor = s.Precursor?.SelectedIonMz ?? 0;
        var label = s.MsLevel > 1 ? $"Scan {spectrumIndex} · MS{s.MsLevel} · precursor {precursor:F4} · RT {RtMinutes(s):F3}" : $"Scan {spectrumIndex} · MS1 · RT {RtMinutes(s):F3}";
        return new MsSpectrum(label, precursor, peaks);
    }

    /// <summary>
    /// Averages all scans of the channel between two retention times. Peaks closer than <paramref name="binDa"/>
    /// are merged (intensity weighted m/z); intensities are divided by the number of scans.
    /// </summary>
    public static MsSpectrum AverageSpectrum(RawMeasurement raw, IReadOnlyList<int> indices, double rt0, double rt1, double binDa = 0.01)
    {
        if (rt1 < rt0) (rt0, rt1) = (rt1, rt0);
        var all = new List<(double Mz, double Intensity)>();
        var n = 0;
        foreach (var i in indices)
        {
            var s = raw.SpectrumList[i];
            var rt = RtMinutes(s);
            if (rt < rt0 || rt > rt1 || s.Spectrum is null) continue;
            n++;
            foreach (var p in s.Spectrum)
            {
                if (p.Intensity > 0) all.Add((p.Mz, p.Intensity));
            }
        }
        var label = $"Average of {n} scan(s), RT {rt0:F3}–{rt1:F3}";
        if (n == 0)
        {
            return new MsSpectrum(label, 0, Array.Empty<SpectrumPeakPoint>());
        }
        all.Sort((a, b) => a.Mz.CompareTo(b.Mz));
        var merged = new List<SpectrumPeakPoint>();
        var k = 0;
        while (k < all.Count)
        {
            var sumI = all[k].Intensity;
            var sumMzI = all[k].Mz * all[k].Intensity;
            var j = k + 1;
            while (j < all.Count && all[j].Mz - all[j - 1].Mz <= binDa)
            {
                sumI += all[j].Intensity;
                sumMzI += all[j].Mz * all[j].Intensity;
                j++;
            }
            merged.Add(new SpectrumPeakPoint(sumMzI / sumI, sumI / n));
            k = j;
        }
        return new MsSpectrum(label, 0, merged);
    }

    private static double Sum(RawPeakElement[]? spectrum)
    {
        if (spectrum is null) return 0;
        double sum = 0;
        foreach (var p in spectrum) sum += p.Intensity;
        return sum;
    }

    private static double Max(RawPeakElement[]? spectrum)
    {
        if (spectrum is null) return 0;
        double max = 0;
        foreach (var p in spectrum) if (p.Intensity > max) max = p.Intensity;
        return max;
    }

    private static double SumRange(RawPeakElement[] spectrum, double lo, double hi)
    {
        int left = 0, right = spectrum.Length;
        while (left < right)
        {
            var mid = (left + right) >> 1;
            if (spectrum[mid].Mz < lo) left = mid + 1; else right = mid;
        }
        double sum = 0;
        for (var i = left; i < spectrum.Length && spectrum[i].Mz <= hi; i++)
        {
            sum += spectrum[i].Intensity;
        }
        return sum;
    }
}

/// <summary>Small chromatogram post-processing used by the viewers (smoothing, baseline, normalisation).</summary>
public static class ChromatogramMath
{
    /// <summary>Gaussian smoothing with sigma in scans (0 = none).</summary>
    public static IReadOnlyList<ChromatogramPoint> Smooth(IReadOnlyList<ChromatogramPoint> points, double sigmaScans)
    {
        if (sigmaScans <= 0 || points.Count < 3) return points;
        var half = (int)Math.Ceiling(sigmaScans * 3);
        var kernel = new double[2 * half + 1];
        double norm = 0;
        for (var k = -half; k <= half; k++)
        {
            kernel[k + half] = Math.Exp(-0.5 * k * k / (sigmaScans * sigmaScans));
            norm += kernel[k + half];
        }
        var result = new ChromatogramPoint[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            double acc = 0, w = 0;
            for (var k = -half; k <= half; k++)
            {
                var j = i + k;
                if (j < 0 || j >= points.Count) continue;
                acc += points[j].Intensity * kernel[k + half];
                w += kernel[k + half];
            }
            result[i] = new ChromatogramPoint(points[i].Rt, w > 0 ? acc / w : points[i].Intensity);
        }
        _ = norm;
        return result;
    }

    /// <summary>Subtracts a rolling-minimum baseline over a window in minutes (0 = none).</summary>
    public static IReadOnlyList<ChromatogramPoint> SubtractBaseline(IReadOnlyList<ChromatogramPoint> points, double windowMinutes)
    {
        if (windowMinutes <= 0 || points.Count < 3) return points;
        var half = windowMinutes / 2;
        var result = new ChromatogramPoint[points.Count];
        var lo = 0;
        var hi = 0;
        for (var i = 0; i < points.Count; i++)
        {
            var rt = points[i].Rt;
            while (lo < points.Count && points[lo].Rt < rt - half) lo++;
            while (hi < points.Count && points[hi].Rt <= rt + half) hi++;
            var min = double.MaxValue;
            for (var j = lo; j < hi; j++) if (points[j].Intensity < min) min = points[j].Intensity;
            if (min == double.MaxValue) min = 0;
            result[i] = new ChromatogramPoint(rt, Math.Max(0, points[i].Intensity - min));
        }
        return result;
    }

    /// <summary>Scales intensities so the maximum is 100.</summary>
    public static IReadOnlyList<ChromatogramPoint> Normalize(IReadOnlyList<ChromatogramPoint> points)
    {
        double max = 0;
        foreach (var p in points) if (p.Intensity > max) max = p.Intensity;
        if (max <= 0) return points;
        var f = 100.0 / max;
        return points.Select(p => new ChromatogramPoint(p.Rt, p.Intensity * f)).ToList();
    }

    /// <summary>Points between two retention times (inclusive).</summary>
    public static IReadOnlyList<ChromatogramPoint> Slice(IReadOnlyList<ChromatogramPoint> points, double rt0, double rt1)
    {
        var list = new List<ChromatogramPoint>();
        foreach (var p in points)
        {
            if (p.Rt >= rt0 && p.Rt <= rt1) list.Add(p);
        }
        return list;
    }
}
