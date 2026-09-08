using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDIAL.Desktop.Controls;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Pipeline.Curation;
using OpenDIAL.Pipeline.Results;
using OpenDIAL.Pipeline.Statistics;

namespace OpenDIAL.Desktop.ViewModels;

/// <summary>One row of the forest's confusion matrix, for the grid.</summary>
public sealed record ConfusionRow(string Class, string Predicted, double Error);

/// <summary>
/// The one-factor analysis: the confirmed analytes as ratios to their class standards (or every
/// feature as it is), through MetaboAnalyst's sequence — missing values, the filter, normalisation,
/// transformation, scaling — and out the other side into the tests, the plots, the clustering, the
/// forest and the enrichment. Every downstream view reads the one dataset this produces, so a
/// change to the preprocessing changes all of them together.
/// </summary>
public sealed partial class OneFactorViewModel : ViewModelBase
{
    private readonly IFileDialogService _dialogs;
    private IReadOnlyList<AlignmentSpotRow> _spots = Array.Empty<AlignmentSpotRow>();
    private IReadOnlyList<SampleInfo> _samples = Array.Empty<SampleInfo>();
    private CurationStore? _curation;
    private IReadOnlyList<StandardAssignment> _assignments = Array.Empty<StandardAssignment>();
    private AnalysisTable? _source;
    private TwoGroupResult? _comparison;
    private AnovaResult? _anova;
    private RandomForestResult? _forest;
    private EnrichmentResult? _enrichment;
    private KMeansResult? _kmeans;
    private Func<AnalysisTable, AnalysisTable>? _corrector;

    public OneFactorViewModel(IFileDialogService dialogs)
    {
        _dialogs = dialogs;
    }

    /// <summary>Raised when a feature is chosen anywhere, so the shell can open it in the ion table.</summary>
    public Action<int>? RequestShowFeature { get; set; }
    /// <summary>Set by the window: shows the standards dialog and returns the assignments, or null when cancelled.</summary>
    public Func<IReadOnlyList<AlignmentSpotRow>, IReadOnlyList<StandardAssignment>, Task<IReadOnlyList<StandardAssignment>?>>? ShowStandardsDialog { get; set; }
    /// <summary>Raised whenever the dataset changes, so the models that read it recompute.</summary>
    public event EventHandler? DataChanged;
    /// <summary>Raised when the analysis asks to be reloaded from the review, which the shell owns.</summary>
    public event EventHandler? ReloadRequested;
    /// <summary>Set when the review changed after the analysis was loaded: the confirmed set may be stale.</summary>
    [ObservableProperty] private bool _reviewIsNewer;

