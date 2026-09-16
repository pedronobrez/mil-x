using System.Globalization;
using System.Text;
using MilX.Pipeline.Model;

namespace MilX.Pipeline.Parameters;

/// <summary>
/// Strongly typed subset of the MS-DIAL method parameters, editable from the GUI.
/// Serialises to/from the console "Key: value" method-file format understood by the
/// upstream console app (tests/MSDIAL5/MsdialCoreTestApp/Parser/ConfigParser.cs) and by
/// <see cref="MethodFileParser"/>. Keys that this class does not model are preserved in
/// <see cref="AdditionalLines"/> so a round trip does not lose information.
/// </summary>
public sealed class MethodParameters
{
    // ---- Data type ----
    public SpectrumDataType Ms1DataType { get; set; } = SpectrumDataType.Centroid;
    public SpectrumDataType Ms2DataType { get; set; } = SpectrumDataType.Centroid;
    public IonPolarity IonMode { get; set; } = IonPolarity.Positive;
    public OmicsTarget TargetOmics { get; set; } = OmicsTarget.Metabolomics;
    public AcquisitionMode AcquisitionType { get; set; } = AcquisitionMode.DDA;

    // ---- Data collection ----
    public float RetentionTimeBegin { get; set; } = 0f;
    public float RetentionTimeEnd { get; set; } = 100f;
    public float Ms1MassRangeBegin { get; set; } = 50f;
    public float Ms1MassRangeEnd { get; set; } = 1500f;
    public float Ms2MassRangeBegin { get; set; } = 50f;
    public float Ms2MassRangeEnd { get; set; } = 1500f;

    // ---- Centroid ----
    public float Ms1CentroidTolerance { get; set; } = 0.01f;
    public float Ms2CentroidTolerance { get; set; } = 0.025f;

    // ---- Peak detection ----
    public SmoothingKind SmoothingMethod { get; set; } = SmoothingKind.LinearWeightedMovingAverage;
    public int SmoothingLevel { get; set; } = 3;
    public int MinimumPeakWidth { get; set; } = 5;
    public int MinimumPeakHeight { get; set; } = 1000;
    public float MassSliceWidth { get; set; } = 0.1f;
    public int MaxChargeNumber { get; set; } = 2;
    /// <summary>Comma separated adduct list, e.g. "[M+H]+,[M+Na]+,[M+NH4]+".</summary>
    public string SearchedAdductIons { get; set; } = "[M+H]+,[M+Na]+,[M+NH4]+";

    // ---- Deconvolution ----
    public float SigmaWindowValue { get; set; } = 0.5f;
    public float AmplitudeCutoff { get; set; } = 0f;
    public float KeepIsotopeRange { get; set; } = 0.5f;
    public bool ExcludeAfterPrecursor { get; set; } = true;

    // ---- MS/MS identification (MSP) ----
    public string MspFilePath { get; set; } = string.Empty;
    public string TextDbFilePath { get; set; } = string.Empty;
    public float RtToleranceForAnnotation { get; set; } = 0.5f;
    public float Ms1ToleranceForAnnotation { get; set; } = 0.01f;
    public float Ms2ToleranceForAnnotation { get; set; } = 0.05f;
    public float AnnotationMassRangeBegin { get; set; } = 0f;
    public float AnnotationMassRangeEnd { get; set; } = 2000f;
    public float RelativeAmplitudeCutoffForAnnotation { get; set; } = 0f;
    public float AbsoluteAmplitudeCutoffForAnnotation { get; set; } = 0f;
    public float WeightedDotProductCutoff { get; set; } = 0.4f;
    public float SimpleDotProductCutoff { get; set; } = 0.4f;
    public float ReverseDotProductCutoff { get; set; } = 0.4f;
    public float MatchedPeaksPercentageCutoff { get; set; } = 0.2f;
    public float MinimumSpectrumMatch { get; set; } = 1f;
    public float TotalScoreCutoff { get; set; } = 60f;
    /// <summary>
    /// Off by default. Almost every public library — MassBank, MoNA, GNPS, the lipid in-silico ones —
    /// carries retention times measured on the gradient it was built for, and scoring against them
    /// zeroes the retention term of every candidate on any other method, dragging correct spectral
    /// matches under the total-score cut-off. On the liver batch that alone was the difference
    /// between 286 names and 1198. Turn it on when the library was measured on the method being run.
    /// </summary>
    public bool UseRetentionInformationForScoring { get; set; } = false;
    public bool UseRetentionInformationForFiltering { get; set; } = false;
    public bool OnlyReportTopHit { get; set; } = true;

