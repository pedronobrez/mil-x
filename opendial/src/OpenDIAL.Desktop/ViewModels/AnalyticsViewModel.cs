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
using OpenDIAL.Pipeline.Curation;
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

/// <summary>One line of the match-score breakdown beside the mirror plot.</summary>
public sealed record ScoreRow(string Metric, string Value);

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
    private CurationStore? _curation;
    private AlignmentResultContainer? _container;
    private MoleculeDataBase? _searchDatabase;
    private IReadOnlyList<AnnotationCandidate> _storedCandidates = Array.Empty<AnnotationCandidate>();
    private List<SpotRowViewModel> _allRows = new();
    private bool _suspendFilter;

    public static string[] ZoomModes { get; } = { "Expected window", "Peak", "Full trace" };
    public static string[] MetricColumns { get; } = { "Height", "Area", "RT", "m/z", "S/N" };
    public static string[] MetricXColumns { get; } = { "Analytical order", "Height", "Area", "RT", "m/z", "S/N" };

    public AnalyticsViewModel(RawDataCache cache)
    {
        _cache = cache;
    }

    public ObservableCollection<ReviewPanelViewModel> Panels { get; } = new();

    /// <summary>The ion table: every aligned feature that passes the filter band, in table order.</summary>
    public ObservableCollection<SpotRowViewModel> IonRows { get; } = new();
    public static string[] TagFilters { get; } = { "All", "Untagged", "Reviewed", "Not reviewed", "Confirmed", "Low quality spectrum", "Misannotation", "Coelution (mixed spectra)", "Overannotation" };
    public static string[] AnnotationFilters { get; } = { "All", "Confident", "Suggested", "Annotated", "Unknown" };
    public static string[] PolarityFilters { get; } = { "All", "Seen in both", "Only in this polarity" };
    public ObservableCollection<string> Ontologies { get; } = new();
    public IReadOnlyList<PeakSpotTagKind> TagKinds { get; } = PeakSpotTagKindExtensions.All;

    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private string _summary = "No results loaded. Process the batch or open a project.";
    [ObservableProperty] private string _filterText = string.Empty;
    [ObservableProperty] private int _annotatedCount;
    [ObservableProperty] private int _unknownCount;
    [ObservableProperty] private AlignmentSpotRow? _selectedSpot;
    [ObservableProperty] private SpotRowViewModel? _selectedRow;

    // ion table filter band
    [ObservableProperty] private string _mzFrom = string.Empty;
    [ObservableProperty] private string _mzTo = string.Empty;
    [ObservableProperty] private string _rtFrom = string.Empty;
    [ObservableProperty] private string _rtTo = string.Empty;
    [ObservableProperty] private string _snFrom = string.Empty;
    [ObservableProperty] private string _blankTo = string.Empty;
    [ObservableProperty] private bool _msmsOnly;
    [ObservableProperty] private bool _molecularIonOnly;
    [ObservableProperty] private bool _manuallyModifiedOnly;
    [ObservableProperty] private string _annotationFilter = "All";
    [ObservableProperty] private string _tagFilter = "All";
    [ObservableProperty] private string _ontologyFilter = "All";
    [ObservableProperty] private string _tableSummary = string.Empty;
    [ObservableProperty] private int _confirmedCount;
    [ObservableProperty] private int _rejectedCount;
    [ObservableProperty] private int _reviewedCount;
    [ObservableProperty] private bool _curationDirty;
    [ObservableProperty] private bool _peaksEdited;
    /// <summary>The ion table is showing in its own window rather than beside the evidence.</summary>
    /// <summary>The evidence area shows two sets of tabs side by side, each on its own tab.</summary>
    [ObservableProperty] private bool _evidenceSplit;
    [ObservableProperty] private int _evidenceTab;
    [ObservableProperty] private int _secondEvidenceTab = 6;   // Statistics, the usual companion to the mirror

    [ObservableProperty] private bool _ionTableDetached;
    /// <summary>The torn-off window is being dragged over the place the table docks into; the place is shown.</summary>
    [ObservableProperty] private bool _ionTableDockPreview;

    /// <summary>Set by the shell: opens or closes the window the ion table can be torn off into.</summary>
    public Action<bool>? RequestDetachIonTable { get; set; }

    [RelayCommand]
    private void ToggleIonTableWindow() => RequestDetachIonTable?.Invoke(!IonTableDetached);

    // manual re-integration
    [ObservableProperty] private string _integrationFrom = string.Empty;
    [ObservableProperty] private string _integrationTo = string.Empty;
    [ObservableProperty] private string _integrationHint = "Shift-drag a panel to set the window.";

    // hand-driven library search
    [ObservableProperty] private string _searchMs1Tolerance = "0.01";
    [ObservableProperty] private string _searchMs2Tolerance = "0.05";
    [ObservableProperty] private string _searchRtTolerance = "0.5";
    [ObservableProperty] private bool _searchUseRt;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _showingSearchResults;
    [ObservableProperty] private string _searchHint = "Widen the tolerances and search the library again for this feature.";

    // candidates of the selected feature
    [ObservableProperty] private IReadOnlyList<AnnotationCandidate> _candidates = Array.Empty<AnnotationCandidate>();
    [ObservableProperty] private AnnotationCandidate? _selectedCandidate;
    [ObservableProperty] private IReadOnlyList<Point> _isotopePeaks = Array.Empty<Point>();
    [ObservableProperty] private IReadOnlyList<ScoreRow> _scoreRows = Array.Empty<ScoreRow>();
    [ObservableProperty] private IReadOnlyList<BarItem> _sampleBars = Array.Empty<BarItem>();
    [ObservableProperty] private IReadOnlyList<ScatterPoint> _featureMap = Array.Empty<ScatterPoint>();
    [ObservableProperty] private ScatterPoint? _selectedFeaturePoint;
    [ObservableProperty] private string _featureMapLabel = string.Empty;
    [ObservableProperty] private string _isotopeTitle = "MS1 isotope pattern";
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

    /// <summary>Every aligned feature, filter or no filter: what the dataset-wide views read.</summary>
    public IReadOnlyList<AlignmentSpotRow> AllSpots => _spots;
    public IReadOnlyList<SampleInfo> Samples => _samples;

    /// <summary>Brings one feature into view by id, clearing the filter when it hides it.</summary>
    public void SelectFeature(int id)
    {
        var row = IonRows.FirstOrDefault(r => r.Id == id);
        if (row is null)
        {
            var hidden = _allRows.FirstOrDefault(r => r.Id == id);
            if (hidden is null) return;
            FilterText = string.Empty;
            AnnotationFilter = "All";
            TagFilter = "All";
            OntologyFilter = "All";
            MsmsOnly = MolecularIonOnly = ManuallyModifiedOnly = false;
            RebuildRows();
            row = IonRows.FirstOrDefault(r => r.Id == id);
        }
        if (row is not null) SelectedRow = row;
    }

    public string OutputFolder => _session?.Folder ?? string.Empty;
    public ResultSession? Session => _session;
    /// <summary>The review of the loaded result: what says which features are confirmed, and where the standards are kept.</summary>
    public CurationStore? Curation => _curation;
    /// <summary>Raised after any change to the review, so the analysis can say its confirmed set is older than the review.</summary>
    public event EventHandler? CurationChanged;

    /// <summary>Set by the shell; the toolbar's Process batch button routes here.</summary>
    public IAsyncRelayCommand? ProcessBatchCommand { get; set; }

    // ---------------------------------------------------------------- session

    public void Clear()
    {
        _session = null;
        _spots = Array.Empty<AlignmentSpotRow>();
        _allPanels.Clear();
        Panels.Clear();
        SelectedSpot = null;
        SelectedRow = null;
        _allRows = new List<SpotRowViewModel>();
        IonRows.Clear();
        _curation = null;
        _container = null;
        _searchDatabase = null;
        _storedCandidates = Array.Empty<AnnotationCandidate>();
        ShowingSearchResults = false;
        CurationDirty = false;
        PeaksEdited = false;
        Candidates = Array.Empty<AnnotationCandidate>();
        IsotopePeaks = Array.Empty<Point>();
        HasResults = false;
        _polarityLink = null;
        _linkedAlignmentPath = null;
        _linkedBean = null;
        _linkedCuration = null;
        _linkedSpots = new Dictionary<int, AlignmentSpotRow>();
        ClearPartnerSpectrum();
        HasPolarityLink = false;
        PolaritySummary = string.Empty;
        PolarityFilter = "All";
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
            _curation = CurationStore.Load(session.AlignmentFile.FilePath);
            _container = table.Container;
            _allRows = _spots.Select(s => new SpotRowViewModel(s, _curation)).ToList();
            GroupIons();
            AnnotatedCount = _spots.Count(s => s.IsAnnotated);
            UnknownCount = _spots.Count - AnnotatedCount;
            HasResults = true;
            Summary = $"{_spots.Count} aligned spots across {table.Samples.Count} sample(s) · {AnnotatedCount} annotated";
            Ontologies.Clear();
            Ontologies.Add("All");
            foreach (var o in _spots.Select(s => s.Ontology).Where(o => !string.IsNullOrWhiteSpace(o)).Distinct().OrderBy(o => o, StringComparer.OrdinalIgnoreCase)) Ontologies.Add(o);
            OntologyFilter = string.Empty;
            OntologyFilter = "All";
            RebuildRows();
            SelectedRow = IonRows.FirstOrDefault();
            await RestorePolarityLinkAsync(session.AlignmentFile.FilePath);
        }
        catch (Exception ex)
        {
            HasResults = true;
            Summary = "Alignment could not be loaded: " + ex.Message;
        }
    }

    /// <summary>
    /// Fills the ion table straight from rows and a curation store, with no processed run behind it.
    /// The headless interface tests use this: they are about the table, the filters and the tagging,
    /// not about reading raw files.
    /// </summary>
    internal void LoadForTest(IReadOnlyList<AlignmentSpotRow> spots, IReadOnlyList<SampleInfo> samples, CurationStore store)
    {
        _spots = spots;
        _samples = samples;
        _curation = store;
        _allRows = spots.Select(s => new SpotRowViewModel(s, store)).ToList();
        AnnotatedCount = spots.Count(s => s.IsAnnotated);
        UnknownCount = spots.Count - AnnotatedCount;
        HasResults = true;
        Ontologies.Clear();
        Ontologies.Add("All");
        foreach (var o in spots.Select(s => s.Ontology).Where(o => !string.IsNullOrWhiteSpace(o)).Distinct().OrderBy(o => o, StringComparer.OrdinalIgnoreCase)) Ontologies.Add(o);
        OntologyFilter = "All";
        RebuildRows();
        SelectedRow = IonRows.FirstOrDefault();
    }

    partial void OnFilterTextChanged(string value) => RebuildRows();
    partial void OnMzFromChanged(string value) => RebuildRows();
    partial void OnMzToChanged(string value) => RebuildRows();
    partial void OnRtFromChanged(string value) => RebuildRows();
    partial void OnRtToChanged(string value) => RebuildRows();
    partial void OnSnFromChanged(string value) => RebuildRows();
    partial void OnBlankToChanged(string value) => RebuildRows();
    partial void OnMsmsOnlyChanged(bool value) => RebuildRows();
    partial void OnMolecularIonOnlyChanged(bool value) => RebuildRows();
    partial void OnManuallyModifiedOnlyChanged(bool value) => RebuildRows();
    partial void OnAnnotationFilterChanged(string value) => RebuildRows();
    partial void OnTagFilterChanged(string value) => RebuildRows();
    partial void OnOntologyFilterChanged(string value) => RebuildRows();
    partial void OnPolarityFilterChanged(string value) => RebuildRows();

    private static double? ParseBound(string s) =>
        double.TryParse(s?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : null;

    /// <summary>
    /// Applies the filter band to the ion table. The selected feature is kept when it survives the
    /// filter, so tightening a filter while reviewing does not throw the reviewer out of place.
    /// </summary>
    public void RebuildRows()
    {
        if (_suspendFilter) return;
        var text = FilterText?.Trim() ?? string.Empty;
        var mzLo = ParseBound(MzFrom);
        var mzHi = ParseBound(MzTo);
        var rtLo = ParseBound(RtFrom);
        var rtHi = ParseBound(RtTo);
        var snLo = ParseBound(SnFrom);
        var blankHi = ParseBound(BlankTo);

        bool Matches(SpotRowViewModel r)
        {
            if (mzLo is not null && r.Mz < mzLo) return false;
            if (mzHi is not null && r.Mz > mzHi) return false;
            if (rtLo is not null && r.Rt < rtLo) return false;
            if (rtHi is not null && r.Rt > rtHi) return false;
            if (snLo is not null && r.SignalToNoise < snLo) return false;
            // a feature the blanks carry as strongly as the samples is background; a batch with no
            // blank reports no percentage, and the filter then keeps everything rather than nothing
            if (blankHi is not null && !double.IsNaN(r.BlankPercent) && r.BlankPercent > blankHi) return false;
            if (RepresentativesOnly && r.Group is { IsRepresentative: false }) return false;
            switch (PolarityFilter)
            {
                case "Seen in both" when !r.SeenInBoth: return false;
                case "Only in this polarity" when r.SeenInBoth: return false;
            }
            if (MsmsOnly && !r.MsmsAssigned) return false;
            if (MolecularIonOnly && !r.IsMolecularIon) return false;
            if (ManuallyModifiedOnly && !r.IsManuallyEdited) return false;
            if (OntologyFilter is not ("All" or "" or null) && !string.Equals(r.Ontology, OntologyFilter, StringComparison.OrdinalIgnoreCase)) return false;
            switch (AnnotationFilter)
            {
                case "Confident" when !r.IsConfident: return false;
                case "Suggested" when r.Level != "suggested" && r.Level != "m/z only": return false;
                case "Annotated" when !r.IsAnnotated: return false;
                case "Unknown" when r.IsAnnotated: return false;
            }
            switch (TagFilter)
            {
                case "Untagged" when r.HasAnyTag: return false;
                case "Reviewed" when !r.Reviewed: return false;
                case "Not reviewed" when r.Reviewed: return false;
                case "Confirmed" when !r.HasTag(PeakSpotTagKind.Confirmed): return false;
                case "Low quality spectrum" when !r.HasTag(PeakSpotTagKind.LowQualitySpectrum): return false;
                case "Misannotation" when !r.HasTag(PeakSpotTagKind.Misannotation): return false;
                case "Coelution (mixed spectra)" when !r.HasTag(PeakSpotTagKind.Coelution): return false;
                case "Overannotation" when !r.HasTag(PeakSpotTagKind.Overannotation): return false;
            }
            if (text.Length == 0) return true;
            return r.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || r.Ontology.Contains(text, StringComparison.OrdinalIgnoreCase)
                || r.Adduct.Contains(text, StringComparison.OrdinalIgnoreCase)
                || r.Formula.Contains(text, StringComparison.OrdinalIgnoreCase)
                || r.Comment.Contains(text, StringComparison.OrdinalIgnoreCase)
                || r.Id.ToString(CultureInfo.InvariantCulture) == text
                || r.Mz.ToString("F4", CultureInfo.InvariantCulture).Contains(text, StringComparison.Ordinal);
        }

        var keep = SelectedRow;
        IonRows.Clear();
        foreach (var r in _allRows)
        {
            if (Matches(r)) IonRows.Add(r);
        }
        if (keep is not null && IonRows.Contains(keep)) SelectedRow = keep;
        else if (SelectedRow is null || !IonRows.Contains(SelectedRow)) SelectedRow = IonRows.FirstOrDefault();
        RefreshCounts();
        BuildFeatureMap();
    }

    /// <summary>
    /// Every filtered feature as a dot in retention time against m/z, coloured by lipid class. Within
    /// one class on a reversed-phase column the dots fall on a line: retention rises with the acyl
    /// carbon number and falls with each double bond, so a member sitting off its class's line is
    /// the first thing to re-examine.
    /// </summary>
    private void BuildFeatureMap()
    {
        var pts = new List<ScatterPoint>(IonRows.Count);
        foreach (var r in IonRows)
        {
            var group = string.IsNullOrWhiteSpace(r.Ontology) ? (r.IsAnnotated ? "(no class)" : "unknown") : r.Ontology;
            pts.Add(new ScatterPoint(r.Rt, r.Mz, r.DisplayName, group, r));
        }
        FeatureMap = pts;
        FeatureMapLabel = $"{pts.Count} feature(s) · retention time against m/z, coloured by class";
        SyncFeatureMapSelection();
    }

    private void SyncFeatureMapSelection()
    {
        var target = FeatureMap.FirstOrDefault(p => ReferenceEquals(p.Tag, SelectedRow));
        if (!ReferenceEquals(target, SelectedFeaturePoint)) SelectedFeaturePoint = target;
    }

    partial void OnPeaksEditedChanged(bool value)
    {
        if (value) CurationDirty = true;
    }

    partial void OnSelectedFeaturePointChanged(ScatterPoint? value)
    {
        if (value?.Tag is SpotRowViewModel row && !ReferenceEquals(row, SelectedRow)) SelectedRow = row;
    }

    private void RefreshCounts()
    {
        ConfirmedCount = _allRows.Count(r => r.HasTag(PeakSpotTagKind.Confirmed));
        RejectedCount = _allRows.Count(r => r.HasTag(PeakSpotTagKind.Misannotation));
        ReviewedCount = _allRows.Count(r => r.Reviewed);
        TableSummary = $"{IonRows.Count} of {_allRows.Count} features · {ReviewedCount} reviewed";
        CurationDirty = _curation?.IsDirty ?? false;
    }

    partial void OnSelectedRowChanged(SpotRowViewModel? value)
    {
        if (value is null)
        {
            Candidates = Array.Empty<AnnotationCandidate>();
            IsotopePeaks = Array.Empty<Point>();
            return;
        }
        value.Refresh();
        SelectedSpot = value.Spot;
        _storedCandidates = value.Spot.Candidates;
        Candidates = _storedCandidates;
        ShowingSearchResults = false;
        SearchHint = "Widen the tolerances and search the library again for this feature.";
        SelectedCandidate = Candidates.FirstOrDefault(c => c.IsRepresentative) ?? Candidates.FirstOrDefault();
        ScoreRows = BuildScoreRows(value.Spot);
        ResetIntegrationWindow();
        SampleBars = value.Spot.SamplePeaks
            .Select(p => new BarItem(p.FileName, double.IsNaN(p.Height) ? 0 : p.Height, string.IsNullOrEmpty(p.Class) ? "(none)" : p.Class))
            .ToList();
        SyncFeatureMapSelection();
        var isotopes = value.Spot.IsotopicPeaks;
        IsotopePeaks = isotopes.Select(i => new Point(i.Mz, i.Intensity)).ToList();
        IsotopeTitle = isotopes.Count == 0
            ? "No MS1 isotope pattern stored for this feature"
            : $"MS1 isotope pattern · {isotopes.Count} ion(s)" + (value.Spot.MonoisotopicPercentage > 0 ? $" · monoisotopic {value.Spot.MonoisotopicPercentage:F0} %" : string.Empty);
    }


    /// <summary>
    /// The numbers behind the reported annotation, in the order a reviewer reads them: how much of
    /// the spectrum matched, how much of the reference was accounted for, and how close the mass was.
    /// </summary>
    private static IReadOnlyList<ScoreRow> BuildScoreRows(AlignmentSpotRow spot)
    {
        var m = spot.MatchResult;
        if (m is null) return new[] { new ScoreRow("No library match", string.Empty) };
        string F(double v) => v.ToString("F3", CultureInfo.InvariantCulture);
        var rows = new List<ScoreRow>
        {
            new("Total score", F(m.TotalScore)),
            new("Dot product", F(m.WeightedDotProduct)),
            new("Reverse dot product", F(m.ReverseDotProduct)),
            new("Simple dot product", F(m.SimpleDotProduct)),
            new("Matched peaks", m.MatchedPeaksCount.ToString("F0", CultureInfo.InvariantCulture)),
            new("Matched peaks %", F(m.MatchedPeaksPercentage)),
            new("Mass similarity", F(m.AcurateMassSimilarity)),
        };
        if (m.RtSimilarity > 0) rows.Add(new ScoreRow("RT similarity", F(m.RtSimilarity)));
        rows.Add(new ScoreRow("Spectrum match", m.IsSpectrumMatch ? "yes" : "no"));
        if (m.IsLipidClassMatch || m.IsLipidChainsMatch || m.IsLipidPositionMatch)
        {
            var level = m.IsLipidPositionMatch ? "sn-position" : m.IsLipidChainsMatch ? "chains" : "class";
            rows.Add(new ScoreRow("Lipid evidence", level));
        }
        return rows;
    }



    // ---------------------------------------------------------------- library re-search

    /// <summary>
    /// Asks the library again for the selected feature, with the reviewer's own tolerances and no
    /// score cut-offs. The run keeps only its best few matches; when the right compound is not among
    /// them this is the way to find it. Scores come from the same annotator the run used.
    /// </summary>
    [RelayCommand]
    private async Task SearchLibrary()
    {
        var row = SelectedRow;
        if (row?.Spot.Spot is null || _session is null) return;
        var options = new LibrarySearchOptions(
            ParseTolerance(SearchMs1Tolerance, 0.01),
            ParseTolerance(SearchMs2Tolerance, 0.05),
            ParseTolerance(SearchRtTolerance, 0.5),
            SearchUseRt);
        IsSearching = true;
        try
        {
            var database = await ResolveSearchDatabaseAsync();
            if (database is null)
            {
                SearchHint = "No library to search: the project carries none and its MSP file is not on this machine.";
                return;
            }
            var scan = _session.AlignmentFile is null ? null : await Task.Run(() => ResultLoader.LoadAlignmentMsDec(_session.AlignmentFile, row.Id));
            var omics = _session.Parameter?.TargetOmics ?? CompMs.Common.Enum.TargetOmics.Metabolomics;
            var spot = row.Spot.Spot;
            var found = await Task.Run(() => LibrarySearcher.Search(database, omics, spot, scan, options));
            if (!ReferenceEquals(SelectedRow, row)) return;
            Candidates = found;
            ShowingSearchResults = true;
            SelectedCandidate = found.FirstOrDefault();
            SearchHint = found.Count == 0
                ? $"Nothing within {options.Ms1Tolerance:F3} Da of m/z {row.Mz:F4} in {database.Id}."
                : $"{found.Count} record(s) within {options.Ms1Tolerance:F3} Da in {database.Id}, best first. Take one with Use this annotation.";
        }
        catch (Exception ex)
        {
            SearchHint = "The search failed: " + ex.Message;
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>Puts the matches the run itself kept back in the list.</summary>
    [RelayCommand]
    private void RestoreCandidates()
    {
        Candidates = _storedCandidates;
        ShowingSearchResults = false;
        SelectedCandidate = Candidates.FirstOrDefault(c => c.IsRepresentative) ?? Candidates.FirstOrDefault();
        SearchHint = "Showing the matches the run kept.";
    }

    private static double ParseTolerance(string? text, double fallback) =>
        double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && v > 0 ? v : fallback;

    /// <summary>
    /// The library to search: the one already in the project when it can be reached, otherwise the
    /// MSP the run was given. Loading a large MSP takes a while, so it is kept for the session.
    /// </summary>
    private async Task<MoleculeDataBase?> ResolveSearchDatabaseAsync()
    {
        if (_searchDatabase is not null) return _searchDatabase;
        _searchDatabase = LibrarySearcher.ResolveDatabase(_session?.DataBaseMapper, _spots);
        if (_searchDatabase is not null) return _searchDatabase;
        var msp = _session?.Parameter?.MspFilePath;
        if (string.IsNullOrWhiteSpace(msp) || !File.Exists(msp)) return null;
        SearchHint = $"Reading {Path.GetFileName(msp)}…";
        _searchDatabase = await LibrarySearcher.LoadAsync(msp);
        return _searchDatabase;
    }

    // ---------------------------------------------------------------- manual peak editing

    /// <summary>Fills the integration boxes from the peak of the sample in focus.</summary>
    [RelayCommand]
    private void ResetIntegrationWindow()
    {
        var peak = SelectedSampleRow?.Peak ?? SelectedRow?.Spot.SamplePeaks.FirstOrDefault(p => p.HasPeak);
        if (peak is null || double.IsNaN(peak.RtLeft) || double.IsNaN(peak.RtRight))
        {
            IntegrationFrom = IntegrationTo = string.Empty;
            return;
        }
        IntegrationFrom = peak.RtLeft.ToString("F3", CultureInfo.InvariantCulture);
        IntegrationTo = peak.RtRight.ToString("F3", CultureInfo.InvariantCulture);
    }

    /// <summary>Called when the reviewer drags a window across one of the review panels.</summary>
    public void SetIntegrationWindow(double from, double to)
    {
        if (double.IsNaN(from) || double.IsNaN(to) || to <= from) return;
        IntegrationFrom = from.ToString("F3", CultureInfo.InvariantCulture);
        IntegrationTo = to.ToString("F3", CultureInfo.InvariantCulture);
        IntegrationHint = $"Window {from:F3}–{to:F3} min. Apply it to this sample or to all of them.";
    }

    private bool TryIntegrationWindow(out double from, out double to)
    {
        from = to = double.NaN;
        if (!double.TryParse(IntegrationFrom?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out from)) return false;
        if (!double.TryParse(IntegrationTo?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out to)) return false;
        return to > from;
    }

    /// <summary>
    /// The chromatograms an integration reads. The review grid loads them one sample at a time, so a
    /// reviewer who acts before it has finished would otherwise re-integrate only the samples already
    /// drawn and leave the rest on their old boundaries. Anything missing is fetched here first.
    /// </summary>
    private async Task<Dictionary<int, IReadOnlyList<ChromatogramPoint>>> ChromatogramsAsync(AlignmentSpotRow spot, IReadOnlyCollection<int>? fileIds)
    {
        var map = new Dictionary<int, IReadOnlyList<ChromatogramPoint>>();
        var wanted = spot.SamplePeaks.Select(p => p.FileId).Where(id => fileIds is null || fileIds.Contains(id)).ToList();
        var missing = new List<int>();
        foreach (var id in wanted)
        {
            if (_allPanels.TryGetValue(id, out var panel) && panel.FullTrace.Count > 0)
            {
                map[id] = panel.FullTrace.Select(p => new ChromatogramPoint(p.X, p.Y)).ToList();
            }
            else
            {
                missing.Add(id);
            }
        }
        if (missing.Count == 0) return map;

        Summary = $"Reading the chromatogram of {missing.Count} sample(s) still loading…";
        var tol = Ms1Tolerance;
        foreach (var id in missing)
        {
            var bean = _session?.AnalysisFiles.FirstOrDefault(b => b.AnalysisFileId == id);
            if (string.IsNullOrEmpty(bean?.AnalysisFilePath)) continue;
            try
            {
                var survey = await _cache.GetMs1Async(bean.AnalysisFilePath);
                var eic = await Task.Run(() => survey.Xic(spot.Mz, tol));
                var points = eic.Points.Select(q => new ChromatogramPoint(q.Rt, q.Intensity)).ToList();
                map[id] = points;
                if (_allPanels.TryGetValue(id, out var panel)) panel.FullTrace = points.Select(q => new Point(q.Rt, q.Intensity)).ToList();
            }
            catch (Exception)
            {
                // a sample whose raw file cannot be read is reported as skipped by the integration
            }
        }
        return map;
    }

    [RelayCommand]
    private Task ReintegrateAll() => ReintegrateAsync(null);

    [RelayCommand]
    private Task ReintegrateSample() => ReintegrateAsync(SelectedSampleRow is null ? null : new[] { SelectedSampleRow.Peak.FileId });

    private async Task ReintegrateAsync(IReadOnlyCollection<int>? fileIds)
    {
        var row = SelectedRow;
        if (row?.Spot.Spot is null) return;
        if (_container is null)
        {
            Summary = "This result was opened from an export, so its peaks cannot be edited.";
            return;
        }
        if (!TryIntegrationWindow(out var from, out var to))
        {
            Summary = "Set a retention window first: shift-drag a panel, or type the two values.";
            return;
        }
        var chromatograms = await ChromatogramsAsync(row.Spot, fileIds);
        if (chromatograms.Count == 0)
        {
            Summary = "No chromatogram could be read for the requested sample(s).";
            return;
        }
        try
        {
            var result = PeakEditor.Reintegrate(row.Spot.Spot, chromatograms, from, to, fileIds);
            PeaksEdited = true;
            var scope = fileIds is null ? "all samples" : SelectedSampleRow?.Peak.FileName ?? "one sample";
            Summary = $"Re-integrated {from:F3}–{to:F3} min over {scope}: {result.SamplesChanged} changed, {result.SamplesSkipped} skipped · mean height {result.HeightAverage:N0} · fill {result.FillPercent:F0} %";
            IntegrationHint = "Edited. Save review writes it back to the alignment files.";
            await ReloadSpotsAsync(row.Id);
        }
        catch (Exception ex)
        {
            Summary = "Re-integration failed: " + ex.Message;
        }
    }

    /// <summary>
    /// Copies the feature so two compounds under one peak can be integrated and named apart. The copy
    /// lands next to the original; give each its own window with the integration controls.
    /// </summary>
    [RelayCommand]
    private async Task SplitIsomer()
    {
        var row = SelectedRow;
        if (row?.Spot.Spot is null || _container is null)
        {
            Summary = "This result was opened from an export, so its features cannot be split.";
            return;
        }
        try
        {
            var clone = PeakEditor.SplitIsomer(_container, row.Spot.Spot);
            PeaksEdited = true;
            Summary = $"Feature #{row.Id} copied to #{clone.MasterAlignmentID}. Give each copy its own integration window, then name them.";
            await ReloadSpotsAsync(clone.MasterAlignmentID);
        }
        catch (Exception ex)
        {
            Summary = "The feature could not be split: " + ex.Message;
        }
    }

    /// <summary>Rebuilds the table from the container after a hand edit, keeping the reviewer in place.</summary>
    private async Task ReloadSpotsAsync(int selectId)
    {
        if (_session?.AlignmentFile is null || _curation is null) return;
        var table = await ResultLoader.LoadAlignmentTableAsync(_session.AlignmentFile, _session.AnalysisFiles, _container);
        _spots = table.Spots;
        _allRows = _spots.Select(s => new SpotRowViewModel(s, _curation)).ToList();
        AnnotatedCount = _spots.Count(s => s.IsAnnotated);
        UnknownCount = _spots.Count - AnnotatedCount;
        RebuildRows();
        var target = IonRows.FirstOrDefault(r => r.Id == selectId);
        if (target is not null) SelectedRow = target;
    }

    // ---------------------------------------------------------------- curation

    /// <summary>Toggles one of MS-DIAL's five review flags on the selected feature.</summary>
    [RelayCommand]
    private void ToggleTag(string? id)
    {
        if (SelectedRow is null || !int.TryParse(id, out var value) || !Enum.IsDefined(typeof(PeakSpotTagKind), value)) return;
        SelectedRow.ToggleTag((PeakSpotTagKind)value);
        AfterCuration();
    }

    [RelayCommand]
    private void ClearTags()
    {
        SelectedRow?.ClearTags();
        AfterCuration();
    }

    /// <summary>Accept the annotation and move on: the common keystroke of a review pass.</summary>
    [RelayCommand]
    private void ConfirmAndNext()
    {
        if (SelectedRow is null) return;
        using (_curation?.Begin("Confirm"))
        {
            SelectedRow.SetTag(PeakSpotTagKind.Misannotation, false);
            SelectedRow.SetTag(PeakSpotTagKind.Confirmed, true);
        }
        AfterCuration();
        NextSpot();
    }

    /// <summary>Reject the annotation and move on.</summary>
    [RelayCommand]
    private void RejectAndNext()
    {
        if (SelectedRow is null) return;
        using (_curation?.Begin("Reject"))
        {
            SelectedRow.SetTag(PeakSpotTagKind.Confirmed, false);
            SelectedRow.SetTag(PeakSpotTagKind.Misannotation, true);
        }
        AfterCuration();
        NextSpot();
    }

    // ---- one compound, several ions -----------------------------------------------------------

    /// <summary>Whether there is a review step to take back, for the shell and for a script.</summary>
    public bool CanUndo => _curation?.CanUndo == true;

    [ObservableProperty] private bool _representativesOnly;
    [ObservableProperty] private string _groupSummary = string.Empty;

    partial void OnRepresentativesOnlyChanged(bool value) => RebuildRows();

    /// <summary>
    /// Gathers the adducts, isotopes, in-source fragments and dimers of one compound behind the ion
    /// the run measured best. An untargeted table reports ions; a reviewer wants compounds, and
    /// until now the only way to see that two rows were one molecule was to notice it.
    /// </summary>
    private void GroupIons()
    {
        try
        {
            var candidates = _allRows.Select(r => new IonCandidate(
                r.Id, r.Rt, r.Mz, r.Height,
                r.Spot.SamplePeaks.Select(p => double.IsNaN(p.Height) ? 0 : p.Height).ToList(),
                r.Adduct, r.Spot.IsotopeWeight)).ToList();
            var positive = !_allRows.Any(r => r.Adduct.EndsWith("-", StringComparison.Ordinal));
            var groups = IonIdentity.Group(candidates, positive);
            var byId = groups.ToDictionary(g => g.Id);
            foreach (var row in _allRows)
            {
                row.Group = byId.TryGetValue(row.Id, out var g) ? g : null;
            }
            var compounds = groups.Count(g => g.IsRepresentative);
            GroupSummary = $"{groups.Count} ion(s) in {compounds} compound group(s)";
        }
        catch (Exception ex)
        {
            GroupSummary = "Ion grouping failed: " + ex.Message;
        }
    }

    // ---- the other polarity ---------------------------------------------------------------------
    // A phospholipid answers in positive and a free fatty acid in negative, so the two runs of one
    // batch are two halves of the same picture. The positive result stays the spine of the review;
    // linking the negative one puts the other half beside it, compound by compound.

    private PolarityLinkResult? _polarityLink;
    private string? _linkedAlignmentPath;
    private AlignmentFileBean? _linkedBean;
    private CurationStore? _linkedCuration;
    private IReadOnlyDictionary<int, AlignmentSpotRow> _linkedSpots = new Dictionary<int, AlignmentSpotRow>();

    /// <summary>The index of the evidence tab that shows the other polarity's spectrum.</summary>
    public const int OtherPolarityTab = 8;

    [ObservableProperty] private bool _tagBothPolarities = true;
    [ObservableProperty] private bool _hasPartner;
    [ObservableProperty] private IReadOnlyList<Point> _partnerPeaks = Array.Empty<Point>();
    [ObservableProperty] private IReadOnlyList<Point>? _partnerMirrorPeaks;
    [ObservableProperty] private double _partnerPrecursor;
    [ObservableProperty] private string _partnerTitle = string.Empty;
    [ObservableProperty] private string _partnerDetail = string.Empty;

    [ObservableProperty] private string _polarityFilter = "All";
    [ObservableProperty] private bool _hasPolarityLink;
    [ObservableProperty] private string _polaritySummary = string.Empty;

    /// <summary>The alignment of the other polarity, once one is linked.</summary>
    public string? LinkedAlignmentPath => _linkedAlignmentPath;

    /// <summary>The pairing itself, for the report and for a script.</summary>
    public PolarityLinkResult? PolarityPairs => _polarityLink;

    /// <summary>The alignment result as it is on disk: the bean carries the ".arf" stem, the file is ".arf2".</summary>
    private static string OnDisk(string alignmentPath)
    {
        if (File.Exists(alignmentPath)) return Path.GetFullPath(alignmentPath);
        return File.Exists(alignmentPath + "2") ? Path.GetFullPath(alignmentPath + "2") : Path.GetFullPath(alignmentPath);
    }

    private static bool Same(string a, string b) =>
        string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

    /// <summary>True when this run is the positive one, as its adducts say.</summary>
    private bool ThisRunIsPositive => !_allRows.Any(r => r.Adduct.EndsWith("-", StringComparison.Ordinal));

    /// <summary>
    /// Reconciles this result with the same batch run in the other polarity: the compounds are
    /// paired on the neutral molecule, and the pairing is written beside the alignment so it is
    /// there again next time the result is opened.
    /// </summary>
    public async Task<string> LinkPolarityAsync(string alignmentPath, PolarityLinkOptions? options = null, bool save = true)
    {
        if (string.IsNullOrWhiteSpace(alignmentPath) || !File.Exists(alignmentPath)) return $"{alignmentPath} was not found.";
        try
        {
            var other = await ResultLoader.LoadAlignmentFromFileAsync(alignmentPath);
            var bean = ResultLoader.BeanFor(alignmentPath);
            var sentence = LinkPolarity(other, options);
            if (!HasPolarityLink) return sentence;
            _linkedAlignmentPath = Path.GetFullPath(alignmentPath);
            // the other run's spectra and its review, so a pair can be read and judged as one compound
            _linkedBean = bean;
            _linkedCuration = CurationStore.Load(bean.FilePath);
            SecondEvidenceTab = OtherPolarityTab;
            if (SelectedRow is not null) SpectrumReady = LoadSpectrumAsync(SelectedRow.Spot);

            if (save && _session?.AlignmentFile is not null)
            {
                options ??= new PolarityLinkOptions();
                var ours = OnDisk(_session.AlignmentFile.FilePath);
                var here = ThisRunIsPositive;
                var positiveAlignment = here ? ours : _linkedAlignmentPath;
                var negativeAlignment = here ? _linkedAlignmentPath : ours;
                PolarityPairFile.Save(PolarityPairFile.FileFor(positiveAlignment), _polarityLink!, positiveAlignment, negativeAlignment, options);
            }
            return sentence;
        }
        catch (Exception ex)
        {
            UnlinkPolarity();
            return "The polarities could not be linked: " + ex.Message;
        }
    }

    /// <summary>
    /// The pairing itself, against an alignment already in memory. Separate from the file so the
    /// decision — which run is which polarity, what pairs with what — can be exercised without one.
    /// </summary>
    public string LinkPolarity(AlignmentTable other, PolarityLinkOptions? options = null)
    {
        if (_allRows.Count == 0) return "Open a result before linking the other polarity.";
        if (other.Spots.Count == 0) return "That alignment has no features.";
        options ??= new PolarityLinkOptions();

        var here = ThisRunIsPositive;
        var there = !other.Spots.Any(s => s.Adduct.EndsWith("-", StringComparison.Ordinal));
        if (here == there)
        {
            return $"Both results look like the {(here ? "positive" : "negative")} mode; link the run of the other polarity.";
        }

        var mine = new AlignmentTable(_samples, _spots, "session");
        var result = here
            ? PolarityLink.Link(mine, other, options)
            : PolarityLink.Link(other, mine, options);

        Attach(result, other, here);
        _polarityLink = result;
        HasPolarityLink = true;
        PolaritySummary = result.Sentence();
        RebuildRows();
        return PolaritySummary;
    }

    /// <summary>Puts the pairing on the rows it belongs to, with enough of the partner to print.</summary>
    private void Attach(PolarityLinkResult result, AlignmentTable other, bool thisRunIsPositive)
    {
        var partner = other.Spots.ToDictionary(s => s.Id);
        _linkedSpots = partner;
        var byMine = new Dictionary<int, PolarityPair>();
        foreach (var pair in result.Pairs)
        {
            byMine[thisRunIsPositive ? pair.PositiveId : pair.NegativeId] = pair;
        }
        foreach (var row in _allRows)
        {
            if (byMine.TryGetValue(row.Id, out var pair) &&
                partner.TryGetValue(thisRunIsPositive ? pair.NegativeId : pair.PositiveId, out var spot))
            {
                row.Polarity = new PolarityState(thisRunIsPositive, pair, spot.Mz, spot.Rt, spot.SignalToNoiseAverage, spot.Name);
            }
            else
            {
                row.Polarity = new PolarityState(thisRunIsPositive, null, double.NaN, double.NaN, double.NaN, string.Empty);
            }
            row.RaisePolarityChanged();
        }
    }

    /// <summary>
    /// Links a polarity that is already in memory, with the review that belongs to it. The headless
    /// interface tests use this: they are about the pairing and the tagging, not about reading files.
    /// </summary>
    internal string LinkPolarityForTest(AlignmentTable other, CurationStore partnerCuration, PolarityLinkOptions? options = null)
    {
        var sentence = LinkPolarity(other, options);
        if (HasPolarityLink)
        {
            _linkedCuration = partnerCuration;
            SecondEvidenceTab = OtherPolarityTab;
        }
        return sentence;
    }

    /// <summary>Forgets the other polarity; the sidecar on disk is left where it is.</summary>
    public void UnlinkPolarity()
    {
        _polarityLink = null;
        _linkedAlignmentPath = null;
        _linkedBean = null;
        _linkedCuration = null;
        _linkedSpots = new Dictionary<int, AlignmentSpotRow>();
        ClearPartnerSpectrum();
        HasPolarityLink = false;
        PolaritySummary = string.Empty;
        PolarityFilter = "All";
        foreach (var row in _allRows)
        {
            row.Polarity = null;
            row.RaisePolarityChanged();
        }
        RebuildRows();
    }

    /// <summary>
    /// Picks up a pairing made earlier for this alignment, so a linked result opens linked. The
    /// pairing is made again from the two alignments rather than trusted from the file: the ids in
    /// it mean nothing if either result has been processed again since.
    /// </summary>
    private async Task RestorePolarityLinkAsync(string alignmentPath)
    {
        try
        {
            var ours = OnDisk(alignmentPath);
            var document = PolarityPairFile.Load(PolarityPairFile.FileFor(ours));
            // the sidecar is named after the positive alignment, which is the other run when this
            // one is the negative: look through the ones in the folder for the pair we belong to
            if (document is null)
            {
                foreach (var candidate in Directory.EnumerateFiles(Path.GetDirectoryName(ours) ?? ".", "*_polarity-pairs.json"))
                {
                    var read = PolarityPairFile.Load(candidate);
                    if (read is null) continue;
                    if (Same(read.PositiveAlignment, ours) || Same(read.NegativeAlignment, ours)) { document = read; break; }
                }
            }
            if (document is null) return;
            var other = Same(document.PositiveAlignment, ours) ? document.NegativeAlignment : document.PositiveAlignment;
            if (!File.Exists(other)) return;
            await LinkPolarityAsync(other, new PolarityLinkOptions
            {
                RtTolerance = document.RtTolerance,
                MassTolerance = document.MassTolerance,
                MinimumCorrelation = document.MinimumCorrelation,
                MinimumScore = document.MinimumScore,
            }, save: false);
        }
        catch (Exception)
        {
            // a pairing that cannot be restored is not a reason to fail opening the result
        }
    }

    /// <summary>
    /// What this run named, and when those compounds actually eluted: the pairs a library's
    /// retention times are calibrated against. Only the confident names, and only one time per
    /// name — the first, which is the strongest since the table arrives sorted by id.
    /// </summary>
    public IReadOnlyDictionary<string, double> NamesAndRetentionTimes()
    {
        var found = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in _allRows)
        {
            if (!row.IsConfident) continue;
            var name = row.Name;
            if (name.Length == 0 || found.ContainsKey(name)) continue;
            found[name] = row.Rt;
        }
        return found;
    }

    /// <summary>
    /// The rows as the table is showing them, which is not the order they were built in: the reviewer
    /// sorts by S/N, by score, by class, and then expects Confirm ▸ to walk down the screen. The view
    /// hands this over; with no view — the headless tests, a script — it is the collection's own order.
    /// </summary>
    internal Func<IReadOnlyList<SpotRowViewModel>>? RowsAsShown { get; set; }

    private IReadOnlyList<SpotRowViewModel> RowsInOrder()
    {
        var shown = RowsAsShown?.Invoke();
        // the view's order is only trusted while it holds the same rows as the filter
        return shown is { Count: > 0 } && shown.Count == IonRows.Count ? shown : IonRows;
    }

    [RelayCommand]
    private void NextSpot()
    {
        var rows = RowsInOrder();
        if (rows.Count == 0) return;
        var i = SelectedRow is null ? -1 : IndexOf(rows, SelectedRow);
        SelectedRow = rows[Math.Min(rows.Count - 1, i + 1)];
    }

    [RelayCommand]
    private void PreviousSpot()
    {
        var rows = RowsInOrder();
        if (rows.Count == 0) return;
        var i = SelectedRow is null ? rows.Count : IndexOf(rows, SelectedRow);
        SelectedRow = rows[Math.Max(0, i - 1)];
    }

    /// <summary>Jumps to the next feature nobody has looked at, skipping what is already decided.</summary>
    [RelayCommand]
    private void NextUnreviewed()
    {
        var rows = RowsInOrder();
        if (rows.Count == 0) return;
        var start = SelectedRow is null ? -1 : IndexOf(rows, SelectedRow);
        for (var k = 1; k <= rows.Count; k++)
        {
            var row = rows[(start + k + rows.Count) % rows.Count];
            if (!row.Reviewed) { SelectedRow = row; return; }
        }
        Summary = "Every feature in the current filter has been reviewed.";
    }

    private static int IndexOf(IReadOnlyList<SpotRowViewModel> rows, SpotRowViewModel row)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (ReferenceEquals(rows[i], row)) return i;
        }
        return -1;
    }

    /// <summary>Replaces the automatic annotation with the candidate the reviewer picked.</summary>
    [RelayCommand]
    private void UseSelectedCandidate()
    {
        if (SelectedRow is null || SelectedCandidate is null) return;
        SelectedRow.SetManualName(SelectedCandidate.Name);
        AfterCuration();
    }

    /// <summary>Drops a hand-picked annotation and shows what the run produced again.</summary>
    [RelayCommand]
    private void ResetAnnotation()
    {
        if (SelectedRow is null) return;
        SelectedRow.SetManualName(string.Empty);
        AfterCuration();
    }

    /// <summary>
    /// Tags every feature the filter is currently showing. Reviewing a lipid class is usually a
    /// matter of filtering to it, checking the retention-time trend, and accepting the whole set.
    /// </summary>
    [RelayCommand]
    private void ConfirmAllShown()
    {
        using var step = _curation?.Begin("Confirm all shown");
        foreach (var r in RowsInOrder())
        {
            r.SetTag(PeakSpotTagKind.Misannotation, false);
            r.SetTag(PeakSpotTagKind.Confirmed, true);
        }
        Summary = $"{IonRows.Count} feature(s) tagged Confirmed.";
        AfterCuration();
    }

    /// <summary>The other half of the pair: a class that turned out to be noise is rejected in one go.</summary>
    [RelayCommand]
    private void RejectAllShown()
    {
        using var step = _curation?.Begin("Reject all shown");
        foreach (var r in RowsInOrder())
        {
            r.SetTag(PeakSpotTagKind.Confirmed, false);
            r.SetTag(PeakSpotTagKind.Misannotation, true);
        }
        Summary = $"{IonRows.Count} feature(s) tagged Misannotation.";
        AfterCuration();
    }

    [RelayCommand]
    private void ClearAllShown()
    {
        using var step = _curation?.Begin("Clear all shown");
        foreach (var r in RowsInOrder()) r.ClearTags();
        Summary = $"Tags removed from {IonRows.Count} feature(s).";
        AfterCuration();
    }

    /// <summary>
    /// Takes the last decision back — one tag, or a whole Confirm all shown. What it undoes is said
    /// in the status line, because a keystroke that silently changes two thousand rows is worse
    /// than no keystroke at all.
    /// </summary>
    [RelayCommand]
    private void Undo()
    {
        if (_curation is null || !_curation.CanUndo) { Summary = "Nothing to undo."; return; }
        var what = _curation.Undo();
        RefreshAfterUndo($"Undone: {what}.");
    }

    [RelayCommand]
    private void Redo()
    {
        if (_curation is null || !_curation.CanRedo) { Summary = "Nothing to redo."; return; }
        var what = _curation.Redo();
        RefreshAfterUndo($"Redone: {what}.");
    }

    private void RefreshAfterUndo(string message)
    {
        foreach (var row in _allRows) row.Refresh();
        CurationDirty = true;
        AfterCuration();
        RebuildRows();
        Summary = message;
    }

    [RelayCommand]
    private void SaveCuration()
    {
        if (_curation is null) return;
        try
        {
            _curation.Save();
            CurationDirty = false;
            var message = $"Review saved to {Path.GetFileName(_curation.TagFilePath)} (MS-DIAL reads this file too).";
            if (_linkedCuration is { IsDirty: true })
            {
                _linkedCuration.Save();
                message += $" The other polarity's review went to {Path.GetFileName(_linkedCuration.TagFilePath)}.";
            }
            if (PeaksEdited && _container is not null && _session?.AlignmentFile is not null)
            {
                PeakEditor.Save(_container, _session.AlignmentFile);
                PeaksEdited = false;
                message += " Edited peaks written back to the alignment files; the originals are kept beside them as .before-curation.";
            }
            Summary = message;
        }
        catch (Exception ex)
        {
            Summary = "Review could not be saved: " + ex.Message;
        }
    }

    /// <summary>Writes the review out if anything changed; called when the workspace is left.</summary>
    public void FlushCuration()
    {
        if (_curation is { IsDirty: true }) SaveCuration();
    }

    private void AfterCuration()
    {
        MirrorCurationToPartner();
        RefreshCounts();
        CurationChanged?.Invoke(this, EventArgs.Empty);
        // a tag filter is a moving target while tagging: re-apply it so the list stays honest
        if (TagFilter != "All") RebuildRows();
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
        SpectrumReady = LoadSpectrumAsync(value);
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

    /// <summary>
    /// The load of the selected feature's product spectrum and its library mirror, which run off the
    /// interface thread. A script that selects a feature and asks what the panel shows has to wait
    /// for this; a person waits by watching it appear.
    /// </summary>
    internal Task SpectrumReady { get; private set; } = Task.CompletedTask;

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
            await LoadPartnerSpectrumAsync(spot, Ms2Peaks);
        }
        catch (Exception ex)
        {
            Ms2Title = "MS/MS unavailable: " + ex.Message;
        }
    }

    private void ClearPartnerSpectrum()
    {
        HasPartner = false;
        PartnerPeaks = Array.Empty<Point>();
        PartnerMirrorPeaks = null;
        PartnerTitle = string.Empty;
        PartnerDetail = string.Empty;
    }

    /// <summary>
    /// The same compound as the other polarity measured it, mirrored under this run's spectrum.
    ///
    /// The two halves are not expected to match peak for peak — a protonated molecule and a
    /// deprotonated one fall apart differently — so this is not a score. It is the thing a reviewer
    /// wants on screen when deciding whether an identification is real: the same molecule,
    /// fragmented twice, at the same retention time.
    /// </summary>
    private async Task LoadPartnerSpectrumAsync(AlignmentSpotRow spot, IReadOnlyList<Point> ownPeaks)
    {
        ClearPartnerSpectrum();
        var row = _allRows.FirstOrDefault(r => r.Id == spot.Id);
        if (row?.Polarity?.Pair is not { } pair || _linkedBean is null) return;
        var partnerId = row.Polarity.OwnIsPositive ? pair.NegativeId : pair.PositiveId;
        if (!_linkedSpots.TryGetValue(partnerId, out var partner)) return;

        var here = row.Polarity.OwnIsPositive ? "positive" : "negative";
        var there = row.Polarity.OwnIsPositive ? "negative" : "positive";
        try
        {
            var ms2 = await Task.Run(() => ResultLoader.LoadAlignmentMs2(_linkedBean, partner));
            if (SelectedSpot != spot) return;
            HasPartner = true;
            PartnerPrecursor = partner.Mz;
            PartnerPeaks = ownPeaks;
            PartnerMirrorPeaks = ms2 is null ? null : ms2.Peaks.Select(p => new Point(p.Mz, p.Intensity)).ToList();
            PartnerTitle = ms2 is null
                ? $"#{partner.Id} in the {there} run carries no MS/MS"
                : $"This run ({here}) above · #{partner.Id} in the {there} run below";
            var name = partner.IsAnnotated ? partner.Name : "no name in that run";
            var correlation = double.IsNaN(pair.Correlation) ? "no shared injections" : $"r {pair.Correlation:0.00}";
            PartnerDetail =
                $"{name} · m/z {partner.Mz.ToString("F4", CultureInfo.InvariantCulture)} {partner.Adduct} · " +
                $"RT {partner.Rt.ToString("F2", CultureInfo.InvariantCulture)} min · S/N {partner.SignalToNoiseAverage:F0} · " +
                $"{correlation} · neutral {pair.NeutralMass.ToString("F4", CultureInfo.InvariantCulture)} · " +
                $"quantified from the {(pair.Quantify == PolarityChoice.Positive ? "positive" : "negative")} run";
        }
        catch (Exception ex)
        {
            HasPartner = true;
            PartnerTitle = "The other polarity's MS/MS could not be read: " + ex.Message;
        }
    }

    /// <summary>
    /// Carries the verdict across the pair. A compound seen in both polarities is one compound, so
    /// confirming it in one run confirms it in the other; the tags of every paired feature are
    /// copied to their partner after each change. Copying the whole state rather than each edit is
    /// what makes undo work across the two reviews without a second undo stack: taking a decision
    /// back here re-copies the state that was restored.
    /// </summary>
    private void MirrorCurationToPartner()
    {
        if (!TagBothPolarities || _linkedCuration is null || _curation is null) return;
        foreach (var row in _allRows)
        {
            if (row.Polarity?.Pair is not { } pair) continue;
            var partnerId = row.Polarity.OwnIsPositive ? pair.NegativeId : pair.PositiveId;
            var mine = _curation.TagsOf(row.Id);
            var theirs = _linkedCuration.TagsOf(partnerId);
            if (mine.Count == 0 && theirs.Count == 0) continue;
            if (mine.Count == theirs.Count && !mine.Except(theirs).Any()) continue;
            _linkedCuration.ClearTags(partnerId);
            foreach (var tag in mine) _linkedCuration.SetTag(partnerId, tag, true);
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
                    var survey = await _cache.GetMs1Async(path);
                    if (version != _gridVersion) return;
                    var eic = await Task.Run(() => survey.Xic(mz, tol));
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

    /// <summary>
    /// Writes the reviewed table as the ion table is showing it: the filter decides which features
    /// go, and the tags, comments and hand edits go with them. MS-DIAL's own export folds tags into
    /// the comment and cannot express "reviewed" at all, so these are columns of their own.
    /// </summary>
    public Task<int> ExportReviewedTableAsync(string path, bool areas)
    {
        var options = new CurationExportOptions(areas ? ExportValue.Area : ExportValue.Height);
        return CurationExporter.WriteFileAsync(path, ListedSpots, _samples, _curation, options);
    }

    /// <summary>The features the ion table is currently showing, in table order: what an export writes.</summary>
    public IReadOnlyList<AlignmentSpotRow> ListedSpots => IonRows.Select(r => r.Spot).ToList();

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
