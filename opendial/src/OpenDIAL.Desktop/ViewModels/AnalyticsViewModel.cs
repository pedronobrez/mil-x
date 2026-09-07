using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompMs.Common.Components;
using CompMs.MsdialCore.DataObj;
using OpenDIAL.Desktop.Controls;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Interop.OpenQuant;
using CompMs.MsdialCore.MSDec;
using OpenDIAL.Pipeline.Model;
using OpenDIAL.Pipeline.Results;

namespace OpenDIAL.Desktop.ViewModels;

/// <summary>Everything the results workspaces need, independent of whether it came from a run or from an opened project.</summary>
public sealed record ResultSession(
    string Folder,
    IReadOnlyList<AnalysisFileBean> AnalysisFiles,
    AlignmentFileBean? AlignmentFile,
    CompMs.MsdialCore.Parameter.ParameterBase? Parameter,
    DataBaseMapper? DataBaseMapper,
    IReadOnlyList<ExportedFile> ExportedFiles,
    string? ProjectFilePath,
    IonizationMode Mode)
{
    public static ResultSession From(PipelineResult r) => new(r.OutputFolder, r.AnalysisFiles, r.AlignmentFile, r.Parameter, r.DataBaseMapper, r.ExportedFiles, r.ProjectFilePath, r.Mode);
    public static ResultSession From(OpenedProject p) => new(p.Folder, p.AnalysisFiles, p.AlignmentFile, p.Parameter, p.DataBaseMapper, p.ExportedFiles, p.ProjectFilePath, p.Mode);
}

/// <summary>Row of the spot list: either a group header or an alignment spot.</summary>
public sealed class SpotListItem
{
    public SpotListItem(string header, int count) { Header = header; Count = count; IsHeader = true; }
    public SpotListItem(AlignmentSpotRow spot) { Spot = spot; IsHeader = false; Header = string.Empty; }
    public bool IsHeader { get; }
    public string Header { get; }
    public int Count { get; }
    public AlignmentSpotRow? Spot { get; }
    public string Title => Spot is null ? Header : Spot.IsAnnotated ? Spot.Name : $"m/z {Spot.Mz.ToString("F4", CultureInfo.InvariantCulture)}";
    public string Subtitle => Spot is null ? string.Empty : $"{Spot.Rt.ToString("F2", CultureInfo.InvariantCulture)} min · m/z {Spot.Mz.ToString("F4", CultureInfo.InvariantCulture)} · #{Spot.Id}";
}

/// <summary>One EIC panel of the review grid.</summary>
public sealed partial class ReviewPanelViewModel : ViewModelBase
{
    public ReviewPanelViewModel(AlignedSamplePeak sample) { Sample = sample; }
    public AlignedSamplePeak Sample { get; }
    public string SampleName => Sample.FileName;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private IReadOnlyList<TraceSeries> _series = Array.Empty<TraceSeries>();
    [ObservableProperty] private IReadOnlyList<ShadedWindow>? _windows;
    [ObservableProperty] private double _markerRt = double.NaN;
    [ObservableProperty] private double _fixedXMin = double.NaN;
    [ObservableProperty] private double _fixedXMax = double.NaN;
    [ObservableProperty] private double _fixedYMax = double.NaN;
    [ObservableProperty] private bool _isSelected;
    public IReadOnlyList<Point> FullTrace { get; set; } = Array.Empty<Point>();
    public double PeakMax { get; set; }
}

/// <summary>Per-class statistics of the selected spot.</summary>
public sealed record ClassStatRow(string Class, int N, double Mean, double Sd, double Cv, double Min, double Max, double AreaMean);

/// <summary>Per-sample row of the Results tab.</summary>
public sealed class SampleResultRow
{
    public SampleResultRow(AlignedSamplePeak p, string sampleType, int order) { Peak = p; SampleType = sampleType; Order = order; }
    public AlignedSamplePeak Peak { get; }
    public int Order { get; }
    public string Sample => Peak.FileName;
    public string Class => Peak.Class;
    public string SampleType { get; }
    public double Rt => Peak.Rt;
    public double Mz => Peak.Mz;
    public double Height => Peak.Height;
    public double Area => Peak.Area;
    public double SignalToNoise => Peak.SignalToNoise;
    public string GapFilled => Peak.IsGapFilled ? "gap-filled" : string.Empty;
    public bool IsGapFilled => Peak.IsGapFilled;
    public double RtLeft => Peak.RtLeft;
    public double RtRight => Peak.RtRight;
}

