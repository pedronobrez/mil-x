using System.Runtime.CompilerServices;
using OpenDIAL.Pipeline.Model;
using OpenDIAL.Pipeline.Parameters;

namespace OpenDIAL.Desktop.ViewModels;

/// <summary>
/// Observable facade over <see cref="MethodParameters"/>. Every form edit regenerates
/// <see cref="MethodText"/>; editing the text re-parses it back into the form.
/// </summary>
public sealed class MethodParametersViewModel : ViewModelBase
{
    private MethodParameters _model = new();
    private bool _syncingFromText;
    private string _methodText = string.Empty;
    private IonizationMode _mode = IonizationMode.LCMS;

    public static IonPolarity[] IonPolarities { get; } = Enum.GetValues<IonPolarity>();
    public static SpectrumDataType[] DataTypes { get; } = Enum.GetValues<SpectrumDataType>();
    public static AcquisitionMode[] AcquisitionModes { get; } = Enum.GetValues<AcquisitionMode>();
    public static OmicsTarget[] OmicsTargets { get; } = Enum.GetValues<OmicsTarget>();
    public static SmoothingKind[] SmoothingKinds { get; } = Enum.GetValues<SmoothingKind>();
    public static RetentionKind[] RetentionKinds { get; } = Enum.GetValues<RetentionKind>();
    public static RiCompoundKind[] RiCompoundKinds { get; } = Enum.GetValues<RiCompoundKind>();

    public MethodParametersViewModel()
    {
        RegenerateText();
    }

    public MethodParameters Model => _model;

    /// <summary>Raised after any change of the model (form edit or text edit).</summary>
    public event EventHandler? Changed;

    /// <summary>Number of "Key: value" lines that differ from the defaults for the current mode.</summary>
    public int ChangedFromDefaults
    {
        get
        {
            var defaults = ParseLines(new MethodParameters().ToMethodFileText(_mode));
            var current = ParseLines(_model.ToMethodFileText(_mode));
            var n = 0;
            foreach (var (k, v) in current)
            {
                if (!defaults.TryGetValue(k, out var d) || !string.Equals(d, v, StringComparison.Ordinal)) n++;
            }
            return n;
        }
    }

    /// <summary>One-line summary: "LC-MS · DDA · positive · 12 parameters changed from defaults".</summary>
    public string Summary
    {
        get
        {
            var n = ChangedFromDefaults;
            var changed = n == 0 ? "defaults" : n == 1 ? "1 parameter changed from defaults" : $"{n} parameters changed from defaults";
            var lib = string.IsNullOrWhiteSpace(_model.MspFilePath) ? "no library" : Path.GetFileName(_model.MspFilePath);
            return $"{(_mode == IonizationMode.GCMS ? "GC-MS" : "LC-MS")} · {_model.AcquisitionType} · {_model.IonMode.ToString().ToLowerInvariant()} · {lib} · {changed}";
        }
    }

