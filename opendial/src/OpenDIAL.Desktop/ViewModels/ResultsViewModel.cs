using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompMs.Common.Components;
using CompMs.Common.DataObj;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.Parameter;
using OpenDIAL.Desktop.Controls;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Pipeline.Model;
using OpenDIAL.Pipeline.Results;

namespace OpenDIAL.Desktop.ViewModels;

public sealed class ResultFileItem
{
    public ResultFileItem(AnalysisFileBean bean)
    {
        Bean = bean;
    }

    public AnalysisFileBean Bean { get; }
    public string Name => Bean.AnalysisFileName;
    public string Class => Bean.AnalysisFileClass;
    public override string ToString() => $"{Name}  ({Class})";
}

/// <summary>Everything the Results page needs, independent of whether it came from a run or from an opened project.</summary>
public sealed record ResultSession(
    string Folder,
    IReadOnlyList<AnalysisFileBean> AnalysisFiles,
    AlignmentFileBean? AlignmentFile,
    ParameterBase? Parameter,
    DataBaseMapper? DataBaseMapper,
    IReadOnlyList<ExportedFile> ExportedFiles,
    string? ProjectFilePath,
    IonizationMode Mode)
{
    public static ResultSession From(PipelineResult r) => new(r.OutputFolder, r.AnalysisFiles, r.AlignmentFile, r.Parameter, r.DataBaseMapper, r.ExportedFiles, r.ProjectFilePath, r.Mode);
    public static ResultSession From(OpenedProject p) => new(p.Folder, p.AnalysisFiles, p.AlignmentFile, p.Parameter, p.DataBaseMapper, p.ExportedFiles, p.ProjectFilePath, p.Mode);
}

public sealed partial class ResultsViewModel : ViewModelBase
{
    private ResultSession? _session;
    private readonly Dictionary<string, RawMeasurement> _rawCache = new(StringComparer.OrdinalIgnoreCase);
    private IReadOnlyList<MoleculeMsReference>? _library;
    private string? _libraryPath;
    private int _peakLoadVersion;
    private int _detailVersion;

    public ObservableCollection<ResultFileItem> Files { get; } = new();
    public ObservableCollection<ExportedFile> Exports { get; } = new();

    [ObservableProperty] private bool _hasSession;
    [ObservableProperty] private string _sessionTitle = "No results loaded";
    [ObservableProperty] private string _statusText = "Run the pipeline or open an existing output folder.";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private ResultFileItem? _selectedFile;
    [ObservableProperty] private IReadOnlyList<PeakFeatureRow> _peaks = Array.Empty<PeakFeatureRow>();
    [ObservableProperty] private PeakFeatureRow? _selectedPeak;
    [ObservableProperty] private string _peakSummary = string.Empty;

    // EIC chart
    [ObservableProperty] private IReadOnlyList<Point> _eicPoints = Array.Empty<Point>();
    [ObservableProperty] private double _eicHighlightStart = double.NaN;
    [ObservableProperty] private double _eicHighlightEnd = double.NaN;
    [ObservableProperty] private double _eicMarker = double.NaN;
    [ObservableProperty] private string _eicTitle = "EIC";

    // MS/MS chart
    [ObservableProperty] private IReadOnlyList<Point> _ms2Peaks = Array.Empty<Point>();
    [ObservableProperty] private IReadOnlyList<Point>? _referencePeaks;
    [ObservableProperty] private string _ms2Title = "MS/MS";
    [ObservableProperty] private double _ms2Precursor = double.NaN;

    // alignment
    [ObservableProperty] private IReadOnlyList<AlignmentSpotRow> _alignmentSpots = Array.Empty<AlignmentSpotRow>();
    [ObservableProperty] private AlignmentSpotRow? _selectedSpot;
    [ObservableProperty] private IReadOnlyList<BarItem> _barItems = Array.Empty<BarItem>();
    [ObservableProperty] private string _barTitle = "Per-sample height";
    [ObservableProperty] private IReadOnlyList<Point> _alignmentMs2Peaks = Array.Empty<Point>();
    [ObservableProperty] private string _alignmentMs2Title = "Representative MS/MS";
    [ObservableProperty] private string _alignmentSummary = "No alignment result.";
    [ObservableProperty] private bool _hasAlignment;

    public string OutputFolder => _session?.Folder ?? string.Empty;

    // ---------------------------------------------------------------- session