/// <summary>
/// The results of the untargeted run: the alignment spot list, the per-sample review grid and the result tabs.
/// </summary>
public sealed partial class AnalyticsViewModel : ViewModelBase
{
    private readonly RawDataCache _cache;
    private ResultSession? _session;
    private IReadOnlyList<AlignmentSpotRow> _spots = Array.Empty<AlignmentSpotRow>();
    private IReadOnlyList<SampleInfo> _samples = Array.Empty<SampleInfo>();
    private IReadOnlyList<MoleculeMsReference>? _library;
    private string? _libraryPath;
    private int _gridVersion;
    private readonly Dictionary<int, ReviewPanelViewModel> _allPanels = new();

    public static string[] ZoomModes { get; } = { "Expected window", "Peak", "Full trace" };
    public static string[] MetricColumns { get; } = { "Height", "Area", "RT", "m/z", "S/N" };
    public static string[] MetricXColumns { get; } = { "Analytical order", "Height", "Area", "RT", "m/z", "S/N" };

    public AnalyticsViewModel(RawDataCache cache)
    {
        _cache = cache;
    }

    public ObservableCollection<SpotListItem> SpotItems { get; } = new();
    public ObservableCollection<ReviewPanelViewModel> Panels { get; } = new();

    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private string _summary = "No results loaded. Process the batch or open a project.";
    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private int _annotatedCount;
    [ObservableProperty] private int _unknownCount;
    [ObservableProperty] private SpotListItem? _selectedItem;
    [ObservableProperty] private AlignmentSpotRow? _selectedSpot;
    [ObservableProperty] private string _spotTitle = string.Empty;
    [ObservableProperty] private string _spotDetail = string.Empty;

    // grid options
    [ObservableProperty] private int _columns = 3;
    [ObservableProperty] private int _rows = 2;
    [ObservableProperty] private bool _sameY;
    [ObservableProperty] private bool _linkX = true;
    [ObservableProperty] private string _zoomMode = "Expected window";
    [ObservableProperty] private int _page;
    [ObservableProperty] private string _pageLabel = "0/0";
    [ObservableProperty] private ReviewPanelViewModel? _magnifiedPanel;
    [ObservableProperty] private bool _isGridBusy;
    public int PageCount => Math.Max(1, (int)Math.Ceiling(_allPanels.Count / (double)Math.Max(1, Columns * Rows)));
    /// <summary>Rows actually needed by the current page (no blank rows when there are few samples).</summary>
    public int EffectiveRows => Math.Max(1, Math.Min(Math.Max(1, Rows), (int)Math.Ceiling(Panels.Count / (double)Math.Max(1, Columns))));

    // results tab
    [ObservableProperty] private IReadOnlyList<SampleResultRow> _sampleRows = Array.Empty<SampleResultRow>();
    [ObservableProperty] private SampleResultRow? _selectedSampleRow;

    // spectrum tab
    [ObservableProperty] private IReadOnlyList<Point> _ms2Peaks = Array.Empty<Point>();
    [ObservableProperty] private IReadOnlyList<Point>? _referencePeaks;
    [ObservableProperty] private string _ms2Title = "Representative MS/MS";
    [ObservableProperty] private double _ms2Precursor = double.NaN;

    // statistics tab
    [ObservableProperty] private IReadOnlyList<ClassStatRow> _classStats = Array.Empty<ClassStatRow>();

    // metric plot
    [ObservableProperty] private string _metricY = "Height";
    [ObservableProperty] private string _metricX = "Analytical order";
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _metricPoints = Array.Empty<ScatterPoint>();
    [ObservableProperty] private ScatterPoint? _selectedMetricPoint;

