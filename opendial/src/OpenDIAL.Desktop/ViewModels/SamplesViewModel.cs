using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Pipeline.Model;
using OpenDIAL.Pipeline.Vendor;

namespace OpenDIAL.Desktop.ViewModels;

/// <summary>The batch table: which files are in the project and what each injection is.</summary>
public sealed partial class SamplesViewModel : ViewModelBase
{
    private readonly IFileDialogService _dialogs;
    private readonly SettingsService _settings;

    public static SampleType[] SampleTypes { get; } = Enum.GetValues<SampleType>();

    public SamplesViewModel(SettingsService settings, IFileDialogService dialogs)
    {
        _settings = settings;
        _dialogs = dialogs;
        Samples.CollectionChanged += (_, _) => { Refresh(); Changed?.Invoke(this, EventArgs.Empty); };
    }

    public ObservableCollection<InputFileViewModel> Samples { get; } = new();
    public ObservableCollection<InputFileViewModel> SelectedSamples { get; } = new();
    public ObservableCollection<string> ClassNames { get; } = new();

    [ObservableProperty] private InputFileViewModel? _selectedSample;
    [ObservableProperty] private SampleType _typeToApply = SampleType.Sample;
    [ObservableProperty] private string _classToApply = string.Empty;
    [ObservableProperty] private string _statusLine = "No samples — add data files to start a batch.";
    [ObservableProperty] private string _message = string.Empty;

    /// <summary>Default acquisition for newly added files (follows the method).</summary>
    public AcquisitionMode DefaultAcquisition { get; set; } = AcquisitionMode.DDA;

    /// <summary>Raised on any structural or field change (dirty tracking).</summary>
    public event EventHandler? Changed;

    public bool HasSamples => Samples.Count > 0;

    public void Refresh()
    {
        var classes = Samples.Select(s => s.Class).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();
        ClassNames.Clear();
        foreach (var c in classes) ClassNames.Add(c);
        var types = Samples.GroupBy(s => s.SampleType).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key}");
        var vendor = Samples.Count(s => s.NeedsConversion);
        var native = Samples.Count(s => s.ReadsNatively);
        var missing = Samples.Count(s => !s.Exists);
        StatusLine = Samples.Count == 0
            ? "No samples — add data files to start a batch."
            : $"{Samples.Count} sample(s) — {classes.Count} class(es)" + (types.Any() ? " · " + string.Join(", ", types) : string.Empty)
              + (native > 0 ? $" · {native} wiff sample(s) read natively" : string.Empty)
              + (vendor > 0 ? $" · {vendor} vendor file(s) via msconvert" : string.Empty) + (missing > 0 ? $" · {missing} file(s) not found" : string.Empty);
        OnPropertyChanged(nameof(HasSamples));
    }

    public void Load(IEnumerable<InputFileViewModel> rows)
    {
        Samples.Clear();
        foreach (var r in rows) Attach(r);
        Refresh();
    }

    private void Attach(InputFileViewModel row)
    {
        row.Edited += (_, _) => { Refresh(); Changed?.Invoke(this, EventArgs.Empty); };
        Samples.Add(row);
    }

    [RelayCommand]
    private async Task AddFilesAsync()
    {
        var files = await _dialogs.PickFilesAsync("Add data files", FileFormats.PickerPatterns, allowMultiple: true, _settings.Current.LastInputFolder);
        AddPaths(files);
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync("Add all raw files in a folder", _settings.Current.LastInputFolder);
        if (folder is null) return;
        var files = FileFormats.EnumerateRawFiles(folder).ToList();
        if (files.Count == 0) { Message = $"No supported raw files found in {folder}."; return; }
        AddPaths(files);
    }

    public int AddPaths(IEnumerable<string> paths)
    {
        var added = 0;
        var problems = new List<string>();
        foreach (var path in paths)
        {
            if (Samples.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
            if (!FileFormats.IsSupported(path)) { problems.Add($"skipped unsupported file {Path.GetFileName(path)}"); continue; }
            foreach (var row in InputFileViewModel.FromPath(path, Samples.Count + 1, DefaultAcquisition, out var error))
            {
                if (error is not null) problems.Add(error);
                if (Samples.Any(f => string.Equals(f.Path, row.Path, StringComparison.OrdinalIgnoreCase))) continue;
                Attach(row);
                added++;
            }
        }
        if (added > 0)
        {
            _settings.Current.LastInputFolder = Path.GetDirectoryName(Samples[^1].Path) ?? string.Empty;
            _ = _settings.SaveAsync();
            Refresh();
        }
        Message = problems.Count > 0 ? string.Join("; ", problems) : added > 0 ? $"Added {added} sample(s)." : Message;
        return added;
    }

    /// <summary>Adds prepared rows (e.g. from an OpenQuant batch), skipping paths already in the batch; returns how many were added.</summary>
    public int AddRows(IEnumerable<InputFileViewModel> rows)
    {
        var added = 0;
        foreach (var row in rows)
        {
            if (Samples.Any(f => string.Equals(f.Path, row.Path, StringComparison.OrdinalIgnoreCase) && f.SampleIndex == row.SampleIndex)) continue;
            row.AnalyticalOrder = Samples.Count + 1;
            Attach(row);
            added++;
        }
        if (added > 0)
        {
            _settings.Current.LastInputFolder = Path.GetDirectoryName(Samples[^1].Path) ?? string.Empty;
            _ = _settings.SaveAsync();
            Refresh();
        }
        return added;
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        var targets = SelectedSamples.ToList();
        if (targets.Count == 0 && SelectedSample is not null) targets.Add(SelectedSample);
        foreach (var t in targets) Samples.Remove(t);
        Renumber();
    }

    [RelayCommand]
    private void ClearAll()
    {
        Samples.Clear();
    }

    [RelayCommand]
    private void ApplyType()
    {
        foreach (var s in Targets()) s.SampleType = TypeToApply;
    }

    [RelayCommand]
    private void ApplyClass()
    {
        if (string.IsNullOrWhiteSpace(ClassToApply)) return;
        foreach (var s in Targets()) s.Class = ClassToApply.Trim();
        Refresh();
    }

    private IEnumerable<InputFileViewModel> Targets()
    {
        var targets = SelectedSamples.ToList();
        if (targets.Count == 0 && SelectedSample is not null) targets.Add(SelectedSample);
        return targets;
    }

    private void Renumber()
    {
        for (var i = 0; i < Samples.Count; i++) Samples[i].AnalyticalOrder = i + 1;
    }

    public List<InputFile> ToModels() => Samples.Select(s => s.ToModel()).ToList();
}
