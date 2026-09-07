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
            var matrix = DataMatrix.Build(features, _samples, ValueKind == "Area", TransformKind, ScalingKind);
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
            Summary = $"{features.Count} features across {_samples.Count} injections · {Transform.ToLowerInvariant()} transform · {Scaling.ToLowerInvariant()}";
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