    public string OutputFolder => _session?.Folder ?? string.Empty;
    public ResultSession? Session => _session;

    /// <summary>Set by the shell; the toolbar's Process batch button routes here.</summary>
    public IAsyncRelayCommand? ProcessBatchCommand { get; set; }

    // ---------------------------------------------------------------- session

    public void Clear()
    {
        _session = null;
        _spots = Array.Empty<AlignmentSpotRow>();
        _allPanels.Clear();
        SpotItems.Clear();
        Panels.Clear();
        SelectedSpot = null;
        HasResults = false;
        Summary = "No results loaded. Process the batch or open a project.";
        SampleRows = Array.Empty<SampleResultRow>();
        ClassStats = Array.Empty<ClassStatRow>();
        MetricPoints = Array.Empty<ScatterPoint>();
        Ms2Peaks = Array.Empty<Point>();
        ReferencePeaks = null;
        OnPropertyChanged(nameof(OutputFolder));
    }

    public async Task LoadAsync(ResultSession session)
    {
        Clear();
        _session = session;
        _library = null;
        OnPropertyChanged(nameof(OutputFolder));
        if (session.AlignmentFile is null)
        {
            Summary = $"{session.AnalysisFiles.Count} file(s) processed, no alignment result.";
            HasResults = true;
            return;
        }
        try
        {
            var table = await ResultLoader.LoadAlignmentTableAsync(session.AlignmentFile, session.AnalysisFiles);
            _spots = table.Spots;
            _samples = table.Samples;
            AnnotatedCount = _spots.Count(s => s.IsAnnotated);
            UnknownCount = _spots.Count - AnnotatedCount;
            HasResults = true;
            Summary = $"{_spots.Count} aligned spots across {table.Samples.Count} sample(s) · {AnnotatedCount} annotated";
            RebuildList();
            SelectedItem = SpotItems.FirstOrDefault(i => !i.IsHeader);
        }
        catch (Exception ex)
        {
            HasResults = true;
            Summary = "Alignment could not be loaded: " + ex.Message;
        }
    }

    partial void OnFilterTextChanged(string value) => RebuildList();