    private static Dictionary<string, string> ParseLines(string text)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Split('\n'))
        {
            if (MethodFileParser.TryReadFieldValues(line, out var k, out var v)) map[k] = v;
        }
        return map;
    }

    /// <summary>The ionization mode only affects which sections are emitted in the text.</summary>
    public IonizationMode Mode
    {
        get => _mode;
        set { if (_mode != value) { _mode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsGcms)); RegenerateText(); } }
    }

    public bool IsGcms => _mode == IonizationMode.GCMS;

    public void Load(MethodParameters parameters)
    {
        _model = parameters;
        RefreshAll();
        RegenerateText();
    }

    public string MethodText
    {
        get => _methodText;
        set
        {
            if (_methodText == value) return;
            _methodText = value;
            OnPropertyChanged();
            if (_syncingFromText) return;
            try
            {
                _model = MethodParameters.FromMethodFileText(value);
                TextError = string.Empty;
            }
            catch (Exception ex)
            {
                TextError = ex.Message;
            }
            RefreshAll();
        }
    }

    private string _textError = string.Empty;
    public string TextError
    {
        get => _textError;
        private set { if (_textError != value) { _textError = value; OnPropertyChanged(); } }
    }

    private void RefreshAll()
    {
        _syncingFromText = true;
        try
        {
            OnPropertyChanged(string.Empty);
        }
        finally
        {
            _syncingFromText = false;
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void RegenerateText()
    {
        _syncingFromText = true;
        try
        {
            MethodText = _model.ToMethodFileText(_mode);
        }
        finally
        {
            _syncingFromText = false;
        }
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(ChangedFromDefaults));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Replaces the model from method-file text (used when a project is opened).</summary>
    public void LoadText(string text)
    {
        try
        {
            _model = MethodParameters.FromMethodFileText(text);
            TextError = string.Empty;
        }
        catch (Exception ex)
        {
            TextError = ex.Message;
        }
        RefreshAll();
        RegenerateText();
    }

    private void Set<T>(T current, T value, Action<T> assign, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(current, value)) return;
        assign(value);
        OnPropertyChanged(name);
        if (!_syncingFromText) RegenerateText();
    }

    // ---- data type ----
    public SpectrumDataType Ms1DataType { get => _model.Ms1DataType; set => Set(_model.Ms1DataType, value, v => _model.Ms1DataType = v); }
    public SpectrumDataType Ms2DataType { get => _model.Ms2DataType; set => Set(_model.Ms2DataType, value, v => _model.Ms2DataType = v); }
    public IonPolarity IonMode { get => _model.IonMode; set => Set(_model.IonMode, value, v => _model.IonMode = v); }
    public OmicsTarget TargetOmics { get => _model.TargetOmics; set => Set(_model.TargetOmics, value, v => _model.TargetOmics = v); }
    public AcquisitionMode AcquisitionType { get => _model.AcquisitionType; set => Set(_model.AcquisitionType, value, v => _model.AcquisitionType = v); }

    // ---- data collection ----
    public double RetentionTimeBegin { get => _model.RetentionTimeBegin; set => Set(_model.RetentionTimeBegin, (float)value, v => _model.RetentionTimeBegin = v); }
    public double RetentionTimeEnd { get => _model.RetentionTimeEnd; set => Set(_model.RetentionTimeEnd, (float)value, v => _model.RetentionTimeEnd = v); }
    public double Ms1MassRangeBegin { get => _model.Ms1MassRangeBegin; set => Set(_model.Ms1MassRangeBegin, (float)value, v => _model.Ms1MassRangeBegin = v); }
    public double Ms1MassRangeEnd { get => _model.Ms1MassRangeEnd; set => Set(_model.Ms1MassRangeEnd, (float)value, v => _model.Ms1MassRangeEnd = v); }
    public double Ms2MassRangeBegin { get => _model.Ms2MassRangeBegin; set => Set(_model.Ms2MassRangeBegin, (float)value, v => _model.Ms2MassRangeBegin = v); }
    public double Ms2MassRangeEnd { get => _model.Ms2MassRangeEnd; set => Set(_model.Ms2MassRangeEnd, (float)value, v => _model.Ms2MassRangeEnd = v); }
    public double Ms1CentroidTolerance { get => _model.Ms1CentroidTolerance; set => Set(_model.Ms1CentroidTolerance, (float)value, v => _model.Ms1CentroidTolerance = v); }
    public double Ms2CentroidTolerance { get => _model.Ms2CentroidTolerance; set => Set(_model.Ms2CentroidTolerance, (float)value, v => _model.Ms2CentroidTolerance = v); }

    // ---- peak detection ----
    public SmoothingKind SmoothingMethod { get => _model.SmoothingMethod; set => Set(_model.SmoothingMethod, value, v => _model.SmoothingMethod = v); }
    public int SmoothingLevel { get => _model.SmoothingLevel; set => Set(_model.SmoothingLevel, value, v => _model.SmoothingLevel = v); }
    public int MinimumPeakWidth { get => _model.MinimumPeakWidth; set => Set(_model.MinimumPeakWidth, value, v => _model.MinimumPeakWidth = v); }
    public int MinimumPeakHeight { get => _model.MinimumPeakHeight; set => Set(_model.MinimumPeakHeight, value, v => _model.MinimumPeakHeight = v); }
    public double MassSliceWidth { get => _model.MassSliceWidth; set => Set(_model.MassSliceWidth, (float)value, v => _model.MassSliceWidth = v); }
    public int MaxChargeNumber { get => _model.MaxChargeNumber; set => Set(_model.MaxChargeNumber, value, v => _model.MaxChargeNumber = v); }
    public string SearchedAdductIons { get => _model.SearchedAdductIons; set => Set(_model.SearchedAdductIons, value, v => _model.SearchedAdductIons = v); }
    public double SigmaWindowValue { get => _model.SigmaWindowValue; set => Set(_model.SigmaWindowValue, (float)value, v => _model.SigmaWindowValue = v); }
    public double AmplitudeCutoff { get => _model.AmplitudeCutoff; set => Set(_model.AmplitudeCutoff, (float)value, v => _model.AmplitudeCutoff = v); }
    public double KeepIsotopeRange { get => _model.KeepIsotopeRange; set => Set(_model.KeepIsotopeRange, (float)value, v => _model.KeepIsotopeRange = v); }
    public bool ExcludeAfterPrecursor { get => _model.ExcludeAfterPrecursor; set => Set(_model.ExcludeAfterPrecursor, value, v => _model.ExcludeAfterPrecursor = v); }

    // ---- identification ----
    public string MspFilePath { get => _model.MspFilePath; set => Set(_model.MspFilePath, value, v => _model.MspFilePath = v); }
    public string TextDbFilePath { get => _model.TextDbFilePath; set => Set(_model.TextDbFilePath, value, v => _model.TextDbFilePath = v); }
    public double RtToleranceForAnnotation { get => _model.RtToleranceForAnnotation; set => Set(_model.RtToleranceForAnnotation, (float)value, v => _model.RtToleranceForAnnotation = v); }
    public double Ms1ToleranceForAnnotation { get => _model.Ms1ToleranceForAnnotation; set => Set(_model.Ms1ToleranceForAnnotation, (float)value, v => _model.Ms1ToleranceForAnnotation = v); }
    public double Ms2ToleranceForAnnotation { get => _model.Ms2ToleranceForAnnotation; set => Set(_model.Ms2ToleranceForAnnotation, (float)value, v => _model.Ms2ToleranceForAnnotation = v); }
    public double TotalScoreCutoff { get => _model.TotalScoreCutoff; set => Set(_model.TotalScoreCutoff, (float)value, v => _model.TotalScoreCutoff = v); }
    public double WeightedDotProductCutoff { get => _model.WeightedDotProductCutoff; set => Set(_model.WeightedDotProductCutoff, (float)value, v => _model.WeightedDotProductCutoff = v); }
    public double SimpleDotProductCutoff { get => _model.SimpleDotProductCutoff; set => Set(_model.SimpleDotProductCutoff, (float)value, v => _model.SimpleDotProductCutoff = v); }
    public double ReverseDotProductCutoff { get => _model.ReverseDotProductCutoff; set => Set(_model.ReverseDotProductCutoff, (float)value, v => _model.ReverseDotProductCutoff = v); }
    public double MatchedPeaksPercentageCutoff { get => _model.MatchedPeaksPercentageCutoff; set => Set(_model.MatchedPeaksPercentageCutoff, (float)value, v => _model.MatchedPeaksPercentageCutoff = v); }
    public double MinimumSpectrumMatch { get => _model.MinimumSpectrumMatch; set => Set(_model.MinimumSpectrumMatch, (float)value, v => _model.MinimumSpectrumMatch = v); }
    public bool UseRetentionInformationForScoring { get => _model.UseRetentionInformationForScoring; set => Set(_model.UseRetentionInformationForScoring, value, v => _model.UseRetentionInformationForScoring = v); }
    public bool UseRetentionInformationForFiltering { get => _model.UseRetentionInformationForFiltering; set => Set(_model.UseRetentionInformationForFiltering, value, v => _model.UseRetentionInformationForFiltering = v); }
    public bool OnlyReportTopHit { get => _model.OnlyReportTopHit; set => Set(_model.OnlyReportTopHit, value, v => _model.OnlyReportTopHit = v); }

    // ---- alignment ----
    public int AlignmentReferenceFileId { get => _model.AlignmentReferenceFileId; set => Set(_model.AlignmentReferenceFileId, value, v => _model.AlignmentReferenceFileId = v); }
    public double RtToleranceForAlignment { get => _model.RtToleranceForAlignment; set => Set(_model.RtToleranceForAlignment, (float)value, v => _model.RtToleranceForAlignment = v); }
    public double Ms1ToleranceForAlignment { get => _model.Ms1ToleranceForAlignment; set => Set(_model.Ms1ToleranceForAlignment, (float)value, v => _model.Ms1ToleranceForAlignment = v); }
    public double RtFactorForAlignment { get => _model.RtFactorForAlignment; set => Set(_model.RtFactorForAlignment, (float)value, v => _model.RtFactorForAlignment = v); }
    public double Ms1FactorForAlignment { get => _model.Ms1FactorForAlignment; set => Set(_model.Ms1FactorForAlignment, (float)value, v => _model.Ms1FactorForAlignment = v); }
    public double PeakCountFilter { get => _model.PeakCountFilter; set => Set(_model.PeakCountFilter, (float)value, v => _model.PeakCountFilter = v); }
    public double NPercentDetectedInOneGroup { get => _model.NPercentDetectedInOneGroup; set => Set(_model.NPercentDetectedInOneGroup, (float)value, v => _model.NPercentDetectedInOneGroup = v); }
    public bool GapFillingByCompulsion { get => _model.GapFillingByCompulsion; set => Set(_model.GapFillingByCompulsion, value, v => _model.GapFillingByCompulsion = v); }
    public bool TogetherWithAlignment { get => _model.TogetherWithAlignment; set => Set(_model.TogetherWithAlignment, value, v => _model.TogetherWithAlignment = v); }
    public bool IsHeightMatrixExport { get => _model.IsHeightMatrixExport; set => Set(_model.IsHeightMatrixExport, value, v => _model.IsHeightMatrixExport = v); }
    public int NumberOfThreads { get => _model.NumberOfThreads; set => Set(_model.NumberOfThreads, Math.Max(1, value), v => _model.NumberOfThreads = v); }

    // ---- GC-MS ----
    public RetentionKind RetentionType { get => _model.RetentionType; set => Set(_model.RetentionType, value, v => _model.RetentionType = v); }
    public RiCompoundKind RiCompoundType { get => _model.RiCompoundType; set => Set(_model.RiCompoundType, value, v => _model.RiCompoundType = v); }
    public RetentionKind AlignmentIndexType { get => _model.AlignmentIndexType; set => Set(_model.AlignmentIndexType, value, v => _model.AlignmentIndexType = v); }
    public string RiIndexFilePaths { get => _model.RiIndexFilePaths; set => Set(_model.RiIndexFilePaths, value, v => _model.RiIndexFilePaths = v); }
}
