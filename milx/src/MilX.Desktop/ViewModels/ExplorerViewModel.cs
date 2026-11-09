using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompMs.Common.Components;
using CompMs.Common.DataObj;
using CompMs.MsdialCore.DataObj;
using MilX.Desktop.Controls;
using MilX.Desktop.Services;
using MilX.Pipeline.Chemistry;
using MilX.Pipeline.Results;

namespace MilX.Desktop.ViewModels;

/// <summary>Node of the "Samples and channels" tree. Files hold a sample node, which holds channel leaves.</summary>
public sealed partial class ExplorerNode : ViewModelBase
{
    public ExplorerNode(string label, string? detail = null)
    {
        Label = label;
        Detail = detail ?? string.Empty;
    }

    public string Label { get; }
    public string Detail { get; }
    public ObservableCollection<ExplorerNode> Children { get; } = new();
    public bool HasChildren => Children.Count > 0;
    public bool IsLeaf => Channel is not null;

    /// <summary>Set on file nodes.</summary>
    public string? Path { get; init; }
    /// <summary>Set on sample and channel nodes.</summary>
    public ExplorerSample? Sample { get; init; }
    /// <summary>Set on channel leaves.</summary>
    public RawChannel? Channel { get; init; }
    public bool IsTicLeaf { get; init; }

    [ObservableProperty] private bool _isChecked;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isVisible = true;
    [ObservableProperty] private string _status = string.Empty;

    public string FullLabel => Sample is null ? Label : $"{Sample.Name} · {Label}";
    public override string ToString() => FullLabel;
}

/// <summary>One raw file of the explorer with its loaded measurement and channels.</summary>
public sealed class ExplorerSample
{
    public ExplorerSample(string path, string name, string cls, string type, AnalysisFileBean? bean)
    {
        Path = path;
        Name = name;
        Class = cls;
        SampleType = type;
        Bean = bean;
    }

    public string Path { get; }
    public string Name { get; }
    public string Class { get; }
    public string SampleType { get; }
    public AnalysisFileBean? Bean { get; }
    public RawMeasurement? Raw { get; set; }
    public IReadOnlyList<RawChannel> Channels { get; set; } = Array.Empty<RawChannel>();
    public RawChannel? Ms1 => Channels.FirstOrDefault(c => c.Kind == RawChannelKind.Ms1);
    public override string ToString() => Name;
}

public sealed partial class ManualXic : ViewModelBase
{
    [ObservableProperty] private double _mz;
    [ObservableProperty] private double _tolerance;
    [ObservableProperty] private string _unit = "Da";
    public string Label => Unit == "view"
        ? $"m/z {(Mz - Tolerance).ToString("F2", CultureInfo.InvariantCulture)}–{(Mz + Tolerance).ToString("F2", CultureInfo.InvariantCulture)} (contour view)"
        : $"m/z {Mz.ToString("F4", CultureInfo.InvariantCulture)} ± {Tolerance.ToString("0.###", CultureInfo.InvariantCulture)} {Unit}";
    public double ToleranceDa => Unit == "ppm" ? RawExplorer.PpmToDa(Mz, Tolerance) : Tolerance;
}

public sealed record SpectrumPeakRow(double Mz, double Intensity, double Relative);

/// <summary>A line of the mass calculator's summary.</summary>
public sealed record MassSummaryRow(string Name, string Value);

/// <summary>One isotope of the theoretical pattern: m/z and relative abundance in percent.</summary>
public sealed record IsotopeRow(double Mz, double Abundance);

/// <summary>
/// Qualitative review of the raw files: a tree of samples and channels, an overlay chromatogram, a spectrum pane
/// and the side panels (peaks of the selected sample, manual XICs, spectrum peak table).
/// </summary>
public sealed partial class ExplorerViewModel : ViewModelBase
{
    private readonly RawDataCache _cache;
    private int _traceVersion;
    private int _spectrumVersion;
    private IReadOnlyList<MoleculeMsReference>? _library;
    private string? _libraryPath;
    private ResultSession? _session;

    public static string[] ChromatogramKinds { get; } = { "TIC", "BPC" };
    public static string[] ToleranceUnits { get; } = { "Da", "ppm" };
    /// <summary>Where a manual XIC is read from; the last two are new to both applications of the suite.</summary>
    public static string[] XicSourceNames { get; } = { "MS1", "Active channel", "Every checked channel", "All MS2", "Gated by precursor" };

    public ExplorerViewModel(RawDataCache cache)
    {
        _cache = cache;
        XicEntries.CollectionChanged += (_, _) => _ = RefreshTracesAsync();
    }

    public ObservableCollection<ExplorerNode> Tree { get; } = new();
    public ObservableCollection<ExplorerNode> ActiveChannels { get; } = new();
    public ObservableCollection<ManualXic> XicEntries { get; } = new();

    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private string _chromatogramKind = "TIC";
    [ObservableProperty] private ExplorerNode? _activeChannel;
    [ObservableProperty] private ExplorerNode? _selectedNode;
    [ObservableProperty] private string _status = "Open a project to review its raw files.";
    [ObservableProperty] private bool _hasSamples;