    private void RebuildList()
    {
        var f = FilterText?.Trim() ?? string.Empty;
        bool Match(AlignmentSpotRow s) => f.Length == 0 || s.Name.Contains(f, StringComparison.OrdinalIgnoreCase)
            || s.Mz.ToString("F4", CultureInfo.InvariantCulture).Contains(f, StringComparison.Ordinal) || s.Id.ToString(CultureInfo.InvariantCulture) == f
            || s.Adduct.Contains(f, StringComparison.OrdinalIgnoreCase) || s.Ontology.Contains(f, StringComparison.OrdinalIgnoreCase);
        var keep = SelectedSpot;
        SpotItems.Clear();
        var annotated = _spots.Where(s => s.IsAnnotated && Match(s)).OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase).ToList();
        var unknown = _spots.Where(s => !s.IsAnnotated && Match(s)).OrderBy(s => s.Rt).ToList();
        SpotItems.Add(new SpotListItem("Annotated", annotated.Count));
        foreach (var s in annotated) SpotItems.Add(new SpotListItem(s));
        SpotItems.Add(new SpotListItem("Unknown", unknown.Count));
        foreach (var s in unknown) SpotItems.Add(new SpotListItem(s));
        if (keep is not null)
        {
            SelectedItem = SpotItems.FirstOrDefault(i => i.Spot == keep);
        }
    }

    partial void OnSelectedItemChanged(SpotListItem? value)
    {
        if (value is { IsHeader: false, Spot: not null }) SelectedSpot = value.Spot;
    }

    partial void OnSelectedSpotChanged(AlignmentSpotRow? value)
    {
        if (value is null) return;
        SpotTitle = value.IsAnnotated ? value.Name : $"Unknown m/z {value.Mz:F4}";
        var parts = new List<string> { $"spot #{value.Id}", $"RT {value.Rt:F2} min", $"m/z {value.Mz:F4}" };
        if (!string.IsNullOrEmpty(value.Adduct)) parts.Add(value.Adduct);
        if (!string.IsNullOrEmpty(value.Formula)) parts.Add(value.Formula);
        if (!string.IsNullOrEmpty(value.Ontology)) parts.Add(value.Ontology);
        if (value.Score > 0) parts.Add($"score {value.Score:F2}");
        parts.Add($"fill {value.FillPercent:F0} %");
        SpotDetail = string.Join(" · ", parts);
        BuildRows(value);
        BuildStats(value);
        BuildMetric();
        _ = LoadSpectrumAsync(value);
        _ = BuildGridAsync(value);
    }

    // ---------------------------------------------------------------- tabs

    private void BuildRows(AlignmentSpotRow spot)
    {
        var rows = new List<SampleResultRow>();
        var order = 0;
        foreach (var p in spot.SamplePeaks)
        {
            var info = _samples.FirstOrDefault(s => s.FileId == p.FileId);
            rows.Add(new SampleResultRow(p, info?.SampleType ?? p.SampleType, ++order));
        }
        SampleRows = rows;
        SelectedSampleRow = rows.FirstOrDefault();
    }

    private void BuildStats(AlignmentSpotRow spot)
    {
        var stats = new List<ClassStatRow>();
        foreach (var g in spot.SamplePeaks.GroupBy(p => string.IsNullOrEmpty(p.Class) ? "(none)" : p.Class).OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var h = g.Select(p => p.Height).Where(v => !double.IsNaN(v)).ToList();
            var a = g.Select(p => p.Area).Where(v => !double.IsNaN(v)).ToList();
            var mean = h.Count > 0 ? h.Average() : 0;
            var sd = h.Count > 1 ? Math.Sqrt(h.Sum(v => (v - mean) * (v - mean)) / (h.Count - 1)) : 0;
            stats.Add(new ClassStatRow(g.Key, h.Count, mean, sd, mean > 0 ? 100 * sd / mean : 0, h.Count > 0 ? h.Min() : 0, h.Count > 0 ? h.Max() : 0, a.Count > 0 ? a.Average() : 0));
        }
        ClassStats = stats;
    }

    partial void OnMetricYChanged(string value) => BuildMetric();
    partial void OnMetricXChanged(string value) => BuildMetric();

    private static double Metric(AlignedSamplePeak p, string column) => column switch
    {
        "Height" => p.Height,
        "Area" => p.Area,
        "RT" => p.Rt,
        "m/z" => p.Mz,
        "S/N" => p.SignalToNoise,
        _ => double.NaN,
    };

    private void BuildMetric()
    {
        var spot = SelectedSpot;
        if (spot is null) { MetricPoints = Array.Empty<ScatterPoint>(); return; }
        var pts = new List<ScatterPoint>();
        var order = 0;
        foreach (var p in spot.SamplePeaks)
        {
            order++;
            var x = MetricX == "Analytical order" ? order : Metric(p, MetricX);
            var y = Metric(p, MetricY);
            if (double.IsNaN(x)) continue;
            pts.Add(new ScatterPoint(x, y, p.FileName, string.IsNullOrEmpty(p.Class) ? "(none)" : p.Class, p));
        }
        MetricPoints = pts;
        SelectedMetricPoint = pts.FirstOrDefault(p => ReferenceEquals(p.Tag, SelectedSampleRow?.Peak));
    }

    partial void OnSelectedMetricPointChanged(ScatterPoint? value)
    {
        if (value?.Tag is AlignedSamplePeak peak)
        {
            var row = SampleRows.FirstOrDefault(r => ReferenceEquals(r.Peak, peak));
            if (row is not null && row != SelectedSampleRow) SelectedSampleRow = row;
        }
    }

    partial void OnSelectedSampleRowChanged(SampleResultRow? value)
    {
        foreach (var p in _allPanels.Values) p.IsSelected = value is not null && p.Sample.FileId == value.Peak.FileId;
        if (value is not null)
        {
            var pt = MetricPoints.FirstOrDefault(p => ReferenceEquals(p.Tag, value.Peak));
            if (pt is not null && pt != SelectedMetricPoint) SelectedMetricPoint = pt;
            // bring the page holding this sample into view
            var index = SampleRows.ToList().IndexOf(value);
            var size = Math.Max(1, Columns * Rows);
            if (index >= 0 && index / size != Page) Page = index / size;
        }
    }

    public void SelectSample(int fileId)
    {
        var row = SampleRows.FirstOrDefault(r => r.Peak.FileId == fileId);
        if (row is not null) SelectedSampleRow = row;
    }

    private async Task LoadSpectrumAsync(AlignmentSpotRow spot)
    {
        var alignmentFile = _session?.AlignmentFile;
        Ms2Peaks = Array.Empty<Point>();
        ReferencePeaks = null;
        Ms2Precursor = spot.Mz;
        if (alignmentFile is null) return;
        try
        {
            var ms2 = await Task.Run(() => ResultLoader.LoadAlignmentMs2(alignmentFile, spot));
            IReadOnlyList<Point>? reference = null;
            if (spot.IsAnnotated)
            {
                var r = await Task.Run(() => ResultLoader.LoadReferenceSpectrum(_session?.DataBaseMapper, spot.MatchResult)) ?? await FindReferenceAsync(spot.Name, spot.Mz);
                if (r is { Peaks.Count: > 0 }) reference = r.Peaks.Select(p => new Point(p.Mz, p.Intensity)).ToList();
            }
            if (SelectedSpot != spot) return;
            Ms2Peaks = ms2 is null ? Array.Empty<Point>() : ms2.Peaks.Select(p => new Point(p.Mz, p.Intensity)).ToList();
            ReferencePeaks = reference;
            Ms2Title = ms2 is null ? "No representative MS/MS" : "Representative MS/MS · " + ms2.Label + (reference is null ? string.Empty : " · mirror: library reference");
        }
        catch (Exception ex)
        {
            Ms2Title = "MS/MS unavailable: " + ex.Message;
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

    // ---------------------------------------------------------------- review grid

    private double Ms1Tolerance => _session?.Parameter?.CentroidMs1Tolerance is > 0 and < 1 ? _session.Parameter.CentroidMs1Tolerance : 0.01;
    private double RtTolerance => _session?.Parameter?.AlignmentBaseParam?.RetentionTimeAlignmentTolerance is > 0 and < 10 ? _session.Parameter.AlignmentBaseParam.RetentionTimeAlignmentTolerance : 0.1;

    private async Task BuildGridAsync(AlignmentSpotRow spot)
    {
        var version = ++_gridVersion;
        _allPanels.Clear();
        foreach (var p in spot.SamplePeaks)
        {
            var panel = new ReviewPanelViewModel(p) { Title = p.FileName, IsSelected = SelectedSampleRow?.Peak.FileId == p.FileId };
            _allPanels[p.FileId] = panel;
        }
        Page = 0;
        RefreshPage();
        IsGridBusy = true;
        var tol = Ms1Tolerance;
        var mz = spot.Mz;
        try
        {
            foreach (var p in spot.SamplePeaks)
            {
                var bean = _session?.AnalysisFiles.FirstOrDefault(b => b.AnalysisFileId == p.FileId);
                var path = bean?.AnalysisFilePath;
                var panel = _allPanels[p.FileId];
                if (string.IsNullOrEmpty(path)) { panel.Title = $"{p.FileName} · raw file unknown"; continue; }
                try
                {
                    var raw = await _cache.GetAsync(path);
                    if (version != _gridVersion) return;
                    var ms1 = _cache.Channels(path, raw).FirstOrDefault(c => c.Kind == RawChannelKind.Ms1);
                    if (ms1 is null) continue;
                    var eic = await Task.Run(() => RawExplorer.Xic(raw, ms1.SpectrumIndices, mz, tol));
                    if (version != _gridVersion) return;
                    panel.FullTrace = eic.Points.Select(q => new Point(q.Rt, q.Intensity)).ToList();
                    panel.PeakMax = p.HasPeak && !double.IsNaN(p.RtLeft) ? eic.Points.Where(q => q.Rt >= p.RtLeft && q.Rt <= p.RtRight).Select(q => q.Intensity).DefaultIfEmpty(0).Max() : eic.Points.Select(q => q.Intensity).DefaultIfEmpty(0).Max();
                    ApplyPanelView(panel, spot);
                }
                catch (Exception ex)
                {
                    panel.Title = $"{p.FileName} · {ex.Message}";
                }
            }
        }
        finally
        {
            if (version == _gridVersion) IsGridBusy = false;
        }
        ApplyAllViews();
    }

    private void ApplyPanelView(ReviewPanelViewModel panel, AlignmentSpotRow spot)
    {
        var p = panel.Sample;
        var windows = new List<ShadedWindow> { new(spot.Rt - RtTolerance, spot.Rt + RtTolerance, WindowKind.Expected) };
        if (p.HasPeak && !double.IsNaN(p.RtLeft) && !double.IsNaN(p.RtRight)) windows.Add(new ShadedWindow(p.RtLeft, p.RtRight, WindowKind.Integration));
        panel.Windows = windows;
        panel.MarkerRt = p.HasPeak ? p.Rt : double.NaN;
        panel.Series = new[] { new TraceSeries(p.FileName, panel.FullTrace, null, true) };
        var height = p.Height > 0 ? ChartBase.FormatIntensity(p.Height) : "not detected";
        var sn = p.SignalToNoise > 0 ? $" · S/N {p.SignalToNoise:F0}" : string.Empty;
        var gap = p.IsGapFilled ? " · gap-filled" : string.Empty;
        panel.Title = $"{p.FileName} · {height}{sn}{gap}";
        (panel.FixedXMin, panel.FixedXMax) = XRange(spot, p);
    }

    private (double Min, double Max) XRange(AlignmentSpotRow spot, AlignedSamplePeak p)
    {
        switch (ZoomMode)
        {
            case "Full trace":
                return (double.NaN, double.NaN);
            case "Peak" when p.HasPeak && !double.IsNaN(p.RtLeft):
                var w = Math.Max(0.05, p.RtRight - p.RtLeft);
                return (p.RtLeft - w, p.RtRight + w);
            default:
                var half = Math.Max(0.25, RtTolerance * 5);
                return (spot.Rt - half, spot.Rt + half);
        }
    }

    private void ApplyAllViews()
    {
        var spot = SelectedSpot;
        if (spot is null) return;
        var yMax = SameY ? _allPanels.Values.Select(p => p.PeakMax).DefaultIfEmpty(0).Max() : double.NaN;
        foreach (var panel in _allPanels.Values)
        {
            if (panel.FullTrace.Count == 0) continue;
            (panel.FixedXMin, panel.FixedXMax) = LinkX ? XRange(spot, panel.Sample) : XRange(spot, panel.Sample);
            panel.FixedYMax = yMax > 0 ? yMax * 1.05 : double.NaN;
        }
    }

    partial void OnSameYChanged(bool value) => ApplyAllViews();
    partial void OnLinkXChanged(bool value) => ApplyAllViews();
    partial void OnZoomModeChanged(string value) => ApplyAllViews();
    partial void OnColumnsChanged(int value) { Page = 0; RefreshPage(); }
    partial void OnRowsChanged(int value) { Page = 0; RefreshPage(); }
    partial void OnPageChanged(int value) => RefreshPage();

    private void RefreshPage()
    {
        var size = Math.Max(1, Math.Max(1, Columns) * Math.Max(1, Rows));
        var all = _allPanels.Values.ToList();
        var pages = Math.Max(1, (int)Math.Ceiling(all.Count / (double)size));
        if (Page >= pages) { Page = pages - 1; return; }
        if (Page < 0) { Page = 0; return; }
        Panels.Clear();
        foreach (var p in all.Skip(Page * size).Take(size)) Panels.Add(p);
        PageLabel = $"{Page + 1}/{pages}";
        OnPropertyChanged(nameof(PageCount));
        OnPropertyChanged(nameof(EffectiveRows));
    }

    [RelayCommand] private void PreviousPage() => Page = Math.Max(0, Page - 1);
    [RelayCommand] private void NextPage() => Page = Math.Min(PageCount - 1, Page + 1);
    [RelayCommand] private void CloseMagnified() => MagnifiedPanel = null;

    public void Magnify(ReviewPanelViewModel? panel) => MagnifiedPanel = panel;

    // ---------------------------------------------------------------- commands

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (_session is not null) ShellService.Open(_session.Folder);
    }

    /// <summary>The spots currently listed (after the filter), in list order.</summary>
    public IReadOnlyList<AlignmentSpotRow> ListedSpots => SpotItems.Where(i => i.Spot is not null).Select(i => i.Spot!).ToList();

    /// <summary>
    /// Writes the listed spots as an OpenQuant component CSV. The representative deconvoluted MS/MS of each spot
    /// (the same one the Spectrum tab shows) supplies the fragment column. Returns the number of components written.
    /// </summary>
    public async Task<int> ExportOpenQuantAsync(string path, OpenQuantExportOptions options)
    {
        var spots = ListedSpots;
        var alignmentFile = _session?.AlignmentFile;
        return await Task.Run(() =>
        {
            var props = new List<AlignmentSpotProperty>(spots.Count);
            var decs = new List<MSDecResult>(spots.Count);
            foreach (var row in spots)
            {
                var prop = row.Spot ?? new AlignmentSpotProperty
                {
                    MasterAlignmentID = row.Id,
                    AlignmentID = row.Id,
                    Name = row.Name,
                    MassCenter = row.Mz,
                    TimesCenter = new CompMs.Common.Components.ChromXs(row.Rt),
                    Ontology = row.Ontology,
                };
                props.Add(prop);
                MSDecResult? dec = null;
                if (options.UseFragment && alignmentFile is not null)
                {
                    try { dec = ResultLoader.LoadAlignmentMsDec(alignmentFile, row.Id); }
                    catch { dec = null; }
                }
                decs.Add(dec!);
            }
            var components = OpenQuantComponentCsv.FromAlignmentSpots(props, decs, options);
            OpenQuantComponentCsv.Write(path, components);
            return components.Count;
        });
    }

    /// <summary>Writes a height (and area) matrix of all spots × samples as TSV.</summary>
    public async Task ReexportAsync(string path)
    {
        var sb = new StringBuilder();
        sb.Append("Alignment ID\tName\tRT (min)\tm/z\tAdduct\tFormula\tOntology\tScore\tFill %");
        foreach (var s in _samples) sb.Append('\t').Append(s.FileName).Append(" height");
        foreach (var s in _samples) sb.Append('\t').Append(s.FileName).Append(" area");
        sb.Append('\n');
        foreach (var spot in _spots)
        {
            sb.Append(spot.Id).Append('\t').Append(spot.Name).Append('\t').Append(spot.Rt.ToString("F3", CultureInfo.InvariantCulture)).Append('\t')
              .Append(spot.Mz.ToString("F5", CultureInfo.InvariantCulture)).Append('\t').Append(spot.Adduct).Append('\t').Append(spot.Formula).Append('\t').Append(spot.Ontology).Append('\t')
              .Append(spot.Score.ToString("F3", CultureInfo.InvariantCulture)).Append('\t').Append(spot.FillPercent.ToString("F0", CultureInfo.InvariantCulture));
            foreach (var s in _samples)
            {
                var p = spot.SamplePeaks.FirstOrDefault(q => q.FileId == s.FileId);
                sb.Append('\t').Append(p is null ? "" : p.Height.ToString("F0", CultureInfo.InvariantCulture));
            }
            foreach (var s in _samples)
            {
                var p = spot.SamplePeaks.FirstOrDefault(q => q.FileId == s.FileId);
                sb.Append('\t').Append(p is null || double.IsNaN(p.Area) ? "" : p.Area.ToString("F0", CultureInfo.InvariantCulture));
            }
            sb.Append('\n');
        }
        await File.WriteAllTextAsync(path, sb.ToString());
    }
}
