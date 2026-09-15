using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDIAL.Desktop.Controls;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Pipeline.Curation;
using OpenDIAL.Pipeline.Results;
using OpenDIAL.Pipeline.Statistics;

namespace OpenDIAL.Desktop.ViewModels;

/// <summary>One row of the variance table beside the score plot.</summary>
public sealed record VarianceRow(string Component, double Percent, double Cumulative);

/// <summary>A feature's standing in the orthogonal model: how much of the separation it carries, and how reliably.</summary>
public sealed record SPlotRow(int FeatureId, string Label, string Group, double Covariance, double Correlation, double Vip, string Side)
{
    /// <summary>Far out on both axes is the only place a feature is worth believing.</summary>
    public bool Reliable => Math.Abs(Correlation) >= 0.7 && Vip >= 1.0;
}

/// <summary>A feature's standing in the discriminant model, for the table under the score plot.</summary>
public sealed record VipRow(int FeatureId, string Label, string Group, double Vip, double Weight)
{
    /// <summary>Which class the feature goes with, read off the sign of the first-component weight.</summary>
    public string Side { get; init; } = string.Empty;
    public bool Matters => Vip >= 1.0;
}

/// <summary>
/// The dataset seen whole rather than one feature at a time. The one-factor analysis in
/// <see cref="Analysis"/> builds the dataset — which features, as what numbers, preprocessed how —
/// and every model here reads it: the principal components, the discriminant and orthogonal
/// models, the clustering. The drift correction and the molecular network keep their own inputs.
/// </summary>
public sealed partial class StatisticsViewModel : ViewModelBase
{
    private readonly IFileDialogService _dialogs;
    private IReadOnlyList<AlignmentSpotRow> _spots = Array.Empty<AlignmentSpotRow>();
    private IReadOnlyList<SampleInfo> _samples = Array.Empty<SampleInfo>();
    private ResultSession? _session;
    private SpectralNetworkResult? _network;
    private BatchCorrectionResult? _corrected;
    private double[,]? _correctedRaw;
    private IReadOnlyList<AlignmentSpotRow> _correctedFeatures = Array.Empty<AlignmentSpotRow>();
    private Action<int>? _requestShowFeature;

    public StatisticsViewModel(IFileDialogService dialogs)
    {
        _dialogs = dialogs;
        Analysis = new OneFactorViewModel(dialogs);
        Pathways = new PathwaysViewModel(Analysis, dialogs);
        TwoFactor = new TwoFactorViewModel(Analysis, dialogs);
        Analysis.DataChanged += (_, _) => Compute();
    }

    /// <summary>The one-factor analysis: the source of the dataset every model here reads.</summary>
    public OneFactorViewModel Analysis { get; }

    /// <summary>The pathway analysis after BioPAN, on the same dataset and the same two classes.</summary>
    public PathwaysViewModel Pathways { get; }

    /// <summary>Two factors at once: the two-way ANOVA per feature and ASCA over the matrix.</summary>
    public TwoFactorViewModel TwoFactor { get; }

    public static string[] ClusterDistances { get; } = OneFactorViewModel.Distances;
    public static string[] ClusterLinkages { get; } = OneFactorViewModel.Linkages;

    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private string _summary = "No results loaded. Process the batch or open a project.";
    [ObservableProperty] private bool _isBusy;

    // principal components
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _scores = Array.Empty<ScatterPoint>();
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _loadings = Array.Empty<ScatterPoint>();
    [ObservableProperty] private ScatterPoint? _selectedLoading;
    [ObservableProperty] private IReadOnlyList<VarianceRow> _variance = Array.Empty<VarianceRow>();
    [ObservableProperty] private IReadOnlyList<BarItem> _scree = Array.Empty<BarItem>();
    [ObservableProperty] private string _scoreLabel = "PC1 against PC2";
    [ObservableProperty] private string _pcX = "PC1";
    [ObservableProperty] private string _pcY = "PC2";
    public static string[] ComponentNames { get; } = { "PC1", "PC2", "PC3", "PC4", "PC5" };
    private PcaResult? _pca;