    // ---- Alignment ----
    public int AlignmentReferenceFileId { get; set; } = 0;
    public float RtToleranceForAlignment { get; set; } = 0.1f;
    public float Ms1ToleranceForAlignment { get; set; } = 0.015f;
    public float RtFactorForAlignment { get; set; } = 0.5f;
    public float Ms1FactorForAlignment { get; set; } = 0.5f;
    public float PeakCountFilter { get; set; } = 0f;
    public float NPercentDetectedInOneGroup { get; set; } = 0f;
    public bool GapFillingByCompulsion { get; set; } = true;
    public bool TogetherWithAlignment { get; set; } = true;

    // ---- Export ----
    public bool IsHeightMatrixExport { get; set; } = true;

    // ---- Process ----
    public int NumberOfThreads { get; set; } = Math.Max(2, Environment.ProcessorCount);

    // ---- GC-MS specific ----
    public RetentionKind RetentionType { get; set; } = RetentionKind.RT;
    public RiCompoundKind RiCompoundType { get; set; } = RiCompoundKind.Alkanes;
    public RetentionKind AlignmentIndexType { get; set; } = RetentionKind.RT;
    /// <summary>Path of the tab separated "raw file path \t RI dictionary path" table (GC-MS with RI only).</summary>
    public string RiIndexFilePaths { get; set; } = string.Empty;

    /// <summary>Key/value lines that were present in a loaded method file but are not modelled by this class.</summary>
    public Dictionary<string, string> AdditionalLines { get; } = new(StringComparer.OrdinalIgnoreCase);

    public MethodParameters Clone()
    {
        var clone = FromMethodFileText(ToMethodFileText());
        return clone;
    }

    // ------------------------------------------------------------------
    // Serialisation to the console method-file format
    // ------------------------------------------------------------------

    private static string F(float value) => value.ToString("0.0#####", CultureInfo.InvariantCulture);

