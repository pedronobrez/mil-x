using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDIAL.Desktop.Controls;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Pipeline.Results;
using OpenDIAL.Pipeline.Statistics;

namespace OpenDIAL.Desktop.ViewModels;

/// <summary>One row of the variance table beside the score plot.</summary>
public sealed record VarianceRow(string Component, double Percent, double Cumulative);

/// <summary>A feature's standing in the discriminant model, for the table under the score plot.</summary>
public sealed record VipRow(int FeatureId, string Label, string Group, double Vip, double Weight)
{
    /// <summary>Which class the feature goes with, read off the sign of the first-component weight.</summary>
    public string Side { get; init; } = string.Empty;
    public bool Matters => Vip >= 1.0;
}

/// <summary>
/// The dataset seen whole rather than one feature at a time: where the injections fall against each
/// other, how they group, and which features fragment alike.
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

    public StatisticsViewModel(IFileDialogService dialogs)
    {
        _dialogs = dialogs;
    }

    public static string[] ValueKinds { get; } = { "Height", "Area" };
    public static string[] Transforms { get; } = { "Log10", "None" };
    public static string[] Scalings { get; } = { "Auto (unit variance)", "Pareto", "None (centre only)" };

    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private string _summary = "No results loaded. Process the batch or open a project.";
    [ObservableProperty] private string _valueKind = "Height";
    [ObservableProperty] private string _transform = "Log10";
    [ObservableProperty] private string _scaling = "Auto (unit variance)";
    [ObservableProperty] private bool _annotatedOnly;
    [ObservableProperty] private bool _isBusy;

    // principal components
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _scores = Array.Empty<ScatterPoint>();
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _loadings = Array.Empty<ScatterPoint>();
    [ObservableProperty] private IReadOnlyList<VarianceRow> _variance = Array.Empty<VarianceRow>();
    [ObservableProperty] private string _scoreLabel = "PC1 against PC2";

    // clustering
    [ObservableProperty] private ClusterNode? _clusterRoot;
    [ObservableProperty] private string _clusterLabel = string.Empty;

    // supervised discriminant model
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _plsScores = Array.Empty<ScatterPoint>();
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _plsLoadings = Array.Empty<ScatterPoint>();
    [ObservableProperty] private IReadOnlyList<VipRow> _vipRows = Array.Empty<VipRow>();
    [ObservableProperty] private VipRow? _selectedVipRow;
    [ObservableProperty] private string _plsComponents = "2";
    [ObservableProperty] private string _plsPermutations = "200";
    [ObservableProperty] private string _plsScoreLabel = "Component 1 against component 2";
    [ObservableProperty] private string _plsVerdict = "Not fitted yet.";
    [ObservableProperty] private string _plsQuality = string.Empty;
    [ObservableProperty] private bool _plsTrustworthy;
    [ObservableProperty] private bool _isFittingPls;
    [ObservableProperty] private bool _hasPls;

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

    /// <summary>Raised when a network node is chosen, so the shell can show it in the ion table.</summary>
    public Action<int>? RequestShowFeature { get; set; }

    public void Clear()
    {
        _session = null;
        _spots = Array.Empty<AlignmentSpotRow>();
        _samples = Array.Empty<SampleInfo>();
        _network = null;
        HasResults = false;
        Scores = Array.Empty<ScatterPoint>();
        Loadings = Array.Empty<ScatterPoint>();
        Variance = Array.Empty<VarianceRow>();
        ClusterRoot = null;
        PlsScores = Array.Empty<ScatterPoint>();
        PlsLoadings = Array.Empty<ScatterPoint>();
        VipRows = Array.Empty<VipRow>();
        HasPls = false;
        PlsVerdict = "Not fitted yet.";
        PlsQuality = string.Empty;
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

    public void Load(ResultSession session, IReadOnlyList<AlignmentSpotRow> spots, IReadOnlyList<SampleInfo> samples)
    {
        _session = session;
        _spots = spots;
        _samples = samples;
        HasResults = spots.Count > 0 && samples.Count > 1;
        Summary = HasResults
            ? $"{spots.Count} features across {samples.Count} injections."
            : samples.Count <= 1 ? "Multivariate views need more than one injection." : "No aligned features.";
        if (HasResults) Compute();
    }

    private ValueTransform TransformKind => Transform == "None" ? ValueTransform.None : ValueTransform.Log10;

    private ValueScaling ScalingKind => Scaling switch
    {
        "Pareto" => ValueScaling.Pareto,
        "None (centre only)" => ValueScaling.None,
        _ => ValueScaling.Auto,
    };

    partial void OnValueKindChanged(string value) => Compute();
    partial void OnTransformChanged(string value) => Compute();
    partial void OnScalingChanged(string value) => Compute();
    partial void OnAnnotatedOnlyChanged(bool value) => Compute();

    partial void OnSelectedNetworkFeatureChanged(int value)
    {
        if (value >= 0) RequestShowFeature?.Invoke(value);
    }

    /// <summary>Recomputes the components and the clustering; both are cheap next to reading the data.</summary>
    [RelayCommand]
    private void Compute()
    {
        if (!HasResults) return;
        var features = AnnotatedOnly ? _spots.Where(s => s.IsAnnotated).ToList() : _spots.ToList();
        if (features.Count < 2)
        {
            Summary = "Not enough features for a multivariate view.";
            return;
        }
        IsBusy = true;
        try
        {
            var corrected = UseCorrectedValues ? CorrectedFor(features) : null;
            var matrix = corrected is null
                ? DataMatrix.Build(features, _samples, ValueKind == "Area", TransformKind, ScalingKind)
                : DataMatrix.From(corrected, _samples, features, TransformKind, ScalingKind);
            var pca = Pca.Compute(matrix, Math.Min(3, Math.Max(2, _samples.Count - 1)));
            Scores = pca.Scores
                .Select(s => new ScatterPoint(s.Components[0], s.Components.Length > 1 ? s.Components[1] : 0, s.Sample, s.Class, s))
                .ToList();
            Loadings = pca.Loadings
                .Select(l => new ScatterPoint(l.Components[0], l.Components.Length > 1 ? l.Components[1] : 0, l.Label, l.Group, l))
                .ToList();
            var cumulative = 0.0;
            Variance = pca.ExplainedVariance
                .Select((v, i) => { cumulative += v; return new VarianceRow($"PC{i + 1}", v, cumulative); })
                .ToList();
            ScoreLabel = pca.ExplainedVariance.Count > 1
                ? $"PC1 ({pca.ExplainedVariance[0]:F1} %) against PC2 ({pca.ExplainedVariance[1]:F1} %)"
                : "PC1";

            var clustering = HierarchicalClustering.ClusterSamples(matrix);
            ClusterRoot = clustering.Root;
            ClusterLabel = $"{_samples.Count} injections, average linkage on one minus the Pearson correlation over {features.Count} features";
            Summary = $"{features.Count} features across {_samples.Count} injections · {Transform.ToLowerInvariant()} transform · {Scaling.ToLowerInvariant()}"
                + (corrected is null ? string.Empty : " · drift corrected");
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

    partial void OnUseCorrectedValuesChanged(bool value) => Compute();

    partial void OnSelectedCorrectionRowChanged(FeatureCorrection? value) => ShowDrift(value);

    partial void OnSelectedVipRowChanged(VipRow? value)
    {
        if (value is not null) RequestShowFeature?.Invoke(value.FeatureId);
    }

    /// <summary>
    /// The corrected responses for exactly these features, or null when the correction has not been
    /// run or no longer covers them.
    /// </summary>
    private double[,]? CorrectedFor(IReadOnlyList<AlignmentSpotRow> features)
    {
        if (_correctedRaw is null || _correctedFeatures.Count == 0) return null;
        var column = new Dictionary<int, int>(_correctedFeatures.Count);
        for (var j = 0; j < _correctedFeatures.Count; j++) column[_correctedFeatures[j].Id] = j;
        var n = _samples.Count;
        var values = new double[n, features.Count];
        for (var j = 0; j < features.Count; j++)
        {
            if (!column.TryGetValue(features[j].Id, out var source)) return null;
            for (var i = 0; i < n; i++) values[i, j] = _correctedRaw[i, source];
        }
        return values;
    }

    /// <summary>
    /// Fits the discriminant model against the declared classes, then does its best to talk the
    /// reviewer out of it: cross-validation and a permutation test run with the fit, and the verdict
    /// beside the plot is theirs, not the fit's.
    /// </summary>
    [RelayCommand]
    private async Task FitDiscriminant()
    {
        if (!HasResults) return;
        var features = AnnotatedOnly ? _spots.Where(s => s.IsAnnotated).ToList() : _spots.ToList();
        if (features.Count < 2)
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
            var corrected = UseCorrectedValues ? CorrectedFor(features) : null;
            var result = await Task.Run(() =>
            {
                var matrix = corrected is null
                    ? DataMatrix.Build(features, _samples, ValueKind == "Area", TransformKind, ScalingKind)
                    : DataMatrix.From(corrected, _samples, features, TransformKind, ScalingKind);
                return PartialLeastSquares.Compute(matrix, components, permutations);
            });

            PlsScores = result.Scores
                .Select(s => new ScatterPoint(s.Components[0], s.Components.Length > 1 ? s.Components[1] : 0, s.Sample, s.Class, s))
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
                var ordered = result.Classes.OrderBy(c => meansByClass.TryGetValue(c, out var m) ? m : 0).ToList();
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

    /// <summary>
    /// Fits the drift out of every feature against the quality controls, and reports what it did to
    /// their spread. Nothing is written to the result: the corrected values live here and feed the
    /// other views only while the box is ticked.
    /// </summary>
    [RelayCommand]
    private async Task ApplyCorrection()
    {
        if (!HasResults) return;
        var span = double.TryParse(CorrectionSpan?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)
            ? Math.Clamp(s, 0.2, 1.0)
            : 0.75;
        var features = _spots.ToList();
        IsCorrecting = true;
        CorrectionVerdict = $"Correcting {features.Count} features…";
        try
        {
            var result = await Task.Run(() => BatchCorrection.Apply(features, _samples, ValueKind == "Area", span));
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

        var useArea = ValueKind == "Area";
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
        var features = (AnnotatedOnly ? _spots.Where(s => s.IsAnnotated) : _spots).Where(s => s.MsmsAssigned).ToList();
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
