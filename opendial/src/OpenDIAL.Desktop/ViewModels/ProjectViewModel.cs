using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Pipeline.Model;
using OpenDIAL.Pipeline.Parameters;
using OpenDIAL.Pipeline.Vendor;

namespace OpenDIAL.Desktop.ViewModels;

public sealed partial class ProjectViewModel : ViewModelBase
{
    private readonly IFileDialogService _dialogs;
    private readonly SettingsService _settings;

    public static IonizationMode[] Modes { get; } = Enum.GetValues<IonizationMode>();

    public ProjectViewModel(SettingsService settings, IFileDialogService dialogs)
    {
        _settings = settings;
        _dialogs = dialogs;
        _outputFolder = settings.Current.LastOutputFolder;
        Parameters = new MethodParametersViewModel();
        if (!string.IsNullOrEmpty(settings.Current.LastMspFile) && File.Exists(settings.Current.LastMspFile))
        {
            Parameters.MspFilePath = settings.Current.LastMspFile;
        }
        InputFiles.CollectionChanged += (_, _) => { OnPropertyChanged(nameof(FileCountText)); OnPropertyChanged(nameof(CanRun)); };
    }

    public ObservableCollection<InputFileViewModel> InputFiles { get; } = new();
    public MethodParametersViewModel Parameters { get; }

    [ObservableProperty] private InputFileViewModel? _selectedInputFile;
    [ObservableProperty] private string _outputFolder;
    [ObservableProperty] private bool _saveProject = true;
    [ObservableProperty] private string _statusMessage = string.Empty;

    private IonizationMode _mode = IonizationMode.LCMS;
    public IonizationMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
            {
                Parameters.Mode = value;
            }
        }
    }

    public string FileCountText => InputFiles.Count == 0 ? "No files" : $"{InputFiles.Count} file(s), {InputFiles.Count(f => f.IsVendorFormat)} vendor";

    public bool CanRun => InputFiles.Count > 0;

    partial void OnOutputFolderChanged(string value) => OnPropertyChanged(nameof(CanRun));

    // ---------------------------------------------------------------- files

    [RelayCommand]
    private async Task AddFilesAsync()
    {
        var files = await _dialogs.PickFilesAsync("Add raw data files", FileFormats.PickerPatterns, allowMultiple: true, _settings.Current.LastInputFolder);
        AddPaths(files);
    }

    [RelayCommand]
    private async Task AddFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync("Add all raw files in a folder", _settings.Current.LastInputFolder);
        if (folder is null) return;
        var files = FileFormats.EnumerateRawFiles(folder).ToList();
        if (files.Count == 0)
        {
            StatusMessage = $"No supported raw files found in {folder}.";
            return;
        }
        AddPaths(files);
    }

    public void AddPaths(IEnumerable<string> paths)
    {
        var added = 0;
        foreach (var path in paths)
        {
            if (InputFiles.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }
            if (!FileFormats.IsSupported(path))
            {
                StatusMessage = $"Skipped unsupported file: {Path.GetFileName(path)}";
                continue;
            }
            InputFiles.Add(new InputFileViewModel(path, InputFiles.Count + 1, Parameters.AcquisitionType));
            added++;
        }
        if (added > 0)
        {
            var first = InputFiles[^1].Path;
            _settings.Current.LastInputFolder = Path.GetDirectoryName(first) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(OutputFolder))
            {
                OutputFolder = Path.Combine(Path.GetDirectoryName(first) ?? string.Empty, "opendial_output");
            }
            StatusMessage = $"Added {added} file(s).";
            _ = _settings.SaveAsync();
        }
    }

    [RelayCommand]
    private void RemoveSelected()
    {
        if (SelectedInputFile is null) return;
        InputFiles.Remove(SelectedInputFile);
        Renumber();
    }

    [RelayCommand]
    private void ClearFiles()
    {
        InputFiles.Clear();
    }

    private void Renumber()
    {
        for (var i = 0; i < InputFiles.Count; i++)
        {
            InputFiles[i].AnalyticalOrder = i + 1;
        }
    }

    [RelayCommand]
    private async Task BrowseOutputAsync()
    {
        var folder = await _dialogs.PickFolderAsync("Choose the output folder", OutputFolder);
        if (folder is not null)
        {
            OutputFolder = folder;
            _settings.Current.LastOutputFolder = folder;
            _ = _settings.SaveAsync();
        }
    }

    [RelayCommand]
    private async Task BrowseMspAsync()
    {
        var files = await _dialogs.PickFilesAsync("Choose an MSP spectral library", new[] { "*.msp", "*.msp2", "*.lbm2", "*.txt" }, allowMultiple: false,
            Path.GetDirectoryName(Parameters.MspFilePath));
        if (files.Count > 0)
        {
            Parameters.MspFilePath = files[0];
            _settings.Current.LastMspFile = files[0];
            _ = _settings.SaveAsync();
        }
    }

    [RelayCommand]
    private async Task BrowseTextDbAsync()
    {
        var files = await _dialogs.PickFilesAsync("Choose a text (RT/m/z) library", new[] { "*.txt", "*.tsv" }, allowMultiple: false);
        if (files.Count > 0)
        {
            Parameters.TextDbFilePath = files[0];
        }
    }

    // ---------------------------------------------------------------- method files

    [RelayCommand]
    private async Task LoadMethodAsync()
    {
        var files = await _dialogs.PickFilesAsync("Load a method file", new[] { "*.txt", "*.method" }, allowMultiple: false,
            Path.GetDirectoryName(_settings.Current.LastMethodFile));
        if (files.Count == 0) return;
        try
        {
            var parameters = await MethodParameters.LoadAsync(files[0]);
            Parameters.Load(parameters);
            _settings.Current.LastMethodFile = files[0];
            _ = _settings.SaveAsync();
            StatusMessage = $"Loaded method file {Path.GetFileName(files[0])}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load method file: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveMethodAsync()
    {
        var path = await _dialogs.SaveFileAsync("Save method file", "method.txt", "txt", Path.GetDirectoryName(_settings.Current.LastMethodFile));
        if (path is null) return;
        try
        {
            await File.WriteAllTextAsync(path, Parameters.MethodText);
            _settings.Current.LastMethodFile = path;
            _ = _settings.SaveAsync();
            StatusMessage = $"Saved method file {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save method file: {ex.Message}";
        }
    }

    // ---------------------------------------------------------------- request

    public PipelineRequest? BuildRequest(out string? error)
    {
        error = null;
        if (InputFiles.Count == 0)
        {
            error = "Add at least one input file.";
            return null;
        }
        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            error = "Choose an output folder.";
            return null;
        }
        if (!string.IsNullOrWhiteSpace(Parameters.MspFilePath) && !File.Exists(Parameters.MspFilePath))
        {
            error = $"MSP library not found: {Parameters.MspFilePath}";
            return null;
        }
        var request = new PipelineRequest
        {
            OutputFolder = OutputFolder,
            Mode = Mode,
            Parameters = Parameters.Model.Clone(),
            SaveProject = SaveProject,
            VendorConversion = new MsconvertVendorConversionService(_settings.Current.VendorConversion),
        };
        foreach (var file in InputFiles)
        {
            request.InputFiles.Add(file.ToModel());
        }
        _settings.Current.LastOutputFolder = OutputFolder;
        _ = _settings.SaveAsync();
        return request;
    }
}
