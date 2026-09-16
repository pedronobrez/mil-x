using CompMs.Common.Components;
using CompMs.Common.Enum;
using CompMs.MsdialCore.DataObj;
using MilX.Pipeline.Results;

namespace MilX.Pipeline.Curation;

/// <summary>What a re-integration changed, for the status line.</summary>
public sealed record ReintegrationResult(int SamplesChanged, int SamplesSkipped, double HeightAverage, double FillPercent);

/// <summary>
/// Hand edits to an alignment result: re-integrating a feature over a retention window the reviewer
/// drew, and splitting one feature into two so co-eluting isomers can be quantified apart.
///
/// Both write through to the same objects MS-DIAL uses and are persisted with the container's own
/// serialiser, so the edited result opens in MS-DIAL and feeds the exporters unchanged.
/// </summary>
public static class PeakEditor
{
    /// <summary>
    /// Re-integrates the aligned peak of each given sample between <paramref name="left"/> and
    /// <paramref name="right"/> minutes, from the chromatogram the reviewer is looking at. Height is
    /// the apex inside the window, area is the trapezoid under the trace, and the baseline-corrected
    /// area subtracts the straight line joining the two edges — the same definitions MS-DIAL uses,
    /// so a re-integrated peak stays comparable with the ones the run produced.
    /// </summary>
    public static ReintegrationResult Reintegrate(
        AlignmentSpotProperty spot,
        IReadOnlyDictionary<int, IReadOnlyList<ChromatogramPoint>> chromatograms,
        double left,
        double right,
        IReadOnlyCollection<int>? fileIds = null)
    {
        ArgumentNullException.ThrowIfNull(spot);
        ArgumentNullException.ThrowIfNull(chromatograms);
        if (right <= left) throw new ArgumentException("The integration window is empty.", nameof(right));

        var peaks = spot.AlignedPeakProperties ?? new List<AlignmentChromPeakFeature>();
        int changed = 0, skipped = 0;
        foreach (var peak in peaks)
        {
            if (fileIds is not null && !fileIds.Contains(peak.FileID)) continue;
            if (!chromatograms.TryGetValue(peak.FileID, out var trace) || trace.Count == 0) { skipped++; continue; }
            if (Integrate(peak, trace, left, right)) changed++; else skipped++;
        }
        if (changed > 0)
        {
            RecomputeSpot(spot);
            spot.IsManuallyModifiedForQuant = true;
        }
        return new ReintegrationResult(changed, skipped, spot.HeightAverage, spot.FillParcentage * 100.0);
    }

    private static bool Integrate(AlignmentChromPeakFeature peak, IReadOnlyList<ChromatogramPoint> trace, double left, double right)
    {
        var window = new List<ChromatogramPoint>();
        foreach (var p in trace)
        {
            if (p.Rt < left) continue;
            if (p.Rt > right) break;
            window.Add(p);
        }
        if (window.Count < 2) return false;

        var apex = window[0];
        foreach (var p in window)
        {
            if (p.Intensity > apex.Intensity) apex = p;
        }

        double areaAboveZero = 0;
        for (var i = 1; i < window.Count; i++)
        {
            // minutes to seconds, which is the unit MS-DIAL's own areas are in
            areaAboveZero += (window[i].Intensity + window[i - 1].Intensity) / 2.0 * (window[i].Rt - window[i - 1].Rt) * 60.0;
        }
        var baseline = (window[0].Intensity + window[^1].Intensity) / 2.0 * (window[^1].Rt - window[0].Rt) * 60.0;

        var type = peak.ChromXsTop?.MainType ?? ChromXType.RT;
        var unit = peak.ChromXsTop?.RT.Unit ?? ChromXUnit.Min;
        peak.ChromXsLeft = new ChromXs(window[0].Rt, type, unit);
        peak.ChromXsTop = new ChromXs(apex.Rt, type, unit);
        peak.ChromXsRight = new ChromXs(window[^1].Rt, type, unit);
        peak.PeakHeightLeft = window[0].Intensity;
        peak.PeakHeightTop = apex.Intensity;
        peak.PeakHeightRight = window[^1].Intensity;
        peak.PeakAreaAboveZero = areaAboveZero;
        peak.PeakAreaAboveBaseline = Math.Max(0, areaAboveZero - baseline);
        return true;
    }