    public async Task LoadSessionAsync(ResultSession session)
    {
        _session = session;
        _rawCache.Clear();
        _library = null;
        _libraryPath = null;

        Files.Clear();
        foreach (var f in session.AnalysisFiles)
        {
            Files.Add(new ResultFileItem(f));
        }
        Exports.Clear();
        foreach (var e in session.ExportedFiles)
        {
            Exports.Add(e);
        }
        HasSession = true;
        SessionTitle = $"{Path.GetFileName(session.Folder.TrimEnd(Path.DirectorySeparatorChar))}  —  {session.AnalysisFiles.Count} file(s), {session.Mode}";
        OnPropertyChanged(nameof(OutputFolder));
        ClearPeakDetails();

        SelectedFile = Files.FirstOrDefault();
        await LoadAlignmentAsync();
    }

    partial void OnSelectedFileChanged(ResultFileItem? value)
    {
        _ = LoadPeaksAsync(value);
    }

    private async Task LoadPeaksAsync(ResultFileItem? file)
    {
        var version = ++_peakLoadVersion;
        SelectedPeak = null;
        ClearPeakDetails();
        if (file is null)
        {
            Peaks = Array.Empty<PeakFeatureRow>();
            return;
        }
        IsBusy = true;
        StatusText = $"Loading peaks of {file.Name}…";
        try
        {
            var rows = await ResultLoader.LoadPeakTableAsync(file.Bean);
            if (version != _peakLoadVersion) return;
            Peaks = rows;
            PeakSummary = $"{rows.Count} peaks, {rows.Count(r => r.IsAnnotated)} annotated, {rows.Count(r => r.HasMs2)} with MS/MS";
            StatusText = $"{file.Name}: {PeakSummary}";
            SelectedPeak = rows.FirstOrDefault(r => r.IsAnnotated) ?? rows.FirstOrDefault();
        }
        catch (Exception ex)
        {
            if (version != _peakLoadVersion) return;
            Peaks = Array.Empty<PeakFeatureRow>();
            StatusText = $"Could not load peaks: {ex.Message}";
        }
        finally
        {
            if (version == _peakLoadVersion) IsBusy = false;
        }
    }

    private void ClearPeakDetails()
    {
        EicPoints = Array.Empty<Point>();
        EicHighlightStart = double.NaN;
        EicHighlightEnd = double.NaN;
        EicMarker = double.NaN;
        EicTitle = "EIC";
        Ms2Peaks = Array.Empty<Point>();
        ReferencePeaks = null;
        Ms2Title = "MS/MS";
        Ms2Precursor = double.NaN;
    }

    partial void OnSelectedPeakChanged(PeakFeatureRow? value)
    {
        _ = LoadPeakDetailsAsync(value);
    }

    private double Ms1Tolerance => _session?.Parameter?.CentroidMs1Tolerance is > 0 and < 1 ? _session.Parameter.CentroidMs1Tolerance : 0.01;

    private async Task LoadPeakDetailsAsync(PeakFeatureRow? peak)
    {
        var version = ++_detailVersion;
        if (peak is null || SelectedFile is null || _session is null)
        {
            ClearPeakDetails();
            return;
        }
        var file = SelectedFile.Bean;

        // MS/MS (fast, from the .dcl)
        try
        {
            var ms2 = await Task.Run(() => ResultLoader.LoadMs2(file, peak));
            if (version != _detailVersion) return;
            Ms2Peaks = ms2 is null ? Array.Empty<Point>() : ToPoints(ms2);
            Ms2Precursor = peak.Mz;
            Ms2Title = ms2 is null ? $"No deconvoluted MS/MS for peak {peak.Id}" : $"Deconvoluted MS/MS — {ms2.Label}";
        }
        catch (Exception ex)
        {
            if (version != _detailVersion) return;
            Ms2Peaks = Array.Empty<Point>();
            Ms2Title = "MS/MS unavailable: " + ex.Message;
        }

        // reference spectrum for the mirror plot
        ReferencePeaks = null;
        if (peak.IsAnnotated)
        {
            try
            {
                var reference = await Task.Run(() => ResultLoader.LoadReferenceSpectrum(_session.DataBaseMapper, peak.MatchResult));
                if (reference is null)
                {
                    reference = await FindReferenceInLibraryAsync(peak.Name, peak.Mz);
                }
                if (version != _detailVersion) return;
                if (reference is not null && reference.Peaks.Count > 0)
                {
                    ReferencePeaks = ToPoints(reference);
                    Ms2Title += "  |  mirror: " + reference.Label;
                }
            }
            catch
            {
                // reference is optional
            }
        }

        // EIC (needs the raw file; cached per file)
        EicTitle = "Loading EIC…";
        try
        {
            var raw = await GetRawAsync(file);
            if (version != _detailVersion) return;
            var tol = Ms1Tolerance;
            var eic = await Task.Run(() => ResultLoader.LoadEic(raw, peak.Mz, tol));
            if (version != _detailVersion) return;
            EicPoints = eic.Points.Select(p => new Point(p.Rt, p.Intensity)).ToList();
            EicHighlightStart = peak.RtLeft;
            EicHighlightEnd = peak.RtRight;
            EicMarker = peak.Rt;
            EicTitle = $"{eic.Label} — {SelectedFile.Name}";
        }
        catch (Exception ex)
        {
            if (version != _detailVersion) return;
            EicPoints = Array.Empty<Point>();
            EicTitle = "EIC unavailable: " + ex.Message;
        }
    }

