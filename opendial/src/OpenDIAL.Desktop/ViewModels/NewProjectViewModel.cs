using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Pipeline.Model;
using OpenDIAL.Pipeline.Parameters;
using OpenDIAL.Pipeline.Project;
using OpenDIAL.Pipeline.Vendor;

namespace OpenDIAL.Desktop.ViewModels;

/// <summary>The New Project wizard: project → samples → method → summary. Writes the project file on Finish.</summary>
public sealed partial class NewProjectViewModel : ViewModelBase
{
    private readonly IFileDialogService _dialogs;
    private readonly SettingsService _settings;

    public static string[] StepTitles { get; } = { "Project", "Samples", "Method", "Summary" };
    public static SampleType[] SampleTypes { get; } = Enum.GetValues<SampleType>();

    public NewProjectViewModel(SettingsService settings, IFileDialogService dialogs)
    {
        _settings = settings;
        _dialogs = dialogs;
        _folder = string.IsNullOrEmpty(settings.Current.LastProjectFolder) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : settings.Current.LastProjectFolder;
        _methodFile = settings.Current.LastMethodFile;
        _mspFile = settings.Current.LastMspFile;
        Samples.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(SamplesSummary)); OnPropertyChanged(nameof(CanNext)); };
    }

    public ObservableCollection<InputFileViewModel> Samples { get; } = new();

    [ObservableProperty] private int _step;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _folder;
    [ObservableProperty] private string _methodSource = "defaults-lcms";
    [ObservableProperty] private string _methodFile;
    [ObservableProperty] private string _mspFile;
    [ObservableProperty] private bool _processNow = true;
    [ObservableProperty] private string _error = string.Empty;
    [ObservableProperty] private InputFileViewModel? _selectedSample;

    public bool IsFirstStep => Step == 0;
    public bool IsLastStep => Step == StepTitles.Length - 1;
    public string StepLabel => $"Step {Step + 1} of {StepTitles.Length} — {StepTitles[Step]}";
    public string NextLabel => IsLastStep ? "Create project" : "Next";
    public bool IsStep0 => Step == 0;
    public bool IsStep1 => Step == 1;
    public bool IsStep2 => Step == 2;
    public bool IsStep3 => Step == 3;

    public bool FromFile { get => MethodSource == "file"; set { if (value) MethodSource = "file"; } }
    public bool DefaultsLcms { get => MethodSource == "defaults-lcms"; set { if (value) MethodSource = "defaults-lcms"; } }
    public bool DefaultsGcms { get => MethodSource == "defaults-gcms"; set { if (value) MethodSource = "defaults-gcms"; } }

    public string ProjectFolder => string.IsNullOrWhiteSpace(Name) ? string.Empty : Path.Combine(Folder, SafeName(Name));
    public string ProjectPath => string.IsNullOrWhiteSpace(Name) ? string.Empty : Path.Combine(ProjectFolder, SafeName(Name) + OpenDialProject.Extension);
    public string ProjectPreview => string.IsNullOrWhiteSpace(Name) ? "Give the project a name." : $"{ProjectPath}\nRaw files stay where they are; results are written next to the project file.";
    public string SamplesSummary => Samples.Count == 0 ? "No files yet." : $"{Samples.Count} sample(s), {Samples.Select(s => s.Class).Distinct().Count()} class(es), {Samples.Count(s => s.NeedsConversion)} file(s) via msconvert";
    public IonizationMode Mode => MethodSource == "defaults-gcms" ? IonizationMode.GCMS : IonizationMode.LCMS;
    public string MethodSummary => MethodSource switch
    {
        "file" => $"From file: {(string.IsNullOrWhiteSpace(MethodFile) ? "(none chosen)" : MethodFile)}",
        "defaults-gcms" => "Defaults for GC-MS (EI, RT alignment)",
        _ => "Defaults for LC-MS DDA (positive, centroid)",
    };
    public string LibrarySummary => string.IsNullOrWhiteSpace(MspFile) ? "No spectral library (peaks stay unknown)" : MspFile;
    public string Summary => $"Project  {ProjectPath}\nSamples  {SamplesSummary}\nMethod   {MethodSummary}\nLibrary  {LibrarySummary}";

    public bool CanNext => Step switch
    {
        0 => !string.IsNullOrWhiteSpace(Name) && Directory.Exists(Folder),
        1 => Samples.Count > 0,
        2 => MethodSource != "file" || File.Exists(MethodFile),
        _ => true,
    };

    partial void OnStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsFirstStep)); OnPropertyChanged(nameof(IsLastStep)); OnPropertyChanged(nameof(StepLabel)); OnPropertyChanged(nameof(NextLabel));
        OnPropertyChanged(nameof(IsStep0)); OnPropertyChanged(nameof(IsStep1)); OnPropertyChanged(nameof(IsStep2)); OnPropertyChanged(nameof(IsStep3));
        OnPropertyChanged(nameof(CanNext)); OnPropertyChanged(nameof(Summary));
    }
    partial void OnNameChanged(string value) { OnPropertyChanged(nameof(ProjectPath)); OnPropertyChanged(nameof(ProjectPreview)); OnPropertyChanged(nameof(CanNext)); }
    partial void OnFolderChanged(string value) { OnPropertyChanged(nameof(ProjectPath)); OnPropertyChanged(nameof(ProjectPreview)); OnPropertyChanged(nameof(CanNext)); }
    partial void OnMethodSourceChanged(string value) { OnPropertyChanged(nameof(FromFile)); OnPropertyChanged(nameof(DefaultsLcms)); OnPropertyChanged(nameof(DefaultsGcms)); OnPropertyChanged(nameof(CanNext)); OnPropertyChanged(nameof(MethodSummary)); OnPropertyChanged(nameof(Summary)); }
    partial void OnMethodFileChanged(string value) { OnPropertyChanged(nameof(CanNext)); OnPropertyChanged(nameof(MethodSummary)); }
    partial void OnMspFileChanged(string value) { OnPropertyChanged(nameof(LibrarySummary)); }

    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    [RelayCommand]
    private async Task BrowseFolderAsync()
    {
        var f = await _dialogs.PickFolderAsync("Project folder", Folder);
        if (f is not null) Folder = f;
    }

    [RelayCommand]
    private async Task AddFilesAsync()
    {
        var files = await _dialogs.PickFilesAsync("Add data files", FileFormats.PickerPatterns, allowMultiple: true, _settings.Current.LastInputFolder);
        AddPaths(files);
        if (files.Count > 0) _settings.Current.LastInputFolder = Path.GetDirectoryName(files[0]) ?? string.Empty;
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync("Add all raw files in a folder", _settings.Current.LastInputFolder);
        if (folder is null) return;
        AddPaths(FileFormats.EnumerateRawFiles(folder));
    }

    /// <summary>Adds files to the wizard batch; a .wiff batch becomes one row per sample.</summary>
    private void AddPaths(IEnumerable<string> paths)
    {
        var problems = new List<string>();
        foreach (var f in paths)
        {
            if (Samples.Any(s => string.Equals(s.Path, f, StringComparison.OrdinalIgnoreCase)) || !FileFormats.IsSupported(f)) continue;
            foreach (var row in InputFileViewModel.FromPath(f, Samples.Count + 1, AcquisitionMode.DDA, out var error))
            {
                if (error is not null) problems.Add(error);
                if (Samples.Any(s => string.Equals(s.Path, row.Path, StringComparison.OrdinalIgnoreCase))) continue;
                Samples.Add(row);
            }
        }
        Error = problems.Count > 0 ? string.Join("; ", problems) : string.Empty;
    }

    [RelayCommand]
    private void RemoveSample()
    {
        if (SelectedSample is not null) Samples.Remove(SelectedSample);
        for (var i = 0; i < Samples.Count; i++) Samples[i].AnalyticalOrder = i + 1;
    }

    [RelayCommand]
    private async Task BrowseMethodAsync()
    {
        var files = await _dialogs.PickFilesAsync("Method file", new[] { "*.txt", "*.method" }, false, Path.GetDirectoryName(MethodFile));
        if (files.Count > 0) { MethodFile = files[0]; MethodSource = "file"; }
    }

    [RelayCommand]
    private async Task BrowseMspAsync()
    {
        var files = await _dialogs.PickFilesAsync("Spectral library", new[] { "*.msp", "*.msp2", "*.lbm2" }, false, Path.GetDirectoryName(MspFile));
        if (files.Count > 0) MspFile = files[0];
    }

    [RelayCommand]
    private void Back() { if (Step > 0) Step--; }

    /// <summary>Advances; returns true when the wizard finished and the project was written.</summary>
    public async Task<bool> NextAsync()
    {
        Error = string.Empty;
        if (!CanNext) return false;
        if (!IsLastStep) { Step++; return false; }
        try
        {
            var project = await BuildProjectAsync();
            await project.SaveAsync(ProjectPath);
            Result = project;
            _settings.Current.LastProjectFolder = Folder;
            if (!string.IsNullOrWhiteSpace(MspFile)) _settings.Current.LastMspFile = MspFile;
            await _settings.SaveAsync();
            return true;
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return false;
        }
    }

    public OpenDialProject? Result { get; private set; }

    private async Task<OpenDialProject> BuildProjectAsync()
    {
        MethodParameters parameters;
        if (MethodSource == "file")
        {
            parameters = await MethodParameters.LoadAsync(MethodFile);
        }
        else
        {
            parameters = new MethodParameters();
        }
        if (!string.IsNullOrWhiteSpace(MspFile)) parameters.MspFilePath = MspFile;
        var folder = ProjectFolder;
        Directory.CreateDirectory(folder);
        return new OpenDialProject
        {
            Name = Name.Trim(),
            Mode = Mode,
            OutputFolder = Path.Combine(folder, "results"),
            MethodText = parameters.ToMethodFileText(Mode),
            Samples = Samples.Select(s => s.ToProjectSample()).ToList(),
        };
    }
}