    // clustering
    [ObservableProperty] private ClusterNode? _clusterRoot;
    [ObservableProperty] private string _clusterLabel = string.Empty;
    [ObservableProperty] private string _clusterDistance = "Pearson";
    [ObservableProperty] private string _clusterLinkage = "Average";

    // supervised discriminant model
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _plsScores = Array.Empty<ScatterPoint>();
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _plsLoadings = Array.Empty<ScatterPoint>();
    [ObservableProperty] private IReadOnlyList<VipRow> _vipRows = Array.Empty<VipRow>();
    [ObservableProperty] private IReadOnlyList<RankItem> _vipBars = Array.Empty<RankItem>();
    [ObservableProperty] private IReadOnlyList<string> _plsClassNames = Array.Empty<string>();
    [ObservableProperty] private object? _vipSelectedTag;
    [ObservableProperty] private VipRow? _selectedVipRow;
    [ObservableProperty] private string _plsComponents = "2";
    [ObservableProperty] private string _plsPermutations = "200";
    [ObservableProperty] private string _plsScoreLabel = "Component 1 against component 2";
    [ObservableProperty] private string _plsVerdict = "Not fitted yet.";
    [ObservableProperty] private string _plsQuality = string.Empty;
    [ObservableProperty] private bool _plsTrustworthy;
    [ObservableProperty] private bool _isFittingPls;
    [ObservableProperty] private bool _hasPls;

    // orthogonal discriminant model
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _oplsScores = Array.Empty<ScatterPoint>();
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _sPlot = Array.Empty<ScatterPoint>();
    [ObservableProperty] private ScatterPoint? _selectedSPlotPoint;
    [ObservableProperty] private IReadOnlyList<SPlotRow> _sPlotRows = Array.Empty<SPlotRow>();
    [ObservableProperty] private SPlotRow? _selectedSPlotRow;
    [ObservableProperty] private string _orthogonalComponents = "1";
    [ObservableProperty] private string _oplsPermutations = "200";
    [ObservableProperty] private string _oplsScoreLabel = "Predictive against the first orthogonal component";
    [ObservableProperty] private string _oplsVerdict = "Not fitted yet.";
    [ObservableProperty] private string _oplsQuality = string.Empty;
    [ObservableProperty] private bool _isFittingOpls;
    [ObservableProperty] private bool _hasOpls;

    // drift and batch correction
    [ObservableProperty] private IReadOnlyList<FeatureCorrection> _correctionRows = Array.Empty<FeatureCorrection>();
    [ObservableProperty] private FeatureCorrection? _selectedCorrectionRow;
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _driftBefore = Array.Empty<ScatterPoint>();
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _driftAfter = Array.Empty<ScatterPoint>();
    [ObservableProperty] private string _correctionSpan = "0.75";
    [ObservableProperty] private string _correctionVerdict = "Not corrected yet.";
    [ObservableProperty] private string _correctionDetail = string.Empty;
    [ObservableProperty] private bool _isCorrecting;
    [ObservableProperty] private bool _hasCorrection;
    [ObservableProperty] private bool _useCorrectedValues;

    // molecular network
    [ObservableProperty] private SpectralNetworkResult? _networkResult;
    [ObservableProperty] private string _networkCutoff = "0.7";
    [ObservableProperty] private string _networkTolerance = "0.05";
    [ObservableProperty] private int _selectedNetworkFeature = -1;
    [ObservableProperty] private string _networkLabel = "Not built yet.";
    [ObservableProperty] private bool _isBuildingNetwork;
    [ObservableProperty] private bool _showUnconnected;

    /// <summary>Raised when a feature is chosen anywhere on the page, so the shell can show it in the ion table.</summary>
    public Action<int>? RequestShowFeature
    {
        get => _requestShowFeature;
        set { _requestShowFeature = value; Analysis.RequestShowFeature = value; }
    }