    [RelayCommand]
    private void ReloadFromReview()
    {
        ReviewIsNewer = false;
        ReloadRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Rewrites the source table before preprocessing — the drift correction, when it is switched on.</summary>
    public Func<AnalysisTable, AnalysisTable>? Corrector
    {
        get => _corrector;
        set { _corrector = value; RebuildSource(); }
    }

    public static string[] SourceModes { get; } = { "Confirmed analytes, as ratios to a standard", "Confirmed analytes, raw", "Every feature" };
    public static string[] MissingMethods { get; } = { "1/5 of the minimum", "Half of the minimum", "Minimum", "Mean", "Median", "K nearest features", "Keep the gaps" };
    public static string[] FilterMethods { get; } = { "None", "Interquartile range", "Standard deviation", "Median absolute deviation", "Relative standard deviation", "Mean intensity", "Median intensity" };
    public static string[] Normalizations { get; } = { "None", "Sum", "Median", "Probabilistic quotient", "Quotient to the controls", "Reference feature" };
    public static string[] Transforms { get; } = { "Log10", "Log2", "Natural log", "Square root", "Cube root", "None" };
    public static string[] Scalings { get; } = { "Auto (unit variance)", "Pareto", "Mean centre only" };
    public static string[] Adjustments { get; } = { "FDR (Benjamini–Hochberg)", "Holm", "Bonferroni", "None (raw p)" };
    public static string[] CorrelationKinds { get; } = { "Pearson", "Spearman", "Kendall" };
    public static string[] CorrelationBases { get; } = { "Most variable", "Lowest p in the comparison", "Lowest p in the ANOVA" };
    public static string[] PatternKinds { get; } = { "A feature's profile", "The class order" };
    public static string[] Distances { get; } = { "Euclidean", "Pearson", "Spearman", "Manhattan" };
    public static string[] Linkages { get; } = { "Average", "Complete", "Single", "Ward" };
    public static string[] HeatmapBases { get; } = { "Lowest p in the comparison", "Lowest p in the ANOVA", "Most variable", "Every feature" };
    public static string[] EnrichmentBases { get; } = { "Significant in the comparison", "Significant in the ANOVA", "Top of the forest" };

    // ---------------------------------------------------------------- source and preprocessing

    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private string _sourceMode = SourceModes[0];
    [ObservableProperty] private bool _useArea = true;
    [ObservableProperty] private bool _annotatedOnly;
    [ObservableProperty] private string _standardsSummary = "No standards chosen.";
    [ObservableProperty] private string _sourceSummary = "No results loaded.";
    [ObservableProperty] private int _confirmedCount;

    [ObservableProperty] private string _missingMethod = MissingMethods[0];
    [ObservableProperty] private string _maxMissingPercent = "50";
    [ObservableProperty] private string _filterMethod = FilterMethods[1];
    [ObservableProperty] private string _filterPercent = string.Empty;
    [ObservableProperty] private string _maxControlRsd = "0";
    [ObservableProperty] private string _normalization = Normalizations[0];
    [ObservableProperty] private string _referenceFeature = string.Empty;
    [ObservableProperty] private string _transform = Transforms[0];
    [ObservableProperty] private string _scaling = Scalings[0];
    [ObservableProperty] private string _dataSummary = string.Empty;
    [ObservableProperty] private string _dataDetail = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _autoApply = true;
    [ObservableProperty] private IReadOnlyList<BoxGroup> _beforeBoxes = Array.Empty<BoxGroup>();
    [ObservableProperty] private IReadOnlyList<BoxGroup> _afterBoxes = Array.Empty<BoxGroup>();
    [ObservableProperty] private IReadOnlyList<BoxGroup> _beforeFeatureBoxes = Array.Empty<BoxGroup>();
    [ObservableProperty] private IReadOnlyList<BoxGroup> _afterFeatureBoxes = Array.Empty<BoxGroup>();
    public ObservableCollection<string> ReferenceFeatures { get; } = new();

    /// <summary>The dataset every analysis reads.</summary>
    public PreprocessedData? Data { get; private set; }

    /// <summary>The classes with at least two injections, for the comparisons.</summary>
    public ObservableCollection<string> Classes { get; } = new();
    public ObservableCollection<string> AllClasses { get; } = new();

    public void Clear()
    {
        _spots = Array.Empty<AlignmentSpotRow>();
        _samples = Array.Empty<SampleInfo>();
        _curation = null;
        _assignments = Array.Empty<StandardAssignment>();
        _source = null;
        _corrector = null;
        Data = null;
        HasResults = false;
        ConfirmedCount = 0;
        StandardsSummary = "No standards chosen.";
        SourceSummary = "No results loaded.";
        DataSummary = string.Empty;
        DataDetail = string.Empty;
        Classes.Clear();
        AllClasses.Clear();
        ReferenceFeatures.Clear();
        ClearResults();
    }

    private void ClearResults()
    {
        _comparison = null;
        _anova = null;
        _forest = null;
        _enrichment = null;
        _kmeans = null;
        ComparisonRows = Array.Empty<FeatureComparison>();
        VolcanoPoints = Array.Empty<ScatterPoint>();
        FoldChangePoints = Array.Empty<ScatterPoint>();
        AnovaRows = Array.Empty<FeatureAnova>();
        AnovaPoints = Array.Empty<ScatterPoint>();
        CorrelationHeatmap = null;
        PatternRows = Array.Empty<PatternMatch>();
        PatternBars = Array.Empty<RankItem>();
        Heatmap = null;
        KMeansPoints = Array.Empty<ScatterPoint>();
        ImportanceBars = Array.Empty<RankItem>();
        Confusion = Array.Empty<ConfusionRow>();
        EnrichmentRows = Array.Empty<EnrichmentRow>();
        EnrichmentBars = Array.Empty<RankItem>();
        ClassChanges = Array.Empty<ClassChange>();
        ChainMap = null;
        FeatureBoxes = Array.Empty<BoxGroup>();
        BeforeBoxes = Array.Empty<BoxGroup>();
        AfterBoxes = Array.Empty<BoxGroup>();
        BeforeFeatureBoxes = Array.Empty<BoxGroup>();
        AfterFeatureBoxes = Array.Empty<BoxGroup>();
        ComparisonMessage = "Not compared yet.";
        AnovaMessage = "Not computed yet.";
        CorrelationMessage = "Not computed yet.";
        PatternMessage = "Not searched yet.";
        HeatmapMessage = "Not built yet.";
        KMeansMessage = "Not run yet.";
        ForestMessage = "Not grown yet.";
        EnrichmentMessage = "Not computed yet.";
    }

    /// <summary>
    /// Loads a result and its review. The confirmed analytes come from the review; the standards
    /// come from the review's sidecar when they were chosen before, and are suggested otherwise.
    /// </summary>
    public void Load(IReadOnlyList<AlignmentSpotRow> spots, IReadOnlyList<SampleInfo> samples, CurationStore? curation, Func<AnalysisTable, AnalysisTable>? corrector = null)
    {
        _spots = spots;
        _samples = samples;
        _curation = curation;
        _corrector = corrector;
        ReviewIsNewer = false;
        var confirmed = Confirmed();
        ConfirmedCount = confirmed.Count;
        var suggested = RelativeAbundance.Suggest(confirmed);
        if (curation is not null && curation.InternalStandards.Count > 0)
        {
            var known = curation.InternalStandards;
            _assignments = suggested.Select(a => known.TryGetValue(a.Class, out var id) && confirmed.Any(f => f.Id == id) ? a with { StandardFeatureId = id } : a).ToList();
        }
        else
        {
            _assignments = suggested;
        }
        if (confirmed.Count == 0 && SourceMode != SourceModes[2]) _sourceMode = SourceModes[2];
        OnPropertyChanged(nameof(SourceMode));
        AllClasses.Clear();
        foreach (var c in samples.Select(AnalysisTable.ClassOf).Distinct()) AllClasses.Add(c);
        HasResults = spots.Count > 0 && samples.Count > 1;
        RebuildSource();
    }

    private List<AlignmentSpotRow> Confirmed() =>
        _curation is null ? new List<AlignmentSpotRow>() : _spots.Where(s => _curation.HasTag(s.Id, PeakSpotTagKind.Confirmed)).ToList();

    partial void OnSourceModeChanged(string value) => RebuildSource();
    partial void OnUseAreaChanged(bool value) => RebuildSource();
    partial void OnAnnotatedOnlyChanged(bool value) => RebuildSource();

    /// <summary>Builds the raw table the preprocessing starts from, then runs it.</summary>
    public void RebuildSource()
    {
        if (!HasResults) return;
        var confirmed = Confirmed();
        AnalysisTable table;
        if (SourceMode == SourceModes[2])
        {
            var features = AnnotatedOnly ? _spots.Where(s => s.IsAnnotated).ToList() : _spots.ToList();
            table = RelativeAbundance.Raw(features, _samples, UseArea);
            StandardsSummary = "Every feature as it is; standards do not apply.";
            SourceSummary = $"{features.Count} feature(s) as peak {(UseArea ? "area" : "height")}.";
        }
        else if (SourceMode == SourceModes[1])
        {
            table = RelativeAbundance.Raw(confirmed, _samples, UseArea);
            StandardsSummary = "Confirmed analytes as raw responses; standards do not apply.";
            SourceSummary = confirmed.Count == 0 ? "No confirmed analyte. Confirm features in the review workspace first." : $"{confirmed.Count} confirmed analyte(s) as peak {(UseArea ? "area" : "height")}.";
        }
        else
        {
            var (ratios, report) = RelativeAbundance.Build(confirmed, _samples, _assignments, UseArea);
            table = ratios;
            var chosen = _assignments.Where(a => a.StandardFeatureId is not null).ToList();
            StandardsSummary = chosen.Count == 0
                ? $"{RelativeAbundance.ClassesWithConfirmed(confirmed).Count} class(es) with confirmed analytes, no standard chosen yet."
                : string.Join(" · ", chosen.Select(a => $"{a.Class} ÷ {confirmed.FirstOrDefault(f => f.Id == a.StandardFeatureId)?.Name ?? "?"}"))
                  + (report.ClassesWithoutStandard.Count > 0 ? $" · raw: {string.Join(", ", report.ClassesWithoutStandard)}" : string.Empty);
            SourceSummary = report.Message;
        }
        if (_corrector is not null) table = _corrector(table);
        _source = table;
        ReferenceFeatures.Clear();
        foreach (var f in table.Features.Take(2000)) ReferenceFeatures.Add(AnalysisTable.LabelOf(f));
        Classes.Clear();
        foreach (var g in Univariate.GroupIndices(table).Where(g => g.Value.Count >= 2)) Classes.Add(g.Key);
        if (Classes.Count >= 2)
        {
            if (!Classes.Contains(ClassA)) ClassA = Classes[0];
            if (!Classes.Contains(ClassB) || ClassB == ClassA) ClassB = Classes.First(c => c != ClassA);
        }
        if (AutoApply) Apply();
    }

    /// <summary>Opens the standards dialog and rebuilds the ratios with what it returns.</summary>
    [RelayCommand]
    private async Task ChooseStandards()
    {
        if (ShowStandardsDialog is null) return;
        var confirmed = Confirmed();
        if (confirmed.Count == 0)
        {
            StandardsSummary = "Confirm features in the review workspace first; there is nothing to take a ratio of.";
            return;
        }
        var result = await ShowStandardsDialog(confirmed, _assignments);
        if (result is null) return;
        _assignments = result;
        _curation?.SetInternalStandards(result.Select(a => new KeyValuePair<string, int?>(a.Class, a.StandardFeatureId)));
        if (SourceMode != SourceModes[0]) SourceMode = SourceModes[0];
        else RebuildSource();
    }

    private PreprocessingOptions Options()
    {
        var reference = _source?.Features.FirstOrDefault(f => AnalysisTable.LabelOf(f) == ReferenceFeature);
        return new PreprocessingOptions
        {
            Missing = (MissingValueMethod)Math.Max(0, Array.IndexOf(MissingMethods, MissingMethod)),
            MaxMissingFraction = Math.Clamp(Parse(MaxMissingPercent, 50), 0, 100) / 100.0,
            Filter = (FilterMethod)Math.Max(0, Array.IndexOf(FilterMethods, FilterMethod)),
            FilterFraction = string.IsNullOrWhiteSpace(FilterPercent) ? double.NaN : Math.Clamp(Parse(FilterPercent, 0), 0, 95) / 100.0,
            MaxControlRsd = Math.Max(0, Parse(MaxControlRsd, 0)),
            Normalization = (SampleNormalization)Math.Max(0, Array.IndexOf(Normalizations, Normalization)),
            ReferenceFeatureId = reference?.Id,
            Transform = Transform switch
            {
                "Log2" => Transformation.Log2,
                "Natural log" => Transformation.NaturalLog,
                "Square root" => Transformation.SquareRoot,
                "Cube root" => Transformation.CubeRoot,
                "None" => Transformation.None,
                _ => Transformation.Log10,
            },
            Scaling = Scaling switch { "Pareto" => ValueScaling.Pareto, "Mean centre only" => ValueScaling.None, _ => ValueScaling.Auto },
        };
    }

    private static double Parse(string? text, double fallback) =>
        double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

    /// <summary>Runs the preprocessing and refreshes everything that reads the dataset.</summary>
    [RelayCommand]
    public void Apply()
    {
        if (_source is null) return;
        IsBusy = true;
        try
        {
            Data = Preprocessing.Run(_source, Options());
            DataSummary = Data.Report.Summary;
            DataDetail = $"{Data.Raw.ValueName} → {Data.Transformed.ValueName} · {Data.Scaled.SampleCount} injections in {AllClasses.Count} class(es), {Classes.Count} of them with replicates";
            ClearResults();
            BuildNormalizationViews();
            DataChanged?.Invoke(this, EventArgs.Empty);
            if (Classes.Count >= 2) Compare();
            if (Classes.Count >= 3) RunAnova();
        }
        catch (Exception ex)
        {
            DataSummary = "The preprocessing failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>MetaboAnalyst's before-and-after view: one box per injection, and the spread of a few features.</summary>
    private void BuildNormalizationViews()
    {
        if (Data is null) return;
        var before = Data.Filtered;
        var after = Data.Transformed;
        BeforeBoxes = Enumerable.Range(0, before.SampleCount)
            .Select(i => new BoxGroup(before.Samples[i].FileName, before.Row(i).Where(v => !double.IsNaN(v) && v > 0).Select(Math.Log10).ToList(), AnalysisTable.ClassOf(before.Samples[i])))
            .ToList();
        AfterBoxes = Enumerable.Range(0, after.SampleCount)
            .Select(i => new BoxGroup(after.Samples[i].FileName, after.Row(i).Where(v => !double.IsNaN(v)).ToList(), AnalysisTable.ClassOf(after.Samples[i])))
            .ToList();
        var picks = Enumerable.Range(0, before.FeatureCount).OrderBy(j => j * 7919 % Math.Max(1, before.FeatureCount)).Take(20).ToList();
        BeforeFeatureBoxes = picks.Select(j => new BoxGroup(AnalysisTable.LabelOf(before.Features[j]), before.Column(j).Where(v => !double.IsNaN(v) && v > 0).Select(Math.Log10).ToList(), "feature")).ToList();
        AfterFeatureBoxes = picks.Select(j => new BoxGroup(AnalysisTable.LabelOf(after.Features[j]), after.Column(j).Where(v => !double.IsNaN(v)).ToList(), "feature")).ToList();
    }

    // ---------------------------------------------------------------- two groups

    [ObservableProperty] private string _classA = string.Empty;
    [ObservableProperty] private string _classB = string.Empty;
    [ObservableProperty] private bool _nonParametric;
    [ObservableProperty] private bool _equalVariance;
    [ObservableProperty] private bool _paired;
    [ObservableProperty] private string _adjustment = Adjustments[0];
    [ObservableProperty] private string _alphaText = "0.05";
    [ObservableProperty] private string _foldThresholdText = "2.0";
    [ObservableProperty] private IReadOnlyList<FeatureComparison> _comparisonRows = Array.Empty<FeatureComparison>();
    [ObservableProperty] private FeatureComparison? _selectedComparison;
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _volcanoPoints = Array.Empty<ScatterPoint>();
    [ObservableProperty] private ScatterPoint? _selectedVolcanoPoint;
    [ObservableProperty] private IReadOnlyList<ReferenceLine> _volcanoLines = Array.Empty<ReferenceLine>();
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _foldChangePoints = Array.Empty<ScatterPoint>();
    [ObservableProperty] private ScatterPoint? _selectedFoldChangePoint;
    [ObservableProperty] private string _comparisonMessage = "Not compared yet.";
    [ObservableProperty] private string _comparisonCounts = string.Empty;
    [ObservableProperty] private IReadOnlyList<BoxGroup> _featureBoxes = Array.Empty<BoxGroup>();
    [ObservableProperty] private string _featureBoxTitle = "Select a feature to see its spread per class";

    public IReadOnlyDictionary<string, Color> VolcanoColours { get; } = new Dictionary<string, Color>
    {
        ["up"] = Color.Parse("#d62728"),
        ["down"] = Color.Parse("#1f77b4"),
        ["not significant"] = Color.Parse("#b0b4bb"),
    };

    private PAdjustment AdjustmentKind => Adjustment switch { "Holm" => PAdjustment.Holm, "Bonferroni" => PAdjustment.Bonferroni, "None (raw p)" => PAdjustment.None, _ => PAdjustment.FalseDiscoveryRate };
    private double Alpha => Math.Clamp(Parse(AlphaText, 0.05), 1e-9, 1);
    private double FoldThreshold => Math.Max(1, Parse(FoldThresholdText, 2));

    [RelayCommand]
    private void Compare()
    {
        if (Data is null || string.IsNullOrEmpty(ClassA) || string.IsNullOrEmpty(ClassB) || ClassA == ClassB)
        {
            ComparisonMessage = Classes.Count < 2 ? "Two classes with at least two injections each are needed." : "Choose two different classes.";
            return;
        }
        try
        {
            _comparison = Univariate.Compare(Data.Normalized, Data.Transformed, ClassA, ClassB, NonParametric, EqualVariance, Paired, AdjustmentKind);
            ComparisonMessage = _comparison.Message;
            var alpha = Alpha;
            var fold = FoldThreshold;
            var adjusted = AdjustmentKind != PAdjustment.None;
            ComparisonRows = _comparison.Features.OrderBy(r => double.IsNaN(r.P) ? 2 : r.P).ToList();
            var log2Fold = Math.Log2(fold);
            VolcanoPoints = _comparison.Features
                .Where(r => !double.IsNaN(r.Log2FoldChange) && !double.IsNaN(r.P))
                .Select(r =>
                {
                    var significant = r.IsSignificant(alpha, adjusted) && Math.Abs(r.Log2FoldChange) >= log2Fold;
                    var group = !significant ? "not significant" : r.Log2FoldChange > 0 ? "up" : "down";
                    return new ScatterPoint(r.Log2FoldChange, r.NegativeLog10P, r.Label, group, r) { Labelled = significant && r.NegativeLog10P >= 2 };
                })
                .ToList();
            VolcanoLines = new[]
            {
                new ReferenceLine(-log2Fold, true, $"1/{fold:0.#}"),
                new ReferenceLine(log2Fold, true, $"×{fold:0.#}"),
                new ReferenceLine(-Math.Log10(alpha), false, $"p {alpha:0.###}"),
            };
            FoldChangePoints = _comparison.Features
                .Where(r => !double.IsNaN(r.Log2FoldChange))
                .Select((r, i) => new ScatterPoint(i + 1, r.Log2FoldChange, r.Label, Math.Abs(r.Log2FoldChange) >= log2Fold ? (r.Log2FoldChange > 0 ? "up" : "down") : "not significant", r))
                .ToList();
            var up = VolcanoPoints.Count(p => p.Group == "up");
            var down = VolcanoPoints.Count(p => p.Group == "down");
            ComparisonCounts = $"{up} higher in {ClassA}, {down} higher in {ClassB}, at {Univariate.AdjustName(AdjustmentKind)} ≤ {alpha:0.###} and fold change ≥ {fold:0.#}";
            SelectedComparison = ComparisonRows.FirstOrDefault();
        }
        catch (Exception ex)
        {
            ComparisonMessage = "The comparison failed: " + ex.Message;
        }
    }

    partial void OnSelectedComparisonChanged(FeatureComparison? value)
    {
        if (value is null) return;
        ShowFeature(value.FeatureId, value.Label);
    }

    partial void OnSelectedVolcanoPointChanged(ScatterPoint? value)
    {
        if (value?.Tag is FeatureComparison c) { SelectedComparison = ComparisonRows.FirstOrDefault(r => r.FeatureId == c.FeatureId); }
    }

    partial void OnSelectedFoldChangePointChanged(ScatterPoint? value)
    {
        if (value?.Tag is FeatureComparison c) { SelectedComparison = ComparisonRows.FirstOrDefault(r => r.FeatureId == c.FeatureId); }
    }

    /// <summary>The selected feature's values per class, as boxes; and a jump to it in the ion table on request.</summary>
    private void ShowFeature(int featureId, string label)
    {
        if (Data is null) return;
        var j = Data.Transformed.Features.ToList().FindIndex(f => f.Id == featureId);
        if (j < 0) return;
        var column = Data.Transformed.Column(j);
        FeatureBoxes = Univariate.GroupIndices(Data.Transformed)
            .Select(g => new BoxGroup(g.Key, g.Value.Select(i => column[i]).Where(v => !double.IsNaN(v)).ToList(), g.Key))
            .ToList();
        FeatureBoxTitle = $"{label} · {Data.Transformed.ValueName}";
    }

    [RelayCommand]
    private void OpenSelectedFeature()
    {
        var id = SelectedComparison?.FeatureId ?? SelectedAnova?.FeatureId;
        if (id is not null) RequestShowFeature?.Invoke(id.Value);
    }

    // ---------------------------------------------------------------- anova

    [ObservableProperty] private bool _anovaNonParametric;
    [ObservableProperty] private IReadOnlyList<FeatureAnova> _anovaRows = Array.Empty<FeatureAnova>();
    [ObservableProperty] private FeatureAnova? _selectedAnova;
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _anovaPoints = Array.Empty<ScatterPoint>();
    [ObservableProperty] private ScatterPoint? _selectedAnovaPoint;
    [ObservableProperty] private string _anovaMessage = "Not computed yet.";
    [ObservableProperty] private string _postHocText = string.Empty;

    [RelayCommand]
    private void RunAnova()
    {
        if (Data is null) return;
        try
        {
            _anova = Univariate.Anova(Data.Transformed, AnovaNonParametric, AdjustmentKind, Alpha);
            AnovaMessage = _anova.Message;
            AnovaRows = _anova.Features.OrderBy(r => double.IsNaN(r.P) ? 2 : r.P).ToList();
            var alpha = Alpha;
            var adjusted = AdjustmentKind != PAdjustment.None;
            AnovaPoints = _anova.Features.Where(r => !double.IsNaN(r.P))
                .Select((r, i) => new ScatterPoint(i + 1, r.NegativeLog10P, r.Label, (adjusted ? r.AdjustedP : r.P) <= alpha ? "significant" : "not significant", r))
                .ToList();
            SelectedAnova = AnovaRows.FirstOrDefault();
        }
        catch (Exception ex)
        {
            AnovaMessage = "The analysis of variance failed: " + ex.Message;
        }
    }

    partial void OnSelectedAnovaChanged(FeatureAnova? value)
    {
        if (value is null) { PostHocText = string.Empty; return; }
        ShowFeature(value.FeatureId, value.Label);
        PostHocText = value.PostHoc.Count == 0
            ? "No post-hoc comparison: the feature did not pass the overall test."
            : string.Join("\n", value.PostHoc.Select(p => $"{p.ClassA} − {p.ClassB}: {p.Difference:+0.00;-0.00} · p {p.P:0.0000}"));
    }

    partial void OnSelectedAnovaPointChanged(ScatterPoint? value)
    {
        if (value?.Tag is FeatureAnova a) SelectedAnova = AnovaRows.FirstOrDefault(r => r.FeatureId == a.FeatureId);
    }

    // ---------------------------------------------------------------- correlations

    [ObservableProperty] private string _correlationKind = CorrelationKinds[0];
    [ObservableProperty] private string _correlationBasis = CorrelationBases[0];
    [ObservableProperty] private string _topCountText = "25";
    [ObservableProperty] private bool _correlateSamples;
    [ObservableProperty] private HeatmapData? _correlationHeatmap;
    [ObservableProperty] private string _correlationMessage = "Not computed yet.";
    [ObservableProperty] private int _correlationSelectedId = -1;
    [ObservableProperty] private string _patternKind = PatternKinds[0];
    [ObservableProperty] private string _patternFeature = string.Empty;
    [ObservableProperty] private string _patternClassOrder = string.Empty;
    [ObservableProperty] private IReadOnlyList<PatternMatch> _patternRows = Array.Empty<PatternMatch>();
    [ObservableProperty] private IReadOnlyList<RankItem> _patternBars = Array.Empty<RankItem>();
    [ObservableProperty] private string _patternMessage = "Not searched yet.";
    [ObservableProperty] private object? _patternSelectedTag;

    private CorrelationKind CorrelationKindValue => CorrelationKind switch { "Spearman" => Pipeline.Statistics.CorrelationKind.Spearman, "Kendall" => Pipeline.Statistics.CorrelationKind.Kendall, _ => Pipeline.Statistics.CorrelationKind.Pearson };

    /// <summary>The features a "top N" view shows, by whichever ranking was asked for.</summary>
    private List<int> TopFeatures(string basis, int count)
    {
        if (Data is null) return new List<int>();
        var table = Data.Transformed;
        var index = table.Features.Select((f, j) => (f.Id, j)).ToDictionary(x => x.Id, x => x.j);
        IEnumerable<int> ordered;
        if (basis.Contains("comparison", StringComparison.OrdinalIgnoreCase) && _comparison is not null)
        {
            ordered = _comparison.Features.Where(r => !double.IsNaN(r.P)).OrderBy(r => r.P).Select(r => index.TryGetValue(r.FeatureId, out var j) ? j : -1).Where(j => j >= 0);
        }
        else if (basis.Contains("ANOVA", StringComparison.OrdinalIgnoreCase) && _anova is not null)
        {
            ordered = _anova.Features.Where(r => !double.IsNaN(r.P)).OrderBy(r => r.P).Select(r => index.TryGetValue(r.FeatureId, out var j) ? j : -1).Where(j => j >= 0);
        }
        else if (basis.StartsWith("Every", StringComparison.OrdinalIgnoreCase))
        {
            ordered = Enumerable.Range(0, table.FeatureCount);
        }
        else
        {
            ordered = Enumerable.Range(0, table.FeatureCount).OrderByDescending(j => Preprocessing.StandardDeviation(table.Column(j).Where(v => !double.IsNaN(v)).ToList()));
        }
        return ordered.Take(count).ToList();
    }

    [RelayCommand]
    private void Correlate()
    {
        if (Data is null) return;
        try
        {
            var count = (int)Math.Clamp(Parse(TopCountText, 25), 2, 200);
            if (CorrelateSamples)
            {
                var m = Correlations.BetweenSamples(Data.Transformed, CorrelationKindValue);
                CorrelationHeatmap = HeatmapData.From(m);
                CorrelationMessage = $"{m.Kind} correlation between the {m.Labels.Count} injections over {Data.Transformed.FeatureCount} features.";
            }
            else
            {
                var top = TopFeatures(CorrelationBasis, count);
                var m = Correlations.BetweenFeatures(Data.Transformed, top, CorrelationKindValue);
                CorrelationHeatmap = HeatmapData.From(m);
                CorrelationMessage = $"{m.Kind} correlation between the {top.Count} features ({CorrelationBasis.ToLowerInvariant()}) over {Data.Transformed.SampleCount} injections.";
            }
        }
        catch (Exception ex)
        {
            CorrelationMessage = "The correlation failed: " + ex.Message;
        }
    }

    partial void OnCorrelationSelectedIdChanged(int value)
    {
        if (value >= 0 && !CorrelateSamples) RequestShowFeature?.Invoke(value);
    }

    [RelayCommand]
    private void SearchPattern()
    {
        if (Data is null) return;
        try
        {
            double[] pattern;
            int? exclude = null;
            string what;
            if (PatternKind == PatternKinds[0])
            {
                var j = Data.Transformed.Features.ToList().FindIndex(f => AnalysisTable.LabelOf(f) == PatternFeature);
                if (j < 0) { PatternMessage = "Choose a feature to search with."; return; }
                pattern = Data.Transformed.Column(j);
                exclude = Data.Transformed.Features[j].Id;
                what = PatternFeature;
            }
            else
            {
                var order = PatternClassOrder.Split(new[] { ',', ';', '>' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                if (order.Count < 2) order = AllClasses.ToList();
                pattern = Correlations.ClassPattern(Data.Transformed, order);
                what = "the order " + string.Join(" < ", order);
            }
            var hits = Correlations.PatternSearch(Data.Transformed, pattern, CorrelationKindValue, exclude);
            PatternRows = hits;
            var top = hits.Where(h => h.Correlation > 0).Take(25).Concat(hits.Where(h => h.Correlation < 0).Take(25)).OrderByDescending(h => h.Correlation).ToList();
            PatternBars = top.Select(h => new RankItem(h.Label, h.Correlation, h.Group, h.FeatureId, null, $"p {h.P:0.0000}")).ToList();
            PatternMessage = $"{hits.Count} features correlated with {what} ({CorrelationKind}); the 25 most positive and negative are drawn.";
        }
        catch (Exception ex)
        {
            PatternMessage = "The search failed: " + ex.Message;
        }
    }

    partial void OnPatternSelectedTagChanged(object? value)
    {
        if (value is int id) RequestShowFeature?.Invoke(id);
    }

    // ---------------------------------------------------------------- heatmap and k-means

    [ObservableProperty] private string _heatmapDistance = Distances[0];
    [ObservableProperty] private string _heatmapLinkage = Linkages[0];
    [ObservableProperty] private string _heatmapBasis = HeatmapBases[0];
    [ObservableProperty] private string _heatmapTopText = "50";
    [ObservableProperty] private bool _heatmapStandardize = true;
    [ObservableProperty] private bool _heatmapClusterColumns = true;
    [ObservableProperty] private HeatmapData? _heatmap;
    [ObservableProperty] private string _heatmapMessage = "Not built yet.";
    [ObservableProperty] private int _heatmapSelectedId = -1;
    [ObservableProperty] private string _kText = "2";
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _kMeansPoints = Array.Empty<ScatterPoint>();
    [ObservableProperty] private string _kMeansMessage = "Not run yet.";
    [ObservableProperty] private string _kMeansLabel = "PC1 against PC2, coloured by cluster";

    private DistanceKind DistanceValue => HeatmapDistance switch { "Pearson" => DistanceKind.Pearson, "Spearman" => DistanceKind.Spearman, "Manhattan" => DistanceKind.Manhattan, _ => DistanceKind.Euclidean };
    private LinkageKind LinkageValue => HeatmapLinkage switch { "Complete" => LinkageKind.Complete, "Single" => LinkageKind.Single, "Ward" => LinkageKind.Ward, _ => LinkageKind.Average };

    [RelayCommand]
    private void BuildHeatmap()
    {
        if (Data is null) return;
        try
        {
            var count = HeatmapBasis.StartsWith("Every", StringComparison.OrdinalIgnoreCase) ? Data.Transformed.FeatureCount : (int)Math.Clamp(Parse(HeatmapTopText, 50), 2, 2000);
            var top = TopFeatures(HeatmapBasis, count);
            var result = Clustering.Heatmap(Data.Transformed, top, DistanceValue, LinkageValue, HeatmapStandardize, HeatmapClusterColumns);
            Heatmap = HeatmapData.From(result);
            HeatmapMessage = $"{top.Count} features ({HeatmapBasis.ToLowerInvariant()}) against {Data.Transformed.SampleCount} injections · {HeatmapDistance.ToLowerInvariant()} distance, {HeatmapLinkage.ToLowerInvariant()} linkage" + (HeatmapStandardize ? " · rows standardised" : string.Empty);
        }
        catch (Exception ex)
        {
            HeatmapMessage = "The heatmap failed: " + ex.Message;
        }
    }

    partial void OnHeatmapSelectedIdChanged(int value)
    {
        if (value >= 0) RequestShowFeature?.Invoke(value);
    }

    [RelayCommand]
    private void RunKMeans()
    {
        if (Data is null) return;
        try
        {
            var k = (int)Math.Clamp(Parse(KText, 2), 1, Math.Max(1, Data.Scaled.SampleCount));
            _kmeans = Clustering.KMeans(Data.Scaled, k);
            var pca = Pca.Compute(Data.Scaled, 2);
            KMeansPoints = pca.Scores.Select((s, i) => new ScatterPoint(s.Components[0], s.Components.Length > 1 ? s.Components[1] : 0, $"{s.Sample} ({s.Class})", $"cluster {_kmeans.Assignment[i] + 1}", s) { Labelled = true }).ToList();
            KMeansLabel = pca.ExplainedVariance.Count > 1 ? $"PC1 ({pca.ExplainedVariance[0]:F1} %) against PC2 ({pca.ExplainedVariance[1]:F1} %), coloured by cluster" : "PC1";
            var composition = Enumerable.Range(0, k).Select(c => $"cluster {c + 1}: " + string.Join(", ", _kmeans.Labels.Where((_, i) => _kmeans.Assignment[i] == c).Select((l, idx) => $"{l} [{_kmeans.Groups[_kmeans.Labels.ToList().IndexOf(l)]}]")));
            KMeansMessage = $"k = {k} over {Data.Scaled.SampleCount} injections · within-cluster sum of squares {_kmeans.WithinSumOfSquares:F1}\n" + string.Join("\n", composition);
        }
        catch (Exception ex)
        {
            KMeansMessage = "K-means failed: " + ex.Message;
        }
    }

    // ---------------------------------------------------------------- random forest

    [ObservableProperty] private string _treesText = "500";
    [ObservableProperty] private string _featuresPerSplitText = string.Empty;
    [ObservableProperty] private IReadOnlyList<RankItem> _importanceBars = Array.Empty<RankItem>();
    [ObservableProperty] private IReadOnlyList<ForestImportance> _importanceRows = Array.Empty<ForestImportance>();
    [ObservableProperty] private IReadOnlyList<ConfusionRow> _confusion = Array.Empty<ConfusionRow>();
    [ObservableProperty] private string _forestMessage = "Not grown yet.";
    [ObservableProperty] private bool _isGrowing;
    [ObservableProperty] private object? _importanceSelectedTag;
    [ObservableProperty] private IReadOnlyList<string> _classNames = Array.Empty<string>();

    [RelayCommand]
    private async Task RunForest()
    {
        if (Data is null) return;
        var trees = (int)Math.Clamp(Parse(TreesText, 500), 10, 5000);
        int? mtry = string.IsNullOrWhiteSpace(FeaturesPerSplitText) ? null : (int)Math.Max(1, Parse(FeaturesPerSplitText, 1));
        IsGrowing = true;
        ForestMessage = $"Growing {trees} trees…";
        try
        {
            var matrix = Data.Scaled;
            var transformed = Data.Transformed;
            var result = await Task.Run(() => RandomForest.Compute(matrix, trees, mtry));
            _forest = result;
            ForestMessage = result.Message;
            ClassNames = result.Classes;
            var groups = Univariate.GroupIndices(transformed);
            ImportanceRows = result.Importance.Take(300).ToList();
            ImportanceBars = result.Importance.Take(25).Select(i =>
            {
                var j = transformed.Features.ToList().FindIndex(f => f.Id == i.FeatureId);
                var levels = j < 0 ? null : result.Classes.Select(c => groups.TryGetValue(c, out var idx) ? idx.Select(x => transformed.Values[x, j]).Where(v => !double.IsNaN(v)).DefaultIfEmpty(0).Average() : 0).ToList();
                return new RankItem(i.Label, i.MeanDecreaseAccuracy, i.Group, i.FeatureId, levels, $"Gini decrease {i.MeanDecreaseGini:0.000}");
            }).ToList();
            Confusion = result.Classes.Select((c, r) => new ConfusionRow(c, string.Join(", ", result.Classes.Select((p, k) => $"{p} {result.Confusion[r, k]}")), result.ClassErrors[r].Error)).ToList();
        }
        catch (Exception ex)
        {
            ForestMessage = "The forest failed: " + ex.Message;
        }
        finally
        {
            IsGrowing = false;
        }
    }

    partial void OnImportanceSelectedTagChanged(object? value)
    {
        if (value is int id) RequestShowFeature?.Invoke(id);
    }

    // ---------------------------------------------------------------- enrichment

    [ObservableProperty] private string _enrichmentBasis = EnrichmentBases[0];
    [ObservableProperty] private bool _includeSpecies;
    [ObservableProperty] private string _minimumSetSizeText = "3";
    [ObservableProperty] private IReadOnlyList<EnrichmentRow> _enrichmentRows = Array.Empty<EnrichmentRow>();
    [ObservableProperty] private IReadOnlyList<RankItem> _enrichmentBars = Array.Empty<RankItem>();
    [ObservableProperty] private IReadOnlyList<ClassChange> _classChanges = Array.Empty<ClassChange>();
    [ObservableProperty] private IReadOnlyList<RankItem> _classChangeBars = Array.Empty<RankItem>();
    [ObservableProperty] private HeatmapData? _chainMap;
    [ObservableProperty] private string _chainMapClass = "All classes";
    [ObservableProperty] private string _enrichmentMessage = "Not computed yet.";
    public ObservableCollection<string> ChainMapClasses { get; } = new();

    [RelayCommand]
    private void RunEnrichment()
    {
        if (Data is null) return;
        try
        {
            var alpha = Alpha;
            var adjusted = AdjustmentKind != PAdjustment.None;
            HashSet<int> significant;
            if (EnrichmentBasis == EnrichmentBases[1] && _anova is not null)
            {
                significant = _anova.Features.Where(r => (adjusted ? r.AdjustedP : r.P) <= alpha).Select(r => r.FeatureId).ToHashSet();
            }
            else if (EnrichmentBasis == EnrichmentBases[2] && _forest is not null)
            {
                significant = _forest.Importance.Take(Math.Max(5, _forest.Importance.Count / 10)).Select(i => i.FeatureId).ToHashSet();
            }
            else
            {
                if (_comparison is null) Compare();
                significant = _comparison?.Features.Where(r => r.IsSignificant(alpha, adjusted) && Math.Abs(r.Log2FoldChange) >= Math.Log2(FoldThreshold)).Select(r => r.FeatureId).ToHashSet() ?? new HashSet<int>();
            }
            var minimum = (int)Math.Clamp(Parse(MinimumSetSizeText, 3), 2, 100);
            _enrichment = Enrichment.OverRepresentation(Data.Transformed.Features, significant, minimum, AdjustmentKind, IncludeSpecies);
            EnrichmentRows = _enrichment.Rows;
            EnrichmentBars = _enrichment.Rows.Take(30).Select(r => new RankItem($"{r.Set}", r.NegativeLog10P, r.Kind, r.Set, null, $"{r.Hits} of {r.SetSize}, expected {r.Expected:0.0} · ratio {r.EnrichmentRatio:0.0} · {Univariate.AdjustName(AdjustmentKind)} {r.AdjustedP:0.000}")).ToList();
            EnrichmentMessage = _enrichment.Message;
            if (_comparison is not null)
            {
                ClassChanges = Enrichment.ClassChanges(_comparison.Features, Data.Transformed.Features);
                ClassChangeBars = ClassChanges.Select(c => new RankItem($"{c.Class} ({c.Members})", c.MeanLog2FoldChange, c.Class, c.Class, null, $"{c.Up} up, {c.Down} down · p {c.P:0.000}")).OrderByDescending(r => r.Value).ToList();
                ChainMapClasses.Clear();
                ChainMapClasses.Add("All classes");
                foreach (var c in ClassChanges.Select(c => c.Class)) ChainMapClasses.Add(c);
                BuildChainMap();
            }
        }
        catch (Exception ex)
        {
            EnrichmentMessage = "The enrichment failed: " + ex.Message;
        }
    }

    partial void OnChainMapClassChanged(string value) => BuildChainMap();

    /// <summary>Total carbons down, double bonds across, the mean log2 fold change in each cell.</summary>
    private void BuildChainMap()
    {
        if (Data is null || _comparison is null) { ChainMap = null; return; }
        var cells = Enrichment.ChainMap(_comparison.Features, Data.Transformed.Features, ChainMapClass == "All classes" ? null : ChainMapClass);
        if (cells.Count == 0) { ChainMap = null; return; }
        var carbons = cells.Select(c => c.Carbons).Distinct().OrderBy(c => c).ToList();
        var bonds = cells.Select(c => c.DoubleBonds).Distinct().OrderBy(b => b).ToList();
        var values = new double[carbons.Count, bonds.Count];
        for (var r = 0; r < carbons.Count; r++) for (var c = 0; c < bonds.Count; c++) values[r, c] = double.NaN;
        foreach (var cell in cells) values[carbons.IndexOf(cell.Carbons), bonds.IndexOf(cell.DoubleBonds)] = cell.MeanLog2FoldChange;
        ChainMap = new HeatmapData(carbons.Select(c => $"C{c}").ToList(), bonds.Select(b => $"{b} DB").ToList(), values, null, null, null, null, $"mean log2 fold change, {ClassA} / {ClassB}");
    }

    // ---------------------------------------------------------------- export

    /// <summary>Writes the table behind the page as tab-separated text.</summary>
    [RelayCommand]
    private async Task ExportTable(string? which)
    {
        var (name, header, rows) = which switch
        {
            "comparison" => ("comparison", new[] { "Feature id", "Feature", "Class", $"Mean {ClassA}", $"Mean {ClassB}", "Fold change", "log2 FC", "Statistic", "p", Univariate.AdjustName(AdjustmentKind) },
                ComparisonRows.Select(r => new object[] { r.FeatureId, r.Label, r.Group, r.MeanA, r.MeanB, r.FoldChange, r.Log2FoldChange, r.Statistic, r.P, r.AdjustedP })),
            "anova" => ("anova", new[] { "Feature id", "Feature", "Class", "Statistic", "p", Univariate.AdjustName(AdjustmentKind), "Post hoc" },
                AnovaRows.Select(r => new object[] { r.FeatureId, r.Label, r.Group, r.Statistic, r.P, r.AdjustedP, string.Join("; ", r.PostHoc.Select(p => $"{p.ClassA}-{p.ClassB} p={p.P:0.0000}")) })),
            "pattern" => ("pattern-search", new[] { "Feature id", "Feature", "Class", "Correlation", "p" },
                PatternRows.Select(r => new object[] { r.FeatureId, r.Label, r.Group, r.Correlation, r.P })),
            "forest" => ("random-forest", new[] { "Feature id", "Feature", "Class", "Mean decrease accuracy", "Mean decrease Gini" },
                ImportanceRows.Select(r => new object[] { r.FeatureId, r.Label, r.Group, r.MeanDecreaseAccuracy, r.MeanDecreaseGini })),
            "enrichment" => ("enrichment", new[] { "Set", "Kind", "Size", "Hits", "Expected", "Enrichment ratio", "p", Univariate.AdjustName(AdjustmentKind), "Members" },
                EnrichmentRows.Select(r => new object[] { r.Set, r.Kind, r.SetSize, r.Hits, r.Expected, r.EnrichmentRatio, r.P, r.AdjustedP, string.Join("; ", r.Members) })),
            _ => ("normalized-data", DataHeader(), DataRows()),
        };
        var path = await _dialogs.SaveFileAsync("Export the table", name + ".tsv", "tsv");
        if (path is null) return;
        var sb = new StringBuilder();
        sb.AppendLine(string.Join('\t', header));
        foreach (var row in rows) sb.AppendLine(string.Join('\t', row.Select(Cell)));
        await File.WriteAllTextAsync(path, sb.ToString(), new UTF8Encoding(false));
        DataSummary = $"Written to {path}";
    }

    private string[] DataHeader() => Data is null ? Array.Empty<string>() : new[] { "Feature id", "Feature", "Class" }.Concat(Data.Transformed.Samples.Select(s => s.FileName)).ToArray();

    private IEnumerable<object[]> DataRows()
    {
        if (Data is null) yield break;
        var t = Data.Transformed;
        for (var j = 0; j < t.FeatureCount; j++)
        {
            var f = t.Features[j];
            yield return new object[] { f.Id, AnalysisTable.LabelOf(f), f.Ontology }.Concat(t.Column(j).Cast<object>()).ToArray();
        }
    }

    private static string Cell(object v) => v switch
    {
        double d => double.IsNaN(d) ? string.Empty : d.ToString("G8", CultureInfo.InvariantCulture),
        int i => i.ToString(CultureInfo.InvariantCulture),
        null => string.Empty,
        _ => v.ToString()!.Replace('\t', ' '),
    };
}
