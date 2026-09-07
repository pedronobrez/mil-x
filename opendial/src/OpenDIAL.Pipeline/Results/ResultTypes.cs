using CompMs.Common.DataObj.Result;
using CompMs.MsdialCore.DataObj;

namespace OpenDIAL.Pipeline.Results;

public readonly record struct SpectrumPeakPoint(double Mz, double Intensity);

public readonly record struct ChromatogramPoint(double Rt, double Intensity);

/// <summary>A centroid spectrum (deconvoluted MS/MS, raw MS/MS or a library reference).</summary>
public sealed class MsSpectrum
{
    public MsSpectrum(string label, double precursorMz, IReadOnlyList<SpectrumPeakPoint> peaks)
    {
        Label = label;
        PrecursorMz = precursorMz;
        Peaks = peaks;
    }

    public string Label { get; }
    public double PrecursorMz { get; }
    public IReadOnlyList<SpectrumPeakPoint> Peaks { get; }
    public double MaxIntensity => Peaks.Count == 0 ? 0 : Peaks.Max(p => p.Intensity);
    public static MsSpectrum Empty { get; } = new("(empty)", 0, Array.Empty<SpectrumPeakPoint>());
}

/// <summary>An extracted (or total) ion chromatogram.</summary>
public sealed class Chromatogram
{
    public Chromatogram(string label, double mz, double tolerance, IReadOnlyList<ChromatogramPoint> points)
    {
        Label = label;
        Mz = mz;
        Tolerance = tolerance;
        Points = points;
    }

    public string Label { get; }
    public double Mz { get; }
    public double Tolerance { get; }
    public IReadOnlyList<ChromatogramPoint> Points { get; }
}

/// <summary>One detected peak of one file, flattened for table display.</summary>
public sealed class PeakFeatureRow
{
    public PeakFeatureRow(ChromatogramPeakFeature feature)
    {
        Feature = feature;
    }

    public ChromatogramPeakFeature Feature { get; }
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public double Rt { get; init; }
    public double RtLeft { get; init; }
    public double RtRight { get; init; }
    public double Mz { get; init; }
    public double Height { get; init; }
    public double Area { get; init; }
    public string Adduct { get; init; } = string.Empty;
    /// <summary>"M", "M+1", ... or empty when unknown.</summary>
    public string Isotope { get; init; } = string.Empty;
    public double Score { get; init; }
    public double SignalToNoise { get; init; }
    public bool HasMs2 { get; init; }
    public bool IsAnnotated => !string.IsNullOrEmpty(Name) && !Name.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase) && !Name.StartsWith("w/o", StringComparison.OrdinalIgnoreCase);
    public MsScanMatchResult? MatchResult { get; init; }

}

/// <summary>
/// One library match of an aligned feature. MS-DIAL keeps several per feature and reports the best;
/// the others are what a reviewer picks from when the automatic choice is wrong.
/// </summary>
public sealed record AnnotationCandidate(
    string Name,
    double TotalScore,
    double SimpleDotProduct,
    double WeightedDotProduct,
    double ReverseDotProduct,
    double MatchedPeaksCount,
    double MatchedPeaksPercentage,
    double MassSimilarity,
    double RtSimilarity,
    bool IsRepresentative,
    bool IsSpectrumMatch,
    bool IsLipidClassMatch,
    bool IsLipidChainsMatch,
    bool IsLipidPositionMatch,
    string Source,
    int LibraryId);

/// <summary>Mean height of one sample class, for the inline chart of the ion table.</summary>
public readonly record struct ClassHeight(string Class, double Mean);

public sealed record SampleInfo(int FileId, string FileName, string Class, string SampleType);

public readonly record struct SampleValue(int FileId, string FileName, string Class, double Height);

/// <summary>The peak of one sample inside an alignment spot (NaN fields when only heights are known).</summary>
public sealed record AlignedSamplePeak(int FileId, string FileName, string Class, string SampleType, double Rt, double RtLeft, double RtRight, double Mz,
    double Height, double Area, double SignalToNoise, bool IsGapFilled)
{
    public bool HasPeak => Height > 0 && !double.IsNaN(Rt);
}

/// <summary>One alignment spot with its per-sample intensities.</summary>
public sealed class AlignmentSpotRow
{
    public AlignmentSpotProperty? Spot { get; init; }
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public double Rt { get; init; }
    public double Mz { get; init; }
    public double AverageHeight { get; init; }
    public double FillPercent { get; init; }
    public double Score { get; init; }
    public string Adduct { get; init; } = string.Empty;
    public IReadOnlyList<SampleValue> SampleHeights { get; init; } = Array.Empty<SampleValue>();
    /// <summary>Per-sample peak details (RT, integration range, height, area, S/N, gap-filled) in file order.</summary>
    public IReadOnlyList<AlignedSamplePeak> SamplePeaks { get; init; } = Array.Empty<AlignedSamplePeak>();
    public string Ontology { get; init; } = string.Empty;
    public string Formula { get; init; } = string.Empty;
    public string InChIKey { get; init; } = string.Empty;
    public double SignalToNoiseAverage { get; init; }
    public bool IsAnnotated => !string.IsNullOrEmpty(Name) && !Name.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase) && !Name.StartsWith("w/o", StringComparison.OrdinalIgnoreCase);
    public MsScanMatchResult? MatchResult { get; init; }

    /// <summary>True when at least one aligned peak carried a product spectrum.</summary>
    public bool MsmsAssigned { get; init; }
    /// <summary>Sample the aligned feature takes its spectrum and annotation from.</summary>
    public int RepresentativeFileId { get; init; }
    /// <summary>MS1 isotope pattern of the representative sample, monoisotopic peak first.</summary>
    public IReadOnlyList<SpectrumPeakPoint> IsotopicPeaks { get; init; } = Array.Empty<SpectrumPeakPoint>();
    /// <summary>Comment MS-DIAL stored with the feature.</summary>
    public string Comment { get; init; } = string.Empty;
    /// <summary>Every library match kept for this feature, best first: the alternatives to the reported name.</summary>
    public IReadOnlyList<AnnotationCandidate> Candidates { get; init; } = Array.Empty<AnnotationCandidate>();
    public bool IsManuallyAnnotated { get; init; }

    /// <summary>Proportion of the monoisotopic ion in the MS1 isotope cluster, as MS-DIAL reports it.</summary>
    public double MonoisotopicPercentage { get; init; }

    /// <summary>0 for the monoisotopic ion, 1 for M+1 and so on; -1 when the run did not decide.</summary>
    public int IsotopeWeight { get; init; } = -1;
    /// <summary>The reviewer re-integrated this feature by hand.</summary>
    public bool IsManuallyQuantified { get; init; }
    /// <summary>Mean height per sample class, for the ion table's inline chart.</summary>
    public IReadOnlyList<ClassHeight> ClassHeights { get; init; } = Array.Empty<ClassHeight>();
}

public sealed class AlignmentTable
{
    public AlignmentTable(IReadOnlyList<SampleInfo> samples, IReadOnlyList<AlignmentSpotRow> spots, string source, AlignmentResultContainer? container = null)
    {
        Samples = samples;
        Spots = spots;
        Source = source;
        Container = container;
    }

    /// <summary>The loaded alignment result, when it came from the binary files: what a hand edit changes and saves.</summary>
    public AlignmentResultContainer? Container { get; }

    public IReadOnlyList<SampleInfo> Samples { get; }
    public IReadOnlyList<AlignmentSpotRow> Spots { get; }
    /// <summary>"container" when loaded from the binary .arf2 files, "tsv" when parsed from the .mdalign export.</summary>
    public string Source { get; }
}
