namespace OpenDIAL.Pipeline.Curation;

/// <summary>
/// The review flags MS-DIAL puts on a peak spot. The numeric values are MS-DIAL's own tag ids
/// (CompMs.MsdialCore.DataObj.PeakSpotTag), so a curation written here is the one MS-DIAL reads
/// back out of &lt;alignment&gt;_tags.xml, and vice versa.
/// </summary>
public enum PeakSpotTagKind
{
    /// <summary>The annotation was checked and is right.</summary>
    Confirmed = 1,
    /// <summary>The MS/MS is too weak or too noisy to decide on.</summary>
    LowQualitySpectrum = 2,
    /// <summary>The annotation is wrong.</summary>
    Misannotation = 3,
    /// <summary>Two or more compounds co-elute, so the spectrum is mixed.</summary>
    Coelution = 4,
    /// <summary>The same compound is reported more than once (isotope, adduct or in-source fragment).</summary>
    Overannotation = 5,
}

public static class PeakSpotTagKindExtensions
{
    public static string Label(this PeakSpotTagKind kind) => kind switch {
        PeakSpotTagKind.Confirmed => "Confirmed",
        PeakSpotTagKind.LowQualitySpectrum => "Low quality spectrum",
        PeakSpotTagKind.Misannotation => "Misannotation",
        PeakSpotTagKind.Coelution => "Coelution (mixed spectra)",
        PeakSpotTagKind.Overannotation => "Overannotation",
        _ => kind.ToString(),
    };

    /// <summary>Short form for the ion table's tag column.</summary>
    public static string ShortLabel(this PeakSpotTagKind kind) => kind switch {
        PeakSpotTagKind.Confirmed => "Confirmed",
        PeakSpotTagKind.LowQualitySpectrum => "Low quality",
        PeakSpotTagKind.Misannotation => "Misannotation",
        PeakSpotTagKind.Coelution => "Coelution",
        PeakSpotTagKind.Overannotation => "Overannotation",
        _ => kind.ToString(),
    };

    /// <summary>Digit that toggles the tag from the keyboard while reviewing.</summary>
    public static string Shortcut(this PeakSpotTagKind kind) => ((int)kind).ToString();

    public static IReadOnlyList<PeakSpotTagKind> All { get; } = new[] {
        PeakSpotTagKind.Confirmed,
        PeakSpotTagKind.LowQualitySpectrum,
        PeakSpotTagKind.Misannotation,
        PeakSpotTagKind.Coelution,
        PeakSpotTagKind.Overannotation,
    };
}