    public string ToMethodFileText(IonizationMode mode = IonizationMode.LCMS)
    {
        var sb = new StringBuilder();
        void Line(string key, string value) => sb.Append(key).Append(": ").Append(value).Append('\n');

        sb.Append("#Data type\n");
        Line("MS1 data type", Ms1DataType.ToString());
        Line("MS2 data type", Ms2DataType.ToString());
        Line("Ion mode", IonMode.ToString());
        Line("Target omics", TargetOmics.ToString());
        Line("Acquisition type", AcquisitionType.ToString());
        Line("Machine category", mode == IonizationMode.GCMS ? "GCMS" : "LCMS");
        sb.Append('\n');

        sb.Append("#Data collection parameters\n");
        Line("Retention time begin", F(RetentionTimeBegin));
        Line("Retention time end", F(RetentionTimeEnd));
        Line("MS1 mass range begin", F(Ms1MassRangeBegin));
        Line("MS1 mass range end", F(Ms1MassRangeEnd));
        Line("MS2 mass range begin", F(Ms2MassRangeBegin));
        Line("MS2 mass range end", F(Ms2MassRangeEnd));
        sb.Append('\n');

        sb.Append("#Centroid parameters\n");
        Line("MS1 tolerance for centroid", F(Ms1CentroidTolerance));
        Line("MS2 tolerance for centroid", F(Ms2CentroidTolerance));
        sb.Append('\n');

        sb.Append("#Peak detection parameters\n");
        Line("Smoothing method", SmoothingMethod.ToString());
        Line("Smoothing level", SmoothingLevel.ToString(CultureInfo.InvariantCulture));
        Line("Minimum peak width", MinimumPeakWidth.ToString(CultureInfo.InvariantCulture));
        Line("Minimum peak height", MinimumPeakHeight.ToString(CultureInfo.InvariantCulture));
        Line("Mass slice width", F(MassSliceWidth));
        Line("Mass accuracy", F(Ms1CentroidTolerance));
        Line("Max charge number", MaxChargeNumber.ToString(CultureInfo.InvariantCulture));
        sb.Append('\n');

        sb.Append("#Deconvolution parameters\n");
        Line("Sigma window value", F(SigmaWindowValue));
        Line("Amplitude cut off", F(AmplitudeCutoff));
        Line("Keep isotope range", F(KeepIsotopeRange));
        Line("Exclude after precursor", ExcludeAfterPrecursor.ToString());
        sb.Append('\n');

        sb.Append("#Adduct list\n");
        Line("Searched adduct ions", SearchedAdductIons);
        sb.Append('\n');

        sb.Append("#MSP file and MS/MS identification setting\n");
        Line("MSP file path", MspFilePath);
        if (!string.IsNullOrWhiteSpace(TextDbFilePath))
        {
            Line("Text DB file path", TextDbFilePath);
        }
        Line("RT tolerance for MSP-based annotation", F(RtToleranceForAnnotation));
        Line("Mass range begin for MSP-based annotation", F(AnnotationMassRangeBegin));
        Line("Mass range end for MSP-based annotation", F(AnnotationMassRangeEnd));
        Line("Relative amplitude cutoff for MSP-based annotation", F(RelativeAmplitudeCutoffForAnnotation));
        Line("Absolute amplitude cutoff for MSP-based annotation", F(AbsoluteAmplitudeCutoffForAnnotation));
        Line("Weighted dot product cutoff for MSP-based annotation", F(WeightedDotProductCutoff));
        Line("Simple dot product cutoff for MSP-based annotation", F(SimpleDotProductCutoff));
        Line("Reverse dot product cutoff for MSP-based annotation", F(ReverseDotProductCutoff));
        Line("Matched peaks percentage cutoff for MSP-based annotation", F(MatchedPeaksPercentageCutoff));
        Line("Minimum spectrum match for MSP-based annotation", F(MinimumSpectrumMatch));
        Line("Total score cutoff for MSP-based annotation", F(TotalScoreCutoff));
        Line("MS1 tolerance for MSP-based annotation", F(Ms1ToleranceForAnnotation));
        Line("MS2 tolerance for MSP-based annotation", F(Ms2ToleranceForAnnotation));
        Line("Use retention information for MSP-based annotation scoring", UseRetentionInformationForScoring.ToString());
        Line("Use retention information for MSP-based annotation filtering", UseRetentionInformationForFiltering.ToString());
        Line("Only report top hit for MSP-based annotation", OnlyReportTopHit.ToString());
        sb.Append('\n');

        if (mode == IonizationMode.GCMS)
        {
            sb.Append("#GC-MS specific\n");
            Line("Retention type", RetentionType.ToString());
            Line("RI compound type", RiCompoundType.ToString());
            Line("Alignment index type", AlignmentIndexType.ToString());
            if (!string.IsNullOrWhiteSpace(RiIndexFilePaths))
            {
                Line("RI index file pathes", RiIndexFilePaths);
            }
            sb.Append('\n');
        }

        sb.Append("#Alignment parameters setting\n");
        Line("Alignment reference file ID", AlignmentReferenceFileId.ToString(CultureInfo.InvariantCulture));
        Line("Retention time tolerance for alignment", F(RtToleranceForAlignment));
        Line("MS1 tolerance for alignment", F(Ms1ToleranceForAlignment));
        Line("Retention time factor for alignment", F(RtFactorForAlignment));
        Line("MS1 factor for alignment", F(Ms1FactorForAlignment));
        Line("Peak count filter", F(PeakCountFilter));
        Line("N percent detected in one group", F(NPercentDetectedInOneGroup));
        Line("Gap filling by compulsion", GapFillingByCompulsion.ToString());
        Line("Together with alignment", TogetherWithAlignment.ToString());
        sb.Append('\n');

        sb.Append("#Export\n");
        Line("Is height matrix export", IsHeightMatrixExport.ToString());
        sb.Append('\n');

        sb.Append("#Process\n");
        Line("Number of threads", NumberOfThreads.ToString(CultureInfo.InvariantCulture));

        if (AdditionalLines.Count > 0)
        {
            sb.Append("\n#Other\n");
            foreach (var kv in AdditionalLines)
            {
                Line(kv.Key, kv.Value);
            }
        }
        return sb.ToString();
    }