    public void Clear()
    {
        _session = null;
        _spots = Array.Empty<AlignmentSpotRow>();
        _samples = Array.Empty<SampleInfo>();
        _network = null;
        _pca = null;
        Analysis.Clear();
        Pathways.Clear();
        TwoFactor.Clear();
        HasResults = false;
        Scores = Array.Empty<ScatterPoint>();
        Loadings = Array.Empty<ScatterPoint>();
        Variance = Array.Empty<VarianceRow>();
        Scree = Array.Empty<BarItem>();
        ClusterRoot = null;
        PlsScores = Array.Empty<ScatterPoint>();
        PlsLoadings = Array.Empty<ScatterPoint>();
        VipRows = Array.Empty<VipRow>();
        VipBars = Array.Empty<RankItem>();
        HasPls = false;
        PlsVerdict = "Not fitted yet.";
        PlsQuality = string.Empty;
        OplsScores = Array.Empty<ScatterPoint>();
        SPlot = Array.Empty<ScatterPoint>();
        SPlotRows = Array.Empty<SPlotRow>();
        HasOpls = false;
        OplsVerdict = "Not fitted yet.";
        OplsQuality = string.Empty;
        CorrectionRows = Array.Empty<FeatureCorrection>();
        DriftBefore = Array.Empty<ScatterPoint>();
        DriftAfter = Array.Empty<ScatterPoint>();
        HasCorrection = false;
        UseCorrectedValues = false;
        _corrected = null;
        _correctedRaw = null;
        CorrectionVerdict = "Not corrected yet.";
        CorrectionDetail = string.Empty;
        NetworkResult = null;
        NetworkLabel = "Not built yet.";
        Summary = "No results loaded. Process the batch or open a project.";
    }

    public void Load(ResultSession session, IReadOnlyList<AlignmentSpotRow> spots, IReadOnlyList<SampleInfo> samples, CurationStore? curation = null, string? note = null)
    {
        _session = session;
        _spots = spots;
        _samples = samples;
        HasResults = spots.Count > 0 && samples.Count > 1;
        Summary = HasResults
            ? note ?? $"{spots.Count} features across {samples.Count} injections."
            : samples.Count <= 1 ? "Multivariate views need more than one injection." : "No aligned features.";
        if (!HasResults) return;
        // loading the analysis builds the dataset and raises DataChanged, which computes the rest
        Analysis.Load(spots, samples, curation, UseCorrectedValues ? CorrectTable : null);
    }

    partial void OnSelectedNetworkFeatureChanged(int value)
    {
        if (value >= 0) RequestShowFeature?.Invoke(value);
    }

    partial void OnClusterDistanceChanged(string value) => Compute();
    partial void OnClusterLinkageChanged(string value) => Compute();
    partial void OnPcXChanged(string value) => ProjectPca();
    partial void OnPcYChanged(string value) => ProjectPca();

    /// <summary>The matrix the models read: the analysis dataset, scaled as the Data page says.</summary>
    private DataMatrix? Matrix => Analysis.Data?.Scaled;