    // toolbar
    [ObservableProperty] private bool _rangeSelectionMode;
    [ObservableProperty] private bool _normalize;
    [ObservableProperty] private bool _stack;
    [ObservableProperty] private bool _showMzLabels = true;
    [ObservableProperty] private bool _showRtLabels;
    [ObservableProperty] private bool _showLegend = true;
    [ObservableProperty] private double _smoothSigma;
    [ObservableProperty] private double _baselineWindow;

    // chromatogram
    [ObservableProperty] private IReadOnlyList<TraceSeries> _series = Array.Empty<TraceSeries>();
    [ObservableProperty] private string _chromatogramTitle = string.Empty;
    [ObservableProperty] private double _markerRt = double.NaN;
    [ObservableProperty] private double _selectionStart = double.NaN;
    [ObservableProperty] private double _selectionEnd = double.NaN;
    [ObservableProperty] private IReadOnlyList<ShadedWindow>? _windows;

    // spectrum
    [ObservableProperty] private IReadOnlyList<Point> _spectrumPeaks = Array.Empty<Point>();
    [ObservableProperty] private IReadOnlyList<Point>? _referencePeaks;
    [ObservableProperty] private double _precursorMz = double.NaN;
    [ObservableProperty] private string _spectrumTitle = "No spectrum";
    [ObservableProperty] private int _scanNumber;
    [ObservableProperty] private int _scanCount;
    [ObservableProperty] private IReadOnlyList<SpectrumPeakRow> _spectrumTable = Array.Empty<SpectrumPeakRow>();
    [ObservableProperty] private string _selectionLabel = "No range selected";
    /// <summary>The precursor the shown scan actually isolated (per scan, for IDA slots and DDA events), or empty for MS1.</summary>
    [ObservableProperty] private string _scanPrecursorLabel = string.Empty;
    public bool HasSelection => !double.IsNaN(SelectionStart) && !double.IsNaN(SelectionEnd);

    // peaks panel
    [ObservableProperty] private IReadOnlyList<PeakFeatureRow> _peaks = Array.Empty<PeakFeatureRow>();
    [ObservableProperty] private PeakFeatureRow? _selectedPeak;
    [ObservableProperty] private string _peaksTitle = "No results for this sample";
    [ObservableProperty] private string _peakFilter = string.Empty;
    public IReadOnlyList<PeakFeatureRow> FilteredPeaks => string.IsNullOrWhiteSpace(PeakFilter)
        ? Peaks
        : Peaks.Where(p => p.Name.Contains(PeakFilter, StringComparison.OrdinalIgnoreCase) || p.Mz.ToString("F3", CultureInfo.InvariantCulture).Contains(PeakFilter, StringComparison.Ordinal)).ToList();

    // contour view (View ▸ Contour): the active channel as RT × m/z × intensity
    public static string[] ContourIntensityScales => ContourChart.IntensityScales;
    public static string[] ContourColourMaps => ContourChart.ColourMaps;
    [ObservableProperty] private bool _showContour;
    [ObservableProperty] private ContourGrid? _contour;
    [ObservableProperty] private string _contourScale = "sqrt";
    [ObservableProperty] private string _contourColours = "inferno";
    [ObservableProperty] private double _contourMzFrom;
    [ObservableProperty] private double _contourMzTo;
    [ObservableProperty] private string _contourStatus = string.Empty;
    private int _contourVersion;

    // mass calculator (MIL-Q's chemistry, same tables): formula → masses, adduct m/z, isotope pattern, ppm
    public static IReadOnlyList<string> AdductNames => Adduct.Names;
    [ObservableProperty] private string _formulaText = string.Empty;
    [ObservableProperty] private string _adductName = "[M+H]+";
    [ObservableProperty] private string _measuredMzText = string.Empty;
    [ObservableProperty] private IReadOnlyList<MassSummaryRow> _massSummary = Array.Empty<MassSummaryRow>();
    [ObservableProperty] private IReadOnlyList<IsotopeRow> _isotopeRows = Array.Empty<IsotopeRow>();
    [ObservableProperty] private string _massError = "—";
    [ObservableProperty] private string _formulaStatus = string.Empty;
    [ObservableProperty] private IReadOnlyList<Point>? _overlayPeaks;
    private double _theoreticalMz = double.NaN;
    private IReadOnlyList<(double Mz, double Abundance)> _pattern = Array.Empty<(double, double)>();

    // manual XIC form
    [ObservableProperty] private string _xicMzText = string.Empty;
    [ObservableProperty] private double _xicTolerance = 0.01;
    [ObservableProperty] private string _xicUnit = "Da";
    [ObservableProperty] private string _xicSourceName = "MS1";
    [ObservableProperty] private double _xicGateMz;
    [ObservableProperty] private double _xicGateTolerance = XicSources.DefaultGateTolerance;
    public bool XicIsGated => XicSourceName == "Gated by precursor";
    [ObservableProperty] private string _xicSourceNote = string.Empty;

    public int TopLabels => ShowMzLabels ? 8 : 0;

    // ---------------------------------------------------------------- session