    // ------------------------------------------------------------------
    // Parsing
    // ------------------------------------------------------------------

    public static MethodParameters FromMethodFileText(string text)
    {
        var p = new MethodParameters();
        foreach (var rawLine in text.Split('\n'))
        {
            if (!MethodFileParser.TryReadFieldValues(rawLine, out var key, out var value))
            {
                continue;
            }
            if (!p.TryApply(key, value))
            {
                p.AdditionalLines[key] = value;
            }
        }
        return p;
    }

    public static async Task<MethodParameters> LoadAsync(string path, CancellationToken ct = default)
    {
        var text = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        return FromMethodFileText(text);
    }

    public Task SaveAsync(string path, IonizationMode mode = IonizationMode.LCMS, CancellationToken ct = default)
        => File.WriteAllTextAsync(path, ToMethodFileText(mode), ct);

    private static bool TryFloat(string v, out float f) => float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out f);
    private static bool TryInt(string v, out int i) => int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out i);
    private static bool TryBool(string v, out bool b) => bool.TryParse(v, out b);
    private static bool TryEnum<T>(string v, out T e) where T : struct => System.Enum.TryParse(v, true, out e);

    /// <summary>Applies one key/value pair. Returns false if the key is not modelled by this class.</summary>
    private bool TryApply(string key, string value)
    {
        switch (key.ToLowerInvariant())
        {
            case "ms1 data type": if (TryEnum<SpectrumDataType>(value, out var dt1)) Ms1DataType = dt1; return true;
            case "ms2 data type": if (TryEnum<SpectrumDataType>(value, out var dt2)) Ms2DataType = dt2; return true;
            case "ion mode": if (TryEnum<IonPolarity>(value, out var im)) IonMode = im; return true;
            case "target omics": if (TryEnum<OmicsTarget>(value, out var to)) TargetOmics = to; return true;
            case "acquisition type": if (TryEnum<AcquisitionMode>(value, out var at)) AcquisitionType = at; return true;
            case "machine category": return true; // derived from IonizationMode

            case "retention time begin": if (TryFloat(value, out var f1)) RetentionTimeBegin = f1; return true;
            case "retention time end": if (TryFloat(value, out var f2)) RetentionTimeEnd = f2; return true;
            case "ms1 mass range begin": if (TryFloat(value, out var f3)) Ms1MassRangeBegin = f3; return true;
            case "ms1 mass range end": if (TryFloat(value, out var f4)) Ms1MassRangeEnd = f4; return true;
            case "ms2 mass range begin": if (TryFloat(value, out var f5)) Ms2MassRangeBegin = f5; return true;
            case "ms2 mass range end": if (TryFloat(value, out var f6)) Ms2MassRangeEnd = f6; return true;

            case "ms1 tolerance for centroid": if (TryFloat(value, out var f7)) Ms1CentroidTolerance = f7; return true;
            case "ms2 tolerance for centroid": if (TryFloat(value, out var f8)) Ms2CentroidTolerance = f8; return true;
            case "mass accuracy": if (TryFloat(value, out var f9)) Ms1CentroidTolerance = f9; return true;

            case "smoothing method": if (TryEnum<SmoothingKind>(value, out var sm)) SmoothingMethod = sm; return true;
            case "smoothing level": if (TryInt(value, out var i1)) SmoothingLevel = i1; return true;
            case "minimum peak width": if (TryInt(value, out var i2)) MinimumPeakWidth = i2; return true;
            case "minimum peak height": if (TryInt(value, out var i3)) MinimumPeakHeight = i3; return true;
            case "mass slice width": if (TryFloat(value, out var f10)) MassSliceWidth = f10; return true;
            case "max charge number": if (TryInt(value, out var i4)) MaxChargeNumber = i4; return true;
            case "searched adduct ions": SearchedAdductIons = value; return true;

            case "sigma window value": if (TryFloat(value, out var f11)) SigmaWindowValue = f11; return true;
            case "amplitude cut off": if (TryFloat(value, out var f12)) AmplitudeCutoff = f12; return true;
            case "keep isotope range": if (TryFloat(value, out var f13)) KeepIsotopeRange = f13; return true;
            case "exclude after precursor": if (TryBool(value, out var b1)) ExcludeAfterPrecursor = b1; return true;

            case "msp file path": MspFilePath = value; return true;
            case "text db file path": TextDbFilePath = value; return true;
            case "rt tolerance for msp-based annotation": if (TryFloat(value, out var f14)) RtToleranceForAnnotation = f14; return true;
            case "mass range begin for msp-based annotation": if (TryFloat(value, out var f15)) AnnotationMassRangeBegin = f15; return true;
            case "mass range end for msp-based annotation": if (TryFloat(value, out var f16)) AnnotationMassRangeEnd = f16; return true;
            case "relative amplitude cutoff for msp-based annotation": if (TryFloat(value, out var f17)) RelativeAmplitudeCutoffForAnnotation = f17; return true;
            case "absolute amplitude cutoff for msp-based annotation": if (TryFloat(value, out var f18)) AbsoluteAmplitudeCutoffForAnnotation = f18; return true;
            case "weighted dot product cutoff for msp-based annotation": if (TryFloat(value, out var f19)) WeightedDotProductCutoff = f19; return true;
            case "simple dot product cutoff for msp-based annotation": if (TryFloat(value, out var f20)) SimpleDotProductCutoff = f20; return true;
            case "reverse dot product cutoff for msp-based annotation": if (TryFloat(value, out var f21)) ReverseDotProductCutoff = f21; return true;
            case "matched peaks percentage cutoff for msp-based annotation": if (TryFloat(value, out var f22)) MatchedPeaksPercentageCutoff = f22; return true;
            case "minimum spectrum match for msp-based annotation": if (TryFloat(value, out var f23)) MinimumSpectrumMatch = f23; return true;
            case "total score cutoff for msp-based annotation": if (TryFloat(value, out var f24)) TotalScoreCutoff = f24; return true;
            case "ms1 tolerance for msp-based annotation": if (TryFloat(value, out var f25)) Ms1ToleranceForAnnotation = f25; return true;
            case "ms2 tolerance for msp-based annotation": if (TryFloat(value, out var f26)) Ms2ToleranceForAnnotation = f26; return true;
            case "use retention information for msp-based annotation scoring": if (TryBool(value, out var b2)) UseRetentionInformationForScoring = b2; return true;
            case "use retention information for msp-based annotation filtering": if (TryBool(value, out var b3)) UseRetentionInformationForFiltering = b3; return true;
            case "only report top hit for msp-based annotation": if (TryBool(value, out var b4)) OnlyReportTopHit = b4; return true;

            case "retention type": if (TryEnum<RetentionKind>(value, out var rk)) RetentionType = rk; return true;
            case "ri compound":
            case "ri compound type": if (TryEnum<RiCompoundKind>(value, out var rc)) RiCompoundType = rc; return true;
            case "alignment index type": if (TryEnum<RetentionKind>(value, out var ai)) AlignmentIndexType = ai; return true;
            case "ri index file pathes": RiIndexFilePaths = value; return true;

            case "alignment reference file id": if (TryInt(value, out var i5)) AlignmentReferenceFileId = i5; return true;
            case "retention time tolerance for alignment": if (TryFloat(value, out var f27)) RtToleranceForAlignment = f27; return true;
            case "ms1 tolerance for alignment": if (TryFloat(value, out var f28)) Ms1ToleranceForAlignment = f28; return true;
            case "retention time factor for alignment": if (TryFloat(value, out var f29)) RtFactorForAlignment = f29; return true;
            case "ms1 factor for alignment": if (TryFloat(value, out var f30)) Ms1FactorForAlignment = f30; return true;
            case "peak count filter": if (TryFloat(value, out var f31)) PeakCountFilter = f31; return true;
            case "n percent detected in one group": if (TryFloat(value, out var f32)) NPercentDetectedInOneGroup = f32; return true;
            case "gap filling by compulsion":
            case "force insert peaks in gap filling": if (TryBool(value, out var b5)) GapFillingByCompulsion = b5; return true;
            case "together with alignment": if (TryBool(value, out var b6)) TogetherWithAlignment = b6; return true;

            case "is height matrix export": if (TryBool(value, out var b7)) IsHeightMatrixExport = b7; return true;
            case "number of threads": if (TryInt(value, out var i6) && i6 > 0) NumberOfThreads = i6; return true;
            default:
                return false;
        }
    }
}