    /// <summary>Recomputes the components and the clustering; both are cheap next to reading the data.</summary>
    [RelayCommand]
    private void Compute()
    {
        var matrix = Matrix;
        if (!HasResults || matrix is null) return;
        if (matrix.FeatureCount < 2 || matrix.SampleCount < 2)
        {
            Summary = "Not enough features for a multivariate view.";
            return;
        }
        IsBusy = true;
        try
        {
            _pca = Pca.Compute(matrix, Math.Min(5, Math.Max(2, matrix.SampleCount - 1)));
            ProjectPca();
            var cumulative = 0.0;
            Variance = _pca.ExplainedVariance
                .Select((v, i) => { cumulative += v; return new VarianceRow($"PC{i + 1}", v, cumulative); })
                .ToList();
            Scree = _pca.ExplainedVariance.Select((v, i) => new BarItem($"PC{i + 1}", v, "explained")).ToList();

            var distance = ClusterDistance switch { "Euclidean" => DistanceKind.Euclidean, "Spearman" => DistanceKind.Spearman, "Manhattan" => DistanceKind.Manhattan, _ => DistanceKind.Pearson };
            var linkage = ClusterLinkage switch { "Complete" => LinkageKind.Complete, "Single" => LinkageKind.Single, "Ward" => LinkageKind.Ward, _ => LinkageKind.Average };
            var n = matrix.SampleCount;
            var d = new double[n, n];
            for (var a = 0; a < n; a++)
                for (var b = a + 1; b < n; b++)
                    d[a, b] = d[b, a] = Clustering.Distance(Row(matrix.Values, a), Row(matrix.Values, b), distance);
            ClusterRoot = Clustering.Cluster(d, matrix.Samples.Select(s => s.FileName).ToList(), matrix.Samples.Select(AnalysisTable.ClassOf).ToList(), linkage);
            ClusterLabel = $"{n} injections, {ClusterLinkage.ToLowerInvariant()} linkage on the {ClusterDistance.ToLowerInvariant()} distance over {matrix.FeatureCount} features";
            Summary = $"{matrix.FeatureCount} features across {n} injections · {Analysis.Data!.Raw.ValueName} · {Analysis.DataSummary}";
        }
        catch (Exception ex)
        {
            Summary = "The multivariate view failed: " + ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>The scores and loadings on the two components chosen.</summary>
    private void ProjectPca()
    {
        if (_pca is null) return;
        var x = Math.Clamp(Array.IndexOf(ComponentNames, PcX), 0, Math.Max(0, _pca.ComponentCount - 1));
        var y = Math.Clamp(Array.IndexOf(ComponentNames, PcY), 0, Math.Max(0, _pca.ComponentCount - 1));
        double At(double[] c, int k) => k < c.Length ? c[k] : 0;
        Scores = _pca.Scores.Select(s => new ScatterPoint(At(s.Components, x), At(s.Components, y), s.Sample, s.Class, s) { Labelled = true }).ToList();
        var loadings = _pca.Loadings.Select(l => new ScatterPoint(At(l.Components, x), At(l.Components, y), l.Label, l.Group, l)).ToList();
        // the ten features farthest from the centre are named; they are what drives the separation
        var far = loadings.OrderByDescending(p => p.X * p.X + p.Y * p.Y).Take(10).ToHashSet();
        Loadings = loadings.Select(p => far.Contains(p) ? p with { Labelled = true } : p).ToList();
        ScoreLabel = _pca.ExplainedVariance.Count > Math.Max(x, y)
            ? $"{PcX} ({_pca.ExplainedVariance[x]:F1} %) against {PcY} ({_pca.ExplainedVariance[y]:F1} %)"
            : $"{PcX} against {PcY}";
    }

    partial void OnSelectedLoadingChanged(ScatterPoint? value)
    {
        if (value?.Tag is PcaLoading l) RequestShowFeature?.Invoke(l.FeatureId);
    }

    private static double[] Row(double[,] m, int r)
    {
        var row = new double[m.GetLength(1)];
        for (var c = 0; c < row.Length; c++) row[c] = m[r, c];
        return row;
    }

    partial void OnUseCorrectedValuesChanged(bool value) => Analysis.Corrector = value ? CorrectTable : null;

    partial void OnSelectedCorrectionRowChanged(FeatureCorrection? value) => ShowDrift(value);

    partial void OnSelectedVipRowChanged(VipRow? value)
    {
        if (value is not null) RequestShowFeature?.Invoke(value.FeatureId);
    }

    partial void OnVipSelectedTagChanged(object? value)
    {
        if (value is int id) RequestShowFeature?.Invoke(id);
    }

    /// <summary>
    /// The source table with the drift-corrected responses put in wherever the correction covers
    /// the feature; a feature it did not cover keeps its raw values.
    /// </summary>
    private AnalysisTable CorrectTable(AnalysisTable table)
    {
        if (_correctedRaw is null || _correctedFeatures.Count == 0) return table;
        var column = new Dictionary<int, int>(_correctedFeatures.Count);
        for (var j = 0; j < _correctedFeatures.Count; j++) column[_correctedFeatures[j].Id] = j;
        var byFile = new Dictionary<int, int>();
        for (var i = 0; i < _samples.Count; i++) byFile[_samples[i].FileId] = i;
        var values = (double[,])table.Values.Clone();
        for (var j = 0; j < table.FeatureCount; j++)
        {
            if (!column.TryGetValue(table.Features[j].Id, out var source)) continue;
            for (var i = 0; i < table.SampleCount; i++)
            {
                if (!byFile.TryGetValue(table.Samples[i].FileId, out var row)) continue;
                var v = _correctedRaw[row, source];
                values[i, j] = v > 0 ? v : double.NaN;
            }
        }
        return table.With(values, table.ValueName + ", drift corrected");
    }

    /// <summary>
    /// Fits the discriminant model against the declared classes, then does its best to talk the
    /// reviewer out of it: cross-validation and a permutation test run with the fit, and the verdict
    /// beside the plot is theirs, not the fit's.
    /// </summary>
    [RelayCommand]
    private async Task FitDiscriminant()
    {
        var matrix = Matrix;
        if (!HasResults || matrix is null) return;
        if (matrix.FeatureCount < 2)
        {
            PlsVerdict = "Not enough features to fit anything.";
            HasPls = false;
            return;
        }
        var components = int.TryParse(PlsComponents?.Trim(), out var c) ? Math.Clamp(c, 1, 5) : 2;
        var permutations = int.TryParse(PlsPermutations?.Trim(), out var k) ? Math.Clamp(k, 0, 2000) : 200;
        IsFittingPls = true;
        PlsVerdict = "Fitting, cross-validating and permuting…";
        try
        {
            var transformed = Analysis.Data?.Transformed;
            var result = await Task.Run(() => PartialLeastSquares.Compute(matrix, components, permutations));

            PlsScores = result.Scores
                .Select(s => new ScatterPoint(s.Components[0], s.Components.Length > 1 ? s.Components[1] : 0, s.Sample, s.Class, s) { Labelled = true })
                .ToList();
            PlsLoadings = result.Loadings
                .Select(l => new ScatterPoint(l.Weights[0], l.Weights.Length > 1 ? l.Weights[1] : 0, l.Label, l.Group, l))
                .ToList();
            // The sign of a weight only names a class when there are two of them, and which sign goes
            // with which class is not fixed: the fit is free to come out either way round. Read it
            // off the scores, where the class sitting on the positive side is the one a
            // positively weighted feature is higher in.
            var twoSided = result.Classes.Count == 2;
            var first = string.Empty;
            var second = string.Empty;
            if (twoSided)
            {
                var meansByClass = result.Scores
                    .GroupBy(s => s.Class)
                    .ToDictionary(g => g.Key, g => g.Average(s => s.Components[0]));
                var ordered = result.Classes.OrderBy(cl => meansByClass.TryGetValue(cl, out var m) ? m : 0).ToList();
                first = ordered[0];    // the negative side
                second = ordered[1];   // the positive one
            }
            VipRows = result.Loadings
                .OrderByDescending(l => l.Vip)
                .Take(300)
                .Select(l => new VipRow(l.FeatureId, l.Label, l.Group, l.Vip, l.Weights[0])
                {
                    Side = twoSided ? (l.Weights[0] < 0 ? first : second) : string.Empty,
                })
                .ToList();
            PlsClassNames = result.Classes;
            var groups = transformed is null ? null : Univariate.GroupIndices(transformed);
            VipBars = VipRows.Take(25).Select(r =>
            {
                IReadOnlyList<double>? levels = null;
                if (transformed is not null && groups is not null)
                {
                    var j = transformed.Features.ToList().FindIndex(f => f.Id == r.FeatureId);
                    if (j >= 0) levels = result.Classes.Select(cl => groups.TryGetValue(cl, out var idx) ? idx.Select(i => transformed.Values[i, j]).Where(v => !double.IsNaN(v)).DefaultIfEmpty(0).Average() : 0).ToList();
                }
                return new RankItem(r.Label, r.Vip, r.Group, r.FeatureId, levels, twoSided ? $"higher in {r.Side}" : null);
            }).ToList();

            HasPls = result.Scores.Count > 0;
            PlsScoreLabel = result.ExplainedX.Count > 1
                ? $"Component 1 ({result.ExplainedX[0]:F1} %) against component 2 ({result.ExplainedX[1]:F1} %)"
                : "Component 1";
            if (!HasPls)
            {
                PlsVerdict = result.Message;
                PlsQuality = string.Empty;
                PlsTrustworthy = false;
                return;
            }

            // Q squared is the number that decides it: the fit itself will separate noise.
            PlsTrustworthy = result.Q2 >= 0.4 && result.PermutationP <= 0.05;
            PlsQuality = $"R²Y {result.R2Y:F2} · Q² {result.Q2:F2} · {result.CorrectlyClassified} of {result.Scores.Count} injections classified back correctly"
                + (result.Permutations > 0 ? $" · permutation p {result.PermutationP:F3} over {result.Permutations} shuffles" : string.Empty);
            PlsVerdict = PlsTrustworthy
                ? $"The separation between {string.Join(" and ", result.Classes)} survives cross-validation."
                : result.Q2 < 0.4
                    ? "The model does not survive cross-validation: it separates these injections but would not separate new ones. Read it as a picture of this batch, not as a finding."
                    : "Chance did about as well on shuffled labels. Treat the separation as unproven.";
        }
        catch (Exception ex)
        {
            HasPls = false;
            PlsVerdict = "The discriminant model failed: " + ex.Message;
        }
        finally
        {
            IsFittingPls = false;
        }
    }

    partial void OnSelectedSPlotRowChanged(SPlotRow? value)
    {
        if (value is not null) RequestShowFeature?.Invoke(value.FeatureId);
    }

    partial void OnSelectedSPlotPointChanged(ScatterPoint? value)
    {
        if (value?.Tag is OplsLoading loading) RequestShowFeature?.Invoke(loading.FeatureId);
    }

    /// <summary>
    /// Fits the orthogonal model: the same separation the plain discriminant finds, rotated so that
    /// it sits on one component and everything else sits beside it. Easier to read, no more true —
    /// the same cross-validation and permutation test decide whether to believe it.
    /// </summary>
    [RelayCommand]
    private async Task FitOrthogonal()
    {
        var matrix = Matrix;
        if (!HasResults || matrix is null) return;
        if (matrix.FeatureCount < 2)
        {
            OplsVerdict = "Not enough features to fit anything.";
            HasOpls = false;
            return;
        }
        var orthogonal = int.TryParse(OrthogonalComponents?.Trim(), out var o) ? Math.Clamp(o, 0, 5) : 1;
        var permutations = int.TryParse(OplsPermutations?.Trim(), out var k) ? Math.Clamp(k, 0, 2000) : 200;
        IsFittingOpls = true;
        OplsVerdict = "Stripping the orthogonal variation, then cross-validating…";
        try
        {
            var result = await Task.Run(() => OrthogonalProjection.Compute(matrix, orthogonal, permutations));

            HasOpls = result.Scores.Count > 0;
            if (!HasOpls)
            {
                OplsScores = Array.Empty<ScatterPoint>();
                SPlot = Array.Empty<ScatterPoint>();
                SPlotRows = Array.Empty<SPlotRow>();
                OplsVerdict = result.Message;
                OplsQuality = string.Empty;
                return;
            }

            OplsScores = result.Scores
                .Select(s => new ScatterPoint(s.Predictive, s.Orthogonal, s.Sample, s.Class, s) { Labelled = true })
                .ToList();
            var corners = result.Loadings.OrderByDescending(l => Math.Abs(l.Covariance) * Math.Abs(l.Correlation)).Take(8).Select(l => l.FeatureId).ToHashSet();
            SPlot = result.Loadings
                .Select(l => new ScatterPoint(l.Covariance, l.Correlation, l.Label, string.IsNullOrEmpty(l.Group) ? "unknown" : l.Group, l) { Labelled = corners.Contains(l.FeatureId) })
                .ToList();
            SPlotRows = result.Loadings
                .OrderByDescending(l => Math.Abs(l.Covariance))
                .Take(300)
                .Select(l => new SPlotRow(l.FeatureId, l.Label, l.Group, l.Covariance, l.Correlation, l.Vip, l.Side))
                .ToList();

            OplsScoreLabel = $"Predictive ({result.PredictiveVarianceX:F1} % of the features) against the first of {result.OrthogonalComponents} orthogonal component(s) ({result.OrthogonalVarianceX:F1} % stripped out)";
            OplsQuality = $"R²Y {result.R2Y:F2} · Q² {result.Q2:F2} · {result.CorrectlyClassified} of {result.Scores.Count} injections classified back correctly"
                + (result.Permutations > 0 ? $" · permutation p {result.PermutationP:F3} over {result.Permutations} shuffles" : string.Empty);
            OplsVerdict = result.Q2 >= 0.4 && result.PermutationP <= 0.05
                ? $"The separation between {string.Join(" and ", result.Classes)} survives cross-validation. The rotation only made it easier to read."
                : result.Q2 < 0.4
                    ? "The rotation is not evidence. This model does not survive cross-validation, so read the plot as a picture of this batch and nothing more."
                    : "Chance did about as well on shuffled labels. Treat the separation as unproven.";
        }
        catch (Exception ex)
        {
            HasOpls = false;
            OplsVerdict = "The orthogonal model failed: " + ex.Message;
        }
        finally
        {
            IsFittingOpls = false;
        }
    }

    /// <summary>
    /// Fits the drift out of every feature against the quality controls, and reports what it did to
    /// their spread. Nothing is written to the result: the corrected values live here and feed the
    /// analysis only while the box is ticked.
    /// </summary>
    [RelayCommand]
    private async Task ApplyCorrection()
    {
        if (!HasResults) return;
        var span = double.TryParse(CorrectionSpan?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)
            ? Math.Clamp(s, 0.2, 1.0)
            : 0.75;
        var features = _spots.ToList();
        var useArea = Analysis.UseArea;
        IsCorrecting = true;
        CorrectionVerdict = $"Correcting {features.Count} features…";
        try
        {
            var result = await Task.Run(() => BatchCorrection.Apply(features, _samples, useArea, span));
            _corrected = result;
            _correctedFeatures = features;
            _correctedRaw = result.Corrected > 0 ? result.Values : null;
            CorrectionRows = result.Features.OrderByDescending(f => f.CvBefore).ToList();
            HasCorrection = result.Corrected > 0;
            CorrectionVerdict = result.Message;
            CorrectionDetail = result.QualityControls == 0
                ? "No injection is marked QC. Mark them in the Samples workspace; without them there is nothing that says what is drift and what is biology."
                : $"{result.QualityControls} quality control(s) across {result.Batches} batch(es) · median CV over the controls {result.MedianCvBefore:F1} % before, {result.MedianCvAfter:F1} % after · {result.Corrected} feature(s) corrected, {result.Skipped} left alone.";
            SelectedCorrectionRow = CorrectionRows.FirstOrDefault(f => f.Corrected) ?? CorrectionRows.FirstOrDefault();
            if (!HasCorrection) UseCorrectedValues = false;
            else if (UseCorrectedValues) Analysis.Corrector = CorrectTable;
        }
        catch (Exception ex)
        {
            HasCorrection = false;
            CorrectionVerdict = "The correction failed: " + ex.Message;
        }
        finally
        {
            IsCorrecting = false;
        }
    }

    /// <summary>Draws one feature's response across the run, before and after, controls apart from samples.</summary>
    private void ShowDrift(FeatureCorrection? row)
    {
        if (row is null || _corrected is null || _correctedFeatures.Count == 0)
        {
            DriftBefore = Array.Empty<ScatterPoint>();
            DriftAfter = Array.Empty<ScatterPoint>();
            return;
        }
        var column = _correctedFeatures.ToList().FindIndex(f => f.Id == row.FeatureId);
        if (column < 0) return;

        var useArea = Analysis.UseArea;
        var before = new List<ScatterPoint>(_samples.Count);
        var after = new List<ScatterPoint>(_samples.Count);
        var feature = _correctedFeatures[column];
        for (var i = 0; i < _samples.Count; i++)
        {
            var sample = _samples[i];
            var order = sample.InjectionOrder > 0 ? sample.InjectionOrder : i + 1;
            var group = sample.IsQualityControl ? "QC" : sample.IsBlank ? "Blank" : $"Batch {sample.Batch}";
            var peak = feature.SamplePeaks.FirstOrDefault(x => x.FileId == sample.FileId);
            var raw = peak is null ? 0 : useArea ? peak.Area : peak.Height;
            before.Add(new ScatterPoint(order, raw, sample.FileName, group));
            after.Add(new ScatterPoint(order, _corrected.Values[i, column], sample.FileName, group));
        }
        DriftBefore = before.OrderBy(p => p.X).ToList();
        DriftAfter = after.OrderBy(p => p.X).ToList();
    }

    /// <summary>
    /// Builds the molecular network from the deconvoluted product spectra. Reading them is the slow
    /// part, so it is a separate action rather than something that happens on every option change.
    /// </summary>
    [RelayCommand]
    private async Task BuildNetwork()
    {
        if (!HasResults || _session?.AlignmentFile is null) return;
        var cutoff = double.TryParse(NetworkCutoff?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var c) ? c : 0.7;
        var tolerance = double.TryParse(NetworkTolerance?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var t) ? t : 0.05;
        var features = (Analysis.AnnotatedOnly ? _spots.Where(s => s.IsAnnotated) : _spots).Where(s => s.MsmsAssigned).ToList();
        if (features.Count == 0)
        {
            NetworkLabel = "No feature in this result carries a product spectrum.";
            NetworkResult = null;
            return;
        }
        IsBuildingNetwork = true;
        NetworkLabel = $"Reading {features.Count} product spectra…";
        try
        {
            var alignment = _session.AlignmentFile;
            var spectra = await Task.Run(() =>
            {
                var map = new Dictionary<int, IReadOnlyList<SpectrumPeakPoint>>();
                foreach (var f in features)
                {
                    var dec = ResultLoader.LoadAlignmentMsDec(alignment, f.Id);
                    if (dec?.Spectrum is { Count: > 0 })
                    {
                        map[f.Id] = dec.Spectrum.Select(p => new SpectrumPeakPoint(p.Mass, p.Intensity)).ToList();
                    }
                }
                return map;
            });
            _network = await Task.Run(() => SpectralNetwork.Build(features, spectra, cutoff, tolerance, includeUnconnected: ShowUnconnected));
            NetworkResult = _network;
            NetworkLabel = ShowUnconnected
                ? $"{_network.Considered} feature(s) with MS/MS, {_network.Edges.Count} edge(s) at or above {cutoff:F2}."
                : $"{_network.Nodes.Count} of {_network.Considered} feature(s) sit in a cluster, joined by {_network.Edges.Count} edge(s) at or above {cutoff:F2}; {_network.Unconnected} joined to nothing and are not drawn.";
        }
        catch (Exception ex)
        {
            NetworkLabel = "The network could not be built: " + ex.Message;
        }
        finally
        {
            IsBuildingNetwork = false;
        }
    }

    /// <summary>Writes the node and edge tables Cytoscape reads.</summary>
    [RelayCommand]
    private async Task ExportNetwork()
    {
        if (_network is null) { NetworkLabel = "Build the network first."; return; }
        var path = await _dialogs.SaveFileAsync("Export the network edges", "network_edges.txt", "txt", _session?.Folder);
        if (path is null) return;
        var nodePath = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty,
            Path.GetFileNameWithoutExtension(path) + "_nodes" + Path.GetExtension(path));
        try
        {
            await SpectralNetwork.ExportAsync(path, nodePath, _network);
            NetworkLabel = $"Edges written to {Path.GetFileName(path)}, nodes to {Path.GetFileName(nodePath)}.";
        }
        catch (Exception ex)
        {
            NetworkLabel = "Export failed: " + ex.Message;
        }
    }
}