    /// <summary>Brings the feature's own numbers back in line with its per-sample peaks.</summary>
    public static void RecomputeSpot(AlignmentSpotProperty spot)
    {
        var peaks = spot.AlignedPeakProperties ?? new List<AlignmentChromPeakFeature>();
        if (peaks.Count == 0) return;
        var detected = peaks.Where(p => p.PeakHeightTop > 0).ToList();
        spot.HeightAverage = detected.Count == 0 ? 0 : (float)detected.Average(p => p.PeakHeightTop);
        spot.FillParcentage = (float)detected.Count / peaks.Count;
        if (detected.Count > 0)
        {
            var type = spot.TimesCenter?.MainType ?? ChromXType.RT;
            var unit = spot.TimesCenter?.RT.Unit ?? ChromXUnit.Min;
            spot.TimesCenter = new ChromXs(detected.Average(p => p.ChromXsTop?.RT.Value ?? 0), type, unit);
            spot.PeakWidthAverage = (float)detected.Average(p => (p.ChromXsRight?.RT.Value ?? 0) - (p.ChromXsLeft?.RT.Value ?? 0));
        }
    }

    /// <summary>
    /// Copies a feature into a second one so two compounds under the same peak can be integrated and
    /// named apart. The copy is added right after the original and given the next master id; the two
    /// then need different integration windows, which is what the reviewer does next.
    /// </summary>
    public static AlignmentSpotProperty SplitIsomer(AlignmentResultContainer container, AlignmentSpotProperty spot)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(spot);
        // AlignmentSpotProperty.Clone copies the match container from the backing field, which stays
        // null until the property is read; reading it here forces the lazy creation so a feature that
        // never matched anything can still be split.
        _ = spot.MatchResults;
        foreach (var drift in spot.AlignmentDriftSpotFeatures ?? new List<AlignmentSpotProperty>()) _ = drift.MatchResults;
        foreach (var peak in spot.AlignedPeakProperties ?? new List<AlignmentChromPeakFeature>()) _ = peak.MatchResults;
        var spots = container.AlignmentSpotProperties;
        var masterId = spots.Count == 0 ? 0 : spots.Max(s => s.MasterAlignmentID) + 1;
        var alignmentId = spots.Count == 0 ? 0 : spots.Max(s => s.AlignmentID) + 1;
        var clone = spot.Clone(ref masterId, alignmentId);
        clone.Comment = string.IsNullOrEmpty(spot.Comment) ? $"split from #{spot.MasterAlignmentID}" : $"{spot.Comment}; split from #{spot.MasterAlignmentID}";
        clone.IsManuallyModifiedForQuant = true;
        var index = spots.IndexOf(spot);
        if (index < 0) spots.Add(clone); else spots.Insert(index + 1, clone);
        container.TotalAlignmentSpotCount = spots.Count;
        return clone;
    }

    /// <summary>
    /// Writes the container back, keeping one copy of the files as they were before the first edit
    /// of a session. The container's own serialiser rewrites three binary files at once, so a failure
    /// half way through would otherwise take the result with it.
    /// </summary>
    public static void Save(AlignmentResultContainer container, AlignmentFileBean file)
    {
        ArgumentNullException.ThrowIfNull(container);
        ArgumentNullException.ThrowIfNull(file);
        Backup(file.FilePath);
        container.Save(file);
    }

    private static void Backup(string alignmentPath)
    {
        var dir = Path.GetDirectoryName(alignmentPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(alignmentPath);
        var extension = Path.GetExtension(alignmentPath);
        if (extension.EndsWith("2", StringComparison.Ordinal)) extension = extension[..^1];
        // the container itself is written as "<name>.arf2" while the bean names it "<name>.arf";
        // the sibling files keep the bean's own extension
        foreach (var name in new[] { stem + extension, stem + extension + "2", stem + "_PeakProperties" + extension, stem + "_DriftSopts" + extension })
        {
            var source = Path.Combine(dir, name);
            if (!File.Exists(source)) continue;
            var target = source + ".before-curation";
            if (File.Exists(target)) continue;   // the first edit of the session is the one worth keeping
            try
            {
                File.Copy(source, target);
            }
            catch (Exception)
            {
                // a missing backup must not stop the reviewer from saving their work
            }
        }
    }
}
