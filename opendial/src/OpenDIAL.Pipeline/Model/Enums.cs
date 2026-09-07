namespace OpenDIAL.Pipeline.Model;

/// <summary>Which MS-DIAL processing branch to run.</summary>
public enum IonizationMode
{
    LCMS,
    GCMS,
}

/// <summary>Role of an input file in the experiment (maps to upstream <c>AnalysisFileType</c>).</summary>
public enum SampleType
{
    Sample,
    Blank,
    QC,
    Standard,
}

/// <summary>MS/MS acquisition scheme of a file (maps to upstream <c>AcquisitionType</c>).</summary>
public enum AcquisitionMode
{
    DDA,
    SWATH,
    AIF,
}

public enum IonPolarity
{
    Positive,
    Negative,
}

public enum SpectrumDataType
{
    Centroid,
    Profile,
}

public enum OmicsTarget
{
    Metabolomics,
    Lipidomics,
}

/// <summary>Chromatogram smoothing methods; names match upstream <c>CompMs.Common.Enum.SmoothingMethod</c>.</summary>
public enum SmoothingKind
{
    SimpleMovingAverage,
    LinearWeightedMovingAverage,
    SavitzkyGolayFilter,
    BinomialFilter,
    LowessFilter,
    LoessFilter,
}

public enum RetentionKind
{
    RT,
    RI,
}

public enum RiCompoundKind
{
    Alkanes,
    Fames,
}
