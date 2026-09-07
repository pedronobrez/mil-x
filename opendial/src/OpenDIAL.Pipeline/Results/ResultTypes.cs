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
}

public sealed class AlignmentTable
{
    public AlignmentTable(IReadOnlyList<SampleInfo> samples, IReadOnlyList<AlignmentSpotRow> spots, string source)
    {
        Samples = samples;
        Spots = spots;
        Source = source;
    }

    public IReadOnlyList<SampleInfo> Samples { get; }
    public IReadOnlyList<AlignmentSpotRow> Spots { get; }
    /// <summary>"container" when loaded from the binary .arf2 files, "tsv" when parsed from the .mdalign export.</summary>
    public string Source { get; }
}