    public void Clear()
    {
        _loading.Clear();
        Tree.Clear();
        ActiveChannels.Clear();
        Series = Array.Empty<TraceSeries>();
        SpectrumPeaks = Array.Empty<Point>();
        SpectrumTable = Array.Empty<SpectrumPeakRow>();
        Peaks = Array.Empty<PeakFeatureRow>();
        ActiveChannel = null;
        HasSamples = false;
        Status = "Open a project to review its raw files.";
    }

    /// <summary>Builds the tree from the batch; results (when present) supply classes/types and the peaks panel.</summary>
    public void Load(IReadOnlyList<InputFileViewModel> samples, ResultSession? session)
    {
        _session = session;
        _library = null;
        _loading.Clear();
        Tree.Clear();
        ActiveChannels.Clear();
        ActiveChannel = null;
        foreach (var s in samples)
        {
            var bean = session?.AnalysisFiles.FirstOrDefault(b => string.Equals(b.AnalysisFilePath, s.Path, StringComparison.OrdinalIgnoreCase)
                                                                 || string.Equals(b.AnalysisFileName, s.Name, StringComparison.OrdinalIgnoreCase));
            var sample = new ExplorerSample(s.Path, s.Name, s.Class, s.SampleType.ToString(), bean);
            var fileNode = new ExplorerNode(s.FileName, s.Badge) { Path = s.Path, IsExpanded = false };
            var sampleNode = new ExplorerNode(sample.Name, $"{sample.SampleType} · class {sample.Class}") { Sample = sample, IsExpanded = true };
            sampleNode.Children.Add(new ExplorerNode("Loading channels…") { Sample = sample });
            fileNode.Children.Add(sampleNode);
            fileNode.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ExplorerNode.IsExpanded) && fileNode.IsExpanded) _ = EnsureLoadedAsync(sampleNode, checkTic: false);
            };
            Tree.Add(fileNode);
        }
        HasSamples = Tree.Count > 0;
        Status = HasSamples ? $"{Tree.Count} sample(s). Tick a channel to draw it; click the chromatogram to see a scan." : "The batch has no samples.";
        if (Tree.Count > 0)
        {
            Tree[0].IsExpanded = true;
            _ = EnsureLoadedAsync((ExplorerNode)Tree[0].Children[0], checkTic: true);
        }
    }

    private readonly Dictionary<ExplorerNode, Task> _loading = new();

    /// <summary>Loads a sample's channels once (expanding the node and the initial load may both ask); optionally ticks its TIC afterwards.</summary>
    private async Task EnsureLoadedAsync(ExplorerNode sampleNode, bool checkTic)
    {
        if (!_loading.TryGetValue(sampleNode, out var task))
        {
            task = LoadSampleAsync(sampleNode);
            _loading[sampleNode] = task;
        }
        await task;
        if (checkTic)
        {
            var tic = sampleNode.Children.FirstOrDefault(c => c.IsTicLeaf);
            if (tic is not null) tic.IsChecked = true;
        }
    }

    private async Task LoadSampleAsync(ExplorerNode sampleNode)
    {
        var sample = sampleNode.Sample!;
        if (sample.Raw is not null) return;
        try
        {
            var raw = await _cache.GetAsync(sample.Path);
            var channels = _cache.Channels(sample.Path, raw);
            sample.Raw = raw;
            sample.Channels = channels;
            sampleNode.Children.Clear();
            var ms1Indices = channels.FirstOrDefault(c => c.Kind == RawChannelKind.Ms1)?.SpectrumIndices ?? Enumerable.Range(0, raw.SpectrumList.Count).ToList();
            var experiments = RawExplorer.HasExperiments(raw);
            var tic = new ExplorerNode("TIC", experiments ? $"all {channels.Count} experiments summed" : "MS1 survey scans") { Sample = sample, Channel = new RawChannel(RawChannelKind.Ms1, "TIC", ms1Indices, 0, 0, 0), IsTicLeaf = true };
            Hook(tic);
            sampleNode.Children.Add(tic);
            foreach (var c in channels)
            {
                var leaf = new ExplorerNode(c.Label, $"{c.Count} scans") { Sample = sample, Channel = c };
                Hook(leaf);
                sampleNode.Children.Add(leaf);
                ActiveChannels.Add(leaf);
            }
            ActiveChannel ??= sampleNode.Children.Skip(1).FirstOrDefault() ?? tic;
            ApplyFilter();
            await LoadPeaksAsync(sample);
        }
        catch (Exception ex)
        {
            sampleNode.Children.Clear();
            sampleNode.Children.Add(new ExplorerNode("Could not read file", ex.Message) { Sample = sample });
            Status = $"{sample.Name}: {ex.Message}";
        }
    }

    private void Hook(ExplorerNode leaf)
    {
        leaf.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExplorerNode.IsChecked)) _ = RefreshTracesAsync();
        };
    }

    partial void OnSelectedNodeChanged(ExplorerNode? value)
    {
        if (value?.Channel is not null) ActiveChannel = value.IsTicLeaf ? (ActiveChannels.FirstOrDefault(c => c.Sample == value.Sample) ?? value) : value;
        else if (value?.Sample is not null && value.Children.Count > 0)
        {
            ActiveChannel = ActiveChannels.FirstOrDefault(c => c.Sample == value.Sample) ?? ActiveChannel;
        }
    }

    partial void OnActiveChannelChanged(ExplorerNode? value)
    {
        if (value?.Sample is not null)
        {
            _ = LoadPeaksAsync(value.Sample);
            if (double.IsNaN(MarkerRt) && value.Channel is { Count: > 0 } && value.Sample.Raw is not null)
            {
                ShowScan(0);
            }
        }
        _ = RefreshTracesAsync();
        if (ShowContour) _ = RebuildContourAsync(null, null);
    }

    // ---------------------------------------------------------------- contour

    partial void OnShowContourChanged(bool value)
    {
        if (value && Contour is null) _ = RebuildContourAsync(null, null);
    }

    [RelayCommand]
    private void ToggleContour() => ShowContour = !ShowContour;

    /// <summary>
    /// Builds the surface of the active channel over a time range (null: the whole run) and the m/z range
    /// typed beside the view (0 and 0: the channel's own range). Runs off the interface thread; a newer
    /// request supersedes an older one.
    /// </summary>
    public async Task RebuildContourAsync(double? rtLo, double? rtHi)
    {
        var node = ActiveChannel;
        if (node?.Sample?.Raw is null || node.Channel is null)
        {
            Contour = null;
            ContourStatus = "Select a channel to draw its surface.";
            return;
        }
        var version = ++_contourVersion;
        var raw = node.Sample.Raw;
        var indices = node.IsTicLeaf ? node.Sample.Ms1?.SpectrumIndices ?? node.Channel.SpectrumIndices : node.Channel.SpectrumIndices;
        (double, double)? rtRange = rtLo is { } a && rtHi is { } b && b > a ? (a, b) : null;
        (double, double)? mzRange = ContourMzTo > ContourMzFrom && ContourMzTo > 0 ? (ContourMzFrom, ContourMzTo) : null;
        var label = node.IsTicLeaf ? "MS1" : node.Label;
        ContourStatus = $"Building the surface of {label}…";
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var grid = await Task.Run(() => ContourGrid.Build(raw, indices, rtRange, mzRange));
        if (version != _contourVersion) return;
        Contour = grid;
        var (lo, hi) = grid.MzRange;
        ContourStatus = grid.IsEmpty
            ? grid.Note
            : $"{node.Sample.Name} · {label}: {grid.Rows} rows × {grid.Bins} bins, {grid.Scans} scans, m/z {lo:F1}–{hi:F1}, built in {watch.Elapsed.TotalSeconds:F1} s" + (grid.Note.Length > 0 ? $" · {grid.Note}" : string.Empty);
    }

    /// <summary>A click on the surface: the spectrum of the scan under the cursor.</summary>
    public void ContourClicked(double rt) => ShowScanAt(rt);

    /// <summary>
    /// The chromatogram and the spectrum of the rectangle on screen — both read from the reader, not from
    /// the grid: the XIC over the m/z band across the channel's scans, as a manual XIC entry, and the
    /// average spectrum over the time range, in the spectrum pane. The view switches back to the
    /// chromatogram so the trace is in sight.
    /// </summary>
    public async Task ExtractContourViewAsync(double rtLo, double rtHi)
    {
        var node = ActiveChannel;
        if (node?.Sample?.Raw is null || node.Channel is null || Contour is not { IsEmpty: false } grid) return;
        var raw = node.Sample.Raw;
        var indices = node.IsTicLeaf ? node.Sample.Ms1?.SpectrumIndices ?? node.Channel.SpectrumIndices : node.Channel.SpectrumIndices;
        var (mzLo, mzHi) = grid.MzRange;
        if (rtHi < rtLo) (rtLo, rtHi) = (rtHi, rtLo);
        var centre = (mzLo + mzHi) / 2;
        var half = (mzHi - mzLo) / 2;
        var spectrum = await Task.Run(() => RawExplorer.AverageSpectrum(raw, indices, rtLo, rtHi));
        var existing = XicEntries.FirstOrDefault(x => x.Unit == "view");
        if (existing is not null) XicEntries.Remove(existing);
        XicSourceName = "Active channel";
        XicEntries.Add(new ManualXic { Mz = centre, Tolerance = half, Unit = "view" });
        SelectionStart = rtLo;
        SelectionEnd = rtHi;
        MarkerRt = double.NaN;
        SetSpectrum(spectrum, $"{node.Sample.Name} · {node.Label} · {spectrum.Label} · m/z {mzLo:F2}–{mzHi:F2}", double.NaN, null);
        ShowContour = false;
        Status = $"Extracted this view from the reader: XIC of m/z {mzLo:F2}–{mzHi:F2} on {node.Label}, and the average of RT {rtLo:F3}–{rtHi:F3} min.";
    }

    partial void OnFilterTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var f = FilterText?.Trim() ?? string.Empty;
        foreach (var file in Tree)
        {
            var any = false;
            foreach (var sample in file.Children)
            {
                foreach (var leaf in sample.Children)
                {
                    leaf.IsVisible = f.Length == 0 || leaf.Label.Contains(f, StringComparison.OrdinalIgnoreCase) || leaf.IsChecked;
                    any |= leaf.IsVisible;
                }
            }
            file.IsVisible = f.Length == 0 || any || file.Label.Contains(f, StringComparison.OrdinalIgnoreCase);
        }
    }

    [RelayCommand]
    private void UncheckAll()
    {
        foreach (var leaf in Leaves()) leaf.IsChecked = false;
    }

    [RelayCommand]
    private void CollapseAll()
    {
        foreach (var file in Tree) file.IsExpanded = false;
    }

    [RelayCommand]
    private void CheckAllTic()
    {
        foreach (var file in Tree)
        {
            file.IsExpanded = true;
            foreach (var sample in file.Children)
            {
                if (sample.Sample?.Raw is null) { _ = EnsureLoadedAsync(sample, checkTic: true); continue; }
                foreach (var leaf in sample.Children) leaf.IsChecked = leaf.IsTicLeaf;
            }
        }
    }

    private IEnumerable<ExplorerNode> Leaves() => Tree.SelectMany(f => f.Children).SelectMany(s => s.Children).Where(l => l.Channel is not null);

    // ---------------------------------------------------------------- traces

    partial void OnChromatogramKindChanged(string value) => _ = RefreshTracesAsync();
    partial void OnSmoothSigmaChanged(double value) => _ = RefreshTracesAsync();
    partial void OnBaselineWindowChanged(double value) => _ = RefreshTracesAsync();
    partial void OnShowMzLabelsChanged(bool value) => OnPropertyChanged(nameof(TopLabels));
    partial void OnSelectionStartChanged(double value) => UpdateSelectionLabel();
    partial void OnSelectionEndChanged(double value) => UpdateSelectionLabel();

    private void UpdateSelectionLabel()
    {
        OnPropertyChanged(nameof(HasSelection));
        SelectionLabel = HasSelection ? $"Range {Math.Min(SelectionStart, SelectionEnd):F3}–{Math.Max(SelectionStart, SelectionEnd):F3} min" : "No range selected";
    }

    private async Task RefreshTracesAsync()
    {
        var version = ++_traceVersion;
        var checkedLeaves = Leaves().Where(l => l.IsChecked && l.Sample?.Raw is not null).ToList();
        var xics = XicEntries.ToList();
        var activeNode = ActiveChannel;
        var active = activeNode?.Sample;
        var kind = ChromatogramKind;
        var sigma = SmoothSigma;
        var baseline = BaselineWindow;
        var normalize = Normalize;
        var sourceName = XicSourceName;
        var gateMz = XicGateMz;
        var gateTol = XicGateTolerance > 0 ? XicGateTolerance : XicSources.DefaultGateTolerance;

        var (series, note) = await Task.Run(() =>
        {
            var list = new List<TraceSeries>();
            foreach (var leaf in checkedLeaves)
            {
                var raw = leaf.Sample!.Raw!;
                var ch = leaf.Channel!;
                var chrom = leaf.IsTicLeaf ? RawExplorer.SampleTic(raw, ch.SpectrumIndices) : kind == "BPC" ? RawExplorer.Bpc(raw, ch.SpectrumIndices) : RawExplorer.Tic(raw, ch.SpectrumIndices);
                var label = leaf.IsTicLeaf ? $"{leaf.Sample.Name} · TIC" : $"{leaf.Sample.Name} · {leaf.Label} ({kind})";
                list.Add(new TraceSeries(label, Post(chrom.Points, sigma, baseline, normalize)));
            }
            var sourceNote = string.Empty;
            if (xics.Count > 0)
            {
                // every (sample, scan set) an XIC is read from, by the source chosen in the panel
                var targets = new List<(ExplorerSample Sample, string Label, IReadOnlyList<int> Indices)>();
                switch (sourceName)
                {
                    case "Every checked channel":
                        foreach (var leaf in checkedLeaves.Where(l => !l.IsTicLeaf)) targets.Add((leaf.Sample!, leaf.Label, leaf.Channel!.SpectrumIndices));
                        if (targets.Count == 0) sourceNote = "No channel is checked; tick the channels to extract from.";
                        break;
                    case "Active channel":
                        if (activeNode?.Channel is { } ac && active?.Raw is not null) targets.Add((active, activeNode.IsTicLeaf ? "MS1" : activeNode.Label, activeNode.IsTicLeaf ? active.Ms1?.SpectrumIndices ?? ac.SpectrumIndices : ac.SpectrumIndices));
                        break;
                    case "All MS2":
                        if (active?.Raw is not null)
                        {
                            var idx = XicSources.Ms2All(active.Raw);
                            targets.Add((active, "all MS2", idx));
                            sourceNote = $"{idx.Count} MS2 scans across every channel.";
                        }
                        break;
                    case "Gated by precursor":
                        if (active?.Raw is not null)
                        {
                            if (gateMz <= 0) sourceNote = "Enter the precursor m/z to gate on.";
                            else
                            {
                                var idx = XicSources.Gated(active.Raw, gateMz, gateTol);
                                var slots = idx.Select(i => active.Raw.SpectrumList[i].ExperimentID).Distinct().Count();
                                targets.Add((active, XicSource.GatedBy(gateMz, gateTol).Label, idx));
                                sourceNote = idx.Count == 0
                                    ? $"No MS2 scan isolated {gateMz.ToString("F4", CultureInfo.InvariantCulture)} ± {gateTol.ToString("0.###", CultureInfo.InvariantCulture)}."
                                    : $"{idx.Count} MS2 scans whose precursor is {gateMz.ToString("F4", CultureInfo.InvariantCulture)} ± {gateTol.ToString("0.###", CultureInfo.InvariantCulture)}, from {slots} slot(s)/channel(s), merged by RT.";
                            }
                        }
                        break;
                    default:
                        if (active?.Raw is not null) targets.Add((active, "MS1", XicSources.Ms1(active.Raw, active.Channels)));
                        break;
                }
                foreach (var (sample, label, indices) in targets)
                {
                    foreach (var x in xics)
                    {
                        var chrom = RawExplorer.Xic(sample.Raw!, indices, x.Mz, x.ToleranceDa);
                        var name = label == "MS1" ? $"{sample.Name} · XIC {x.Mz:F4}" : $"{sample.Name} · {label} · XIC {x.Mz:F4}";
                        list.Add(new TraceSeries(name, Post(chrom.Points, sigma, baseline, normalize), null, false, true));
                    }
                }
            }
            return ((IReadOnlyList<TraceSeries>)list, sourceNote);
        });
        if (version != _traceVersion) return;
        Series = series;
        XicSourceNote = note;
        ChromatogramTitle = series.Count == 0 ? "Chromatogram" : series.Count <= 3 ? string.Join("  ·  ", series.Select(s => s.Label)) : $"{series.Count} traces";
    }

    private static IReadOnlyList<Point> Post(IReadOnlyList<ChromatogramPoint> pts, double sigma, double baseline, bool normalize)
    {
        var p = ChromatogramMath.SubtractBaseline(ChromatogramMath.Smooth(pts, sigma), baseline);
        if (normalize) p = ChromatogramMath.Normalize(p);
        return p.Select(q => new Point(q.Rt, q.Intensity)).ToList();
    }

    partial void OnNormalizeChanged(bool value) => _ = RefreshTracesAsync();
    partial void OnXicSourceNameChanged(string value) { OnPropertyChanged(nameof(XicIsGated)); _ = RefreshTracesAsync(); }
    partial void OnXicGateMzChanged(double value) => _ = RefreshTracesAsync();
    partial void OnXicGateToleranceChanged(double value) => _ = RefreshTracesAsync();

    // ---------------------------------------------------------------- spectra

    /// <summary>Called by the view when the chromatogram is clicked.</summary>
    public void ShowScanAt(double rt)
    {
        var node = ActiveChannel;
        if (node?.Sample?.Raw is null || node.Channel is null) return;
        var i = RawExplorer.NearestScan(node.Sample.Raw, node.Channel.SpectrumIndices, rt);
        if (i >= 0) ShowScan(i);
    }

    private void ShowScan(int positionInChannel)
    {
        var node = ActiveChannel;
        if (node?.Sample?.Raw is null || node.Channel is null) return;
        var indices = node.Channel.SpectrumIndices;
        if (indices.Count == 0) return;
        positionInChannel = Math.Clamp(positionInChannel, 0, indices.Count - 1);
        var raw = node.Sample.Raw;
        var spectrum = RawExplorer.Spectrum(raw, indices[positionInChannel]);
        ScanCount = indices.Count;
        ScanNumber = positionInChannel + 1;
        MarkerRt = RawExplorer.RtMinutes(raw.SpectrumList[indices[positionInChannel]]);
        SetSpectrum(spectrum, $"{node.Sample.Name} · {spectrum.Label}", spectrum.PrecursorMz > 0 ? spectrum.PrecursorMz : double.NaN, null);
        ScanPrecursorLabel = RawExplorer.DescribePrecursor(raw, indices[positionInChannel]);
    }

    private void SetSpectrum(MsSpectrum spectrum, string title, double precursor, IReadOnlyList<Point>? reference)
    {
        ++_spectrumVersion;
        OverlayPeaks = null;
        SpectrumPeaks = spectrum.Peaks.Select(p => new Point(p.Mz, p.Intensity)).ToList();
        ReferencePeaks = reference;
        PrecursorMz = precursor;
        SpectrumTitle = title;
        var max = spectrum.MaxIntensity;
        SpectrumTable = spectrum.Peaks.OrderByDescending(p => p.Intensity).Select(p => new SpectrumPeakRow(p.Mz, p.Intensity, max > 0 ? 100.0 * p.Intensity / max : 0)).ToList();
    }

    [RelayCommand] private void PreviousScan() => ShowScan(ScanNumber - 2);
    [RelayCommand] private void NextScan() => ShowScan(ScanNumber);

    public void GoToScan(int number) => ShowScan(number - 1);

    [RelayCommand]
    private async Task AverageSelectedRangeAsync()
    {
        var node = ActiveChannel;
        if (!HasSelection || node?.Sample?.Raw is null || node.Channel is null) return;
        var raw = node.Sample.Raw;
        var (a, b) = (Math.Min(SelectionStart, SelectionEnd), Math.Max(SelectionStart, SelectionEnd));
        var indices = node.Channel.SpectrumIndices;
        var spectrum = await Task.Run(() => RawExplorer.AverageSpectrum(raw, indices, a, b));
        MarkerRt = double.NaN;
        SetSpectrum(spectrum, $"{node.Sample.Name} · {node.Label} · {spectrum.Label}", double.NaN, null);
    }

    [RelayCommand]
    private void ClearSelection()
    {
        SelectionStart = double.NaN;
        SelectionEnd = double.NaN;
    }

    // ---------------------------------------------------------------- peaks panel

    private async Task LoadPeaksAsync(ExplorerSample sample)
    {
        if (sample.Bean is null)
        {
            Peaks = Array.Empty<PeakFeatureRow>();
            PeaksTitle = "No results for this sample";
            OnPropertyChanged(nameof(FilteredPeaks));
            return;
        }
        try
        {
            var rows = await ResultLoader.LoadPeakTableAsync(sample.Bean);
            if (ActiveChannel?.Sample != sample) return;
            Peaks = rows;
            PeaksTitle = $"{sample.Name}: {rows.Count} peaks, {rows.Count(r => r.IsAnnotated)} annotated";
            OnPropertyChanged(nameof(FilteredPeaks));
        }
        catch (Exception ex)
        {
            Peaks = Array.Empty<PeakFeatureRow>();
            PeaksTitle = "Peaks unavailable: " + ex.Message;
            OnPropertyChanged(nameof(FilteredPeaks));
        }
    }

    partial void OnPeakFilterChanged(string value) => OnPropertyChanged(nameof(FilteredPeaks));

    partial void OnSelectedPeakChanged(PeakFeatureRow? value)
    {
        if (value is null) return;
        _ = ShowPeakAsync(value);
    }

    private async Task ShowPeakAsync(PeakFeatureRow peak)
    {
        var sample = ActiveChannel?.Sample;
        if (sample?.Raw is null || sample.Bean is null) return;
        // XIC of the peak as a manual entry (replacing the previous peak XIC)
        var tol = _session?.Parameter?.CentroidMs1Tolerance is > 0 and < 1 ? _session.Parameter.CentroidMs1Tolerance : 0.01;
        var existing = XicEntries.FirstOrDefault(x => x.Unit == "peak");
        if (existing is not null) XicEntries.Remove(existing);
        XicEntries.Add(new ManualXic { Mz = peak.Mz, Tolerance = tol, Unit = "peak" });
        Windows = new[] { new ShadedWindow(peak.RtLeft, peak.RtRight, WindowKind.Integration) };
        MarkerRt = peak.Rt;

        // deconvoluted MS/MS + reference
        var bean = sample.Bean;
        try
        {
            var ms2 = await Task.Run(() => ResultLoader.LoadMs2(bean, peak));
            IReadOnlyList<Point>? reference = null;
            if (peak.IsAnnotated)
            {
                var r = await Task.Run(() => ResultLoader.LoadReferenceSpectrum(_session?.DataBaseMapper, peak.MatchResult)) ?? await FindReferenceAsync(peak.Name, peak.Mz);
                if (r is { Peaks.Count: > 0 }) reference = r.Peaks.Select(p => new Point(p.Mz, p.Intensity)).ToList();
            }
            if (SelectedPeak != peak) return;
            if (ms2 is null)
            {
                SetSpectrum(MsSpectrum.Empty, $"No deconvoluted MS/MS for peak {peak.Id}", peak.Mz, null);
            }
            else
            {
                SetSpectrum(ms2, $"{sample.Name} · deconvoluted MS/MS · {ms2.Label}" + (reference is null ? string.Empty : " · mirror: library"), peak.Mz, reference);
            }
        }
        catch (Exception ex)
        {
            Status = "MS/MS unavailable: " + ex.Message;
        }
    }

    private async Task<MsSpectrum?> FindReferenceAsync(string name, double mz)
    {
        var mspPath = _session?.Parameter?.MspFilePath;
        if (string.IsNullOrWhiteSpace(mspPath) || !File.Exists(mspPath)) return null;
        if (_library is null || !string.Equals(_libraryPath, mspPath, StringComparison.OrdinalIgnoreCase))
        {
            _library = await ResultLoader.LoadMspLibraryAsync(mspPath);
            _libraryPath = mspPath;
        }
        var reference = ResultLoader.FindReference(_library, name, mz);
        return reference is null ? null : ResultLoader.ToSpectrum(reference);
    }

    // ---------------------------------------------------------------- mass calculator

    partial void OnFormulaTextChanged(string value) => Recompute();
    partial void OnAdductNameChanged(string value) => Recompute();
    partial void OnMeasuredMzTextChanged(string value) => UpdateMassError();

    private void Recompute()
    {
        _pattern = Array.Empty<(double, double)>();
        _theoreticalMz = double.NaN;
        var text = (FormulaText ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            MassSummary = Array.Empty<MassSummaryRow>();
            IsotopeRows = Array.Empty<IsotopeRow>();
            FormulaStatus = string.Empty;
            UpdateMassError();
            return;
        }
        IReadOnlyList<KeyValuePair<string, int>> counts;
        try { counts = Formula.Parse(text); }
        catch (FormulaException ex)
        {
            FormulaStatus = ex.Message;
            MassSummary = Array.Empty<MassSummaryRow>();
            IsotopeRows = Array.Empty<IsotopeRow>();
            UpdateMassError();
            return;
        }
        FormulaStatus = string.Empty;
        var adduct = Adduct.ByName(AdductName) ?? Adduct.All[5];
        var mono = Formula.MonoisotopicMass(counts);
        _theoreticalMz = adduct.Mz(mono);
        MassSummary = new[]
        {
            new MassSummaryRow("Formula", Formula.Format(counts)),
            new MassSummaryRow("Monoisotopic", mono.ToString("F5", CultureInfo.InvariantCulture)),
            new MassSummaryRow("Average", Formula.AverageMass(counts).ToString("F4", CultureInfo.InvariantCulture)),
            new MassSummaryRow("RDBE", Formula.Rdbe(counts).ToString("0.#", CultureInfo.InvariantCulture)),
            new MassSummaryRow($"Ion m/z {adduct.Name}", _theoreticalMz.ToString("F5", CultureInfo.InvariantCulture)),
        };
        _pattern = Formula.IsotopePattern(counts, adduct, minAbundance: 0.001, maxPeaks: 12);
        IsotopeRows = _pattern.Select(p => new IsotopeRow(p.Mz, p.Abundance * 100)).ToList();
        UpdateMassError();
    }

    private void UpdateMassError()
    {
        var text = (MeasuredMzText ?? string.Empty).Trim().Replace(',', '.');
        if (double.IsNaN(_theoreticalMz) || text.Length == 0 || !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var measured))
        {
            MassError = "—";
            return;
        }
        var mda = Formula.MassErrorMda(measured, _theoreticalMz);
        var ppm = Formula.MassErrorPpm(measured, _theoreticalMz);
        MassError = $"{mda.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)} mDa    {ppm.ToString("+0.00;-0.00", CultureInfo.InvariantCulture)} ppm";
    }

    /// <summary>The theoretical pattern over the spectrum pane, as sticks scaled to the measured peak nearest the monoisotopic m/z.</summary>
    [RelayCommand]
    private void OverlayIsotopes()
    {
        if (_pattern.Count == 0 || SpectrumPeaks.Count == 0) { Status = "Type a formula and show a spectrum to overlay its isotope pattern."; return; }
        var m = _pattern[0].Mz;
        var nearest = SpectrumPeaks.OrderBy(p => Math.Abs(p.X - m)).First();
        var scale = nearest.Y > 0 ? nearest.Y / _pattern[0].Abundance : SpectrumPeaks.Max(p => p.Y);
        OverlayPeaks = _pattern.Select(p => new Point(p.Mz, p.Abundance * scale)).ToList();
        Status = $"Isotope pattern of {FormulaText.Trim()} {AdductName} overlaid, scaled to the measured peak at {nearest.X:F4} ({Formula.MassErrorPpm(nearest.X, m):+0.0;-0.0} ppm from M).";
    }

    [RelayCommand]
    private void ClearOverlay() => OverlayPeaks = null;

    /// <summary>The measured m/z taken from the spectrum pane: the peak nearest the theoretical ion.</summary>
    [RelayCommand]
    private void MeasuredFromSpectrum()
    {
        if (double.IsNaN(_theoreticalMz) || SpectrumPeaks.Count == 0) { Status = "Type a formula and show a spectrum to read the measured m/z from it."; return; }
        var nearest = SpectrumPeaks.OrderBy(p => Math.Abs(p.X - _theoreticalMz)).First();
        MeasuredMzText = nearest.X.ToString("F4", CultureInfo.InvariantCulture);
    }

    // ---------------------------------------------------------------- manual XIC

    [RelayCommand]
    private void AddXic()
    {
        // commas, semicolons or spaces separate the masses: "183.1391, 313.2384" as MIL-Q takes it
        var masses = XicSources.ParseMzList(XicMzText, out var rejected);
        foreach (var mz in masses) XicEntries.Add(new ManualXic { Mz = mz, Tolerance = XicTolerance, Unit = XicUnit });
        if (masses.Count > 0) XicMzText = string.Empty;
        if (masses.Count == 0) Status = "Enter one or more m/z values, separated by commas or spaces (e.g. 183.1391, 313.2384).";
        else if (rejected.Count > 0) Status = $"Skipped what is not a mass: {string.Join(", ", rejected)}.";
    }

    /// <summary>Gates the XICs on a precursor from outside the panel (a spectrum's precursor, a peak).</summary>
    public void GateOn(double precursorMz)
    {
        if (precursorMz <= 0) return;
        XicGateMz = precursorMz;
        XicSourceName = "Gated by precursor";
    }

    [RelayCommand]
    private void RemoveXic(ManualXic? entry)
    {
        if (entry is not null) XicEntries.Remove(entry);
    }

    [RelayCommand]
    private void ClearXics()
    {
        XicEntries.Clear();
        Windows = null;
    }
}