    private async Task<RawMeasurement> GetRawAsync(AnalysisFileBean file)
    {
        var key = file.AnalysisFilePath;
        if (_rawCache.TryGetValue(key, out var raw))
        {
            return raw;
        }
        if (string.IsNullOrEmpty(key) || (!File.Exists(key) && !Directory.Exists(key)))
        {
            throw new FileNotFoundException("Raw data file not found (was the project folder moved?)", key);
        }
        StatusText = $"Reading raw data {Path.GetFileName(key)}…";
        raw = await ResultLoader.LoadRawMeasurementAsync(file);
        _rawCache[key] = raw;
        StatusText = $"{file.AnalysisFileName}: {raw.SpectrumList.Count} spectra loaded";
        return raw;
    }

    private async Task<MsSpectrum?> FindReferenceInLibraryAsync(string name, double mz)
    {
        var mspPath = _session?.Parameter?.MspFilePath;
        if (string.IsNullOrWhiteSpace(mspPath) || !File.Exists(mspPath))
        {
            return null;
        }
        if (_library is null || !string.Equals(_libraryPath, mspPath, StringComparison.OrdinalIgnoreCase))
        {
            _library = await ResultLoader.LoadMspLibraryAsync(mspPath);
            _libraryPath = mspPath;
        }
        var reference = ResultLoader.FindReference(_library, name, mz);
        return reference is null ? null : ResultLoader.ToSpectrum(reference);
    }

    private static IReadOnlyList<Point> ToPoints(MsSpectrum s) => s.Peaks.Select(p => new Point(p.Mz, p.Intensity)).ToList();

    // ---------------------------------------------------------------- alignment

    private async Task LoadAlignmentAsync()
    {
        AlignmentSpots = Array.Empty<AlignmentSpotRow>();
        SelectedSpot = null;
        BarItems = Array.Empty<BarItem>();
        HasAlignment = false;
        if (_session?.AlignmentFile is null)
        {
            AlignmentSummary = "No alignment result in this session.";
            return;
        }
        try
        {
            var table = await ResultLoader.LoadAlignmentTableAsync(_session.AlignmentFile, _session.AnalysisFiles);
            AlignmentSpots = table.Spots;
            HasAlignment = table.Spots.Count > 0;
            AlignmentSummary = $"{table.Spots.Count} alignment spots across {table.Samples.Count} samples ({table.Spots.Count(s => s.IsAnnotated)} annotated) — loaded from {table.Source}";
            SelectedSpot = table.Spots.FirstOrDefault(s => s.IsAnnotated) ?? table.Spots.FirstOrDefault();
        }
        catch (Exception ex)
        {
            AlignmentSummary = "Alignment could not be loaded: " + ex.Message;
        }
    }

    partial void OnSelectedSpotChanged(AlignmentSpotRow? value)
    {
        if (value is null)
        {
            BarItems = Array.Empty<BarItem>();
            AlignmentMs2Peaks = Array.Empty<Point>();
            return;
        }
        BarItems = value.SampleHeights.Select(h => new BarItem(h.FileName, h.Height, string.IsNullOrEmpty(h.Class) ? "?" : h.Class)).ToList();
        BarTitle = $"Height per sample — spot {value.Id} {value.Name} (m/z {value.Mz:F4}, RT {value.Rt:F2})";
        var alignmentFile = _session?.AlignmentFile;
        if (alignmentFile is null) return;
        _ = Task.Run(() => ResultLoader.LoadAlignmentMs2(alignmentFile, value)).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully && SelectedSpot == value)
            {
                AlignmentMs2Peaks = t.Result is null ? Array.Empty<Point>() : ToPoints(t.Result);
                AlignmentMs2Title = t.Result is null ? "No representative MS/MS" : "Representative MS/MS — " + t.Result.Label;
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    // ---------------------------------------------------------------- commands

    [RelayCommand]
    private void OpenOutputFolder()
    {
        if (_session is not null) ShellService.Open(_session.Folder);
    }

    [RelayCommand]
    private void OpenExport(ExportedFile? file)
    {
        if (file is not null) ShellService.Open(file.Path);
    }

    [RelayCommand]
    private void RevealExport(ExportedFile? file)
    {
        if (file is not null) ShellService.Reveal(file.Path);
    }
}
