using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CompMs.Common.Enum;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Interop.OpenQuant;
using OpenDIAL.Pipeline;
using OpenDIAL.Pipeline.Model;
using OpenDIAL.Pipeline.Project;
using OpenDIAL.Pipeline.Results;
using OpenDIAL.Pipeline.Vendor;

namespace OpenDIAL.Desktop.ViewModels;

/// <summary>
/// The shell: owns the project (batch, method, output folder, results) and switches between the workspaces.
/// The window owns the file handling, the menus and the status bar; each workspace reads from the same session.
/// </summary>
public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileDialogService _dialogs;
    private readonly IMessageService _messages;
    private bool _loading;

    public MainWindowViewModel(SettingsService settings, IFileDialogService dialogs, IMessageService messages)
    {
        Settings = settings;
        _dialogs = dialogs;
        _messages = messages;
        RawCache = new RawDataCache();
        RawCache.Status += (_, s) => Avalonia.Threading.Dispatcher.UIThread.Post(() => Status = s);
        Samples = new SamplesViewModel(settings, dialogs);
        Method = new MethodViewModel(settings, dialogs);
        Explorer = new ExplorerViewModel(RawCache);
        Analytics = new AnalyticsViewModel(RawCache) { ProcessBatchCommand = ProcessBatchCommand };
        Statistics = new StatisticsViewModel(dialogs)
        {
            // clicking a node of the molecular network opens that feature in the ion table
            RequestShowFeature = id => { SelectedWorkspace = 1; Analytics.SelectFeature(id); },
        };
        Run = new RunViewModel();
        Samples.Changed += (_, _) => { if (!_loading) IsDirty = true; };
        Method.Changed += (_, _) => { if (!_loading) IsDirty = true; Samples.DefaultAcquisition = Method.Parameters.AcquisitionType; };
        Run.LogLines.CollectionChanged += (_, _) => OnPropertyChanged(nameof(LogLines));
        ShowLog = settings.Current.ShowLog;
        RefreshRecent();
        Method.LoadDefaults(IonizationMode.LCMS);
        IsDirty = false;
    }

    public SettingsService Settings { get; }
    public RawDataCache RawCache { get; }
    public SamplesViewModel Samples { get; }
    public MethodViewModel Method { get; }
    public ExplorerViewModel Explorer { get; }
    public AnalyticsViewModel Analytics { get; }
    public StatisticsViewModel Statistics { get; }
    public RunViewModel Run { get; }
    /// <summary>The manual, loaded on first use: the pages are embedded, so this is a few milliseconds.</summary>
    public Help.HelpViewModel? Help { get; private set; }
    public ObservableCollection<RecentProject> RecentProjects { get; } = new();
    public ObservableCollection<string> LogLines => Run.LogLines;

    [ObservableProperty] private int _selectedWorkspace;
    [ObservableProperty] private string _status = "Open a project, or create one with File ▸ New project…";
    [ObservableProperty] private string _projectName = string.Empty;
    [ObservableProperty] private string? _projectPath;
    [ObservableProperty] private string _outputFolder = string.Empty;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private bool _showLog;
    [ObservableProperty] private bool _showProgressBand;
    [ObservableProperty] private ResultSession? _results;

    public string Title => string.IsNullOrEmpty(ProjectName) ? "OpenDIAL" + (IsDirty ? " •" : string.Empty) : $"OpenDIAL — {ProjectName}{(IsDirty ? " •" : string.Empty)}";
    public bool HasProject => !string.IsNullOrEmpty(ProjectName) || Samples.HasSamples;
    public bool HasResults => Results is not null;
    public bool HasContent => Samples.HasSamples || Results is not null;

    partial void OnProjectNameChanged(string value) { OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(HasProject)); }
    partial void OnIsDirtyChanged(bool value) => OnPropertyChanged(nameof(Title));
    partial void OnResultsChanged(ResultSession? value) { OnPropertyChanged(nameof(HasResults)); OnPropertyChanged(nameof(HasContent)); }
    partial void OnShowLogChanged(bool value) { Settings.Current.ShowLog = value; _ = Settings.SaveAsync(); }

    /// <summary>Set by the window: shows the wizard and returns the created project (or null).</summary>
    public Func<Task<OpenDialProject?>>? ShowNewProjectWizard { get; set; }
    public Func<Task>? ShowSettings { get; set; }
    public Func<Task>? ShowAbout { get; set; }
    /// <summary>Set by the window: shows the OpenQuant export options and returns them (or null when cancelled).</summary>
    public Func<Task<OpenQuantExportOptions?>>? ShowOpenQuantExport { get; set; }

    private void RefreshRecent()
    {
        RecentProjects.Clear();
        foreach (var r in Settings.Current.RecentProjects) RecentProjects.Add(r);
    }

    // ---------------------------------------------------------------- workspaces

    [RelayCommand] private void SelectWorkspace(object? index) => SelectedWorkspace = index is string s && int.TryParse(s, out var i) ? i : index is int n ? n : SelectedWorkspace;
    [RelayCommand] private void ToggleLog() => ShowLog = !ShowLog;

    // ---------------------------------------------------------------- project lifecycle

    public async Task<bool> ConfirmDiscardAsync(string action)
    {
        if (!(IsDirty && HasContent)) return true;
        var choice = await _messages.ConfirmDiscardAsync($"This project has changes that are not saved. {action}");
        if (choice == DiscardChoice.Cancel) return false;
        if (choice == DiscardChoice.Save) return await SaveProjectAsync();
        return true;
    }

    private void ResetSession()
    {
        _loading = true;
        Results = null;
        Analytics.Clear();
        Statistics.Clear();
        Explorer.Clear();
        RawCache.Clear();
        Samples.Load(Array.Empty<InputFileViewModel>());
        ProjectName = string.Empty;
        ProjectPath = null;
        OutputFolder = string.Empty;
        _loading = false;
        IsDirty = false;
    }

    [RelayCommand]
    private async Task NewProjectAsync()
    {
        if (ShowNewProjectWizard is null) return;
        if (!await ConfirmDiscardAsync("Start a new project anyway?")) return;
        var project = await ShowNewProjectWizard();
        if (project is null) return;
        ResetSession();
        ApplyProject(project);
        RememberRecent(project.FilePath!, project.Name);
        Status = $"Project created at {project.FilePath}";
        SelectedWorkspace = 3;
        IsDirty = false;
    }

    private void ApplyProject(OpenDialProject project)
    {
        _loading = true;
        try
        {
            ProjectName = project.Name;
            ProjectPath = project.FilePath;
            OutputFolder = project.OutputFolder;
            Method.LoadText(project.MethodText, project.Mode);
            Samples.DefaultAcquisition = Method.Parameters.AcquisitionType;
            Samples.Load(project.Samples.Select(InputFileViewModel.FromProjectSample));
            Explorer.Load(Samples.Samples.ToList(), Results);
        }
        finally
        {
            _loading = false;
        }
    }

    [RelayCommand]
    private async Task OpenProjectAsync()
    {
        var files = await _dialogs.PickFilesAsync("Open project", new[] { "*" + OpenDialProject.Extension, "*.mdproject" }, allowMultiple: false, Settings.Current.LastProjectFolder);
        if (files.Count == 0) return;
        if (!await ConfirmDiscardAsync("Open another project anyway?")) return;
        await OpenAsync(files[0]);
    }

    [RelayCommand]
    private async Task OpenResultsFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync("Open a results folder (OpenDIAL or MS-DIAL console output)", Settings.Current.LastOutputFolder);
        if (folder is null) return;
        if (!await ConfirmDiscardAsync("Open another project anyway?")) return;
        await OpenAsync(folder);
    }

    [RelayCommand]
    private async Task OpenRecentAsync(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            await _messages.ShowErrorAsync("Not found", $"{path} no longer exists.");
            Settings.Current.RecentProjects.RemoveAll(r => r.Path == path);
            RefreshRecent();
            return;
        }
        if (!await ConfirmDiscardAsync("Open another project anyway?")) return;
        await OpenAsync(path);
    }

    /// <summary>
    /// Opens whatever the path happens to be, choosing by extension: an OpenQuant batch is imported,
    /// a raw file or a folder of raw files goes to the Explorer, anything else is treated as a project
    /// or a results folder. This is what Finder hands us when a document is dropped on the application.
    /// </summary>
    public Task OpenAnyAsync(string path)
    {
        if (OpenQuantProject.IsProject(path)) return ImportOpenQuantBatchAsync(path);
        if (FileFormats.IsSupported(path)) return OpenRawAsync(path);
        return OpenAsync(path);
    }

    /// <summary>Opens an OpenDIAL project (.odproj), an MS-DIAL .mdproject or a results folder.</summary>
    public async Task OpenAsync(string path)
    {
        path = Path.GetFullPath(path);
        Status = $"Opening {path}…";
        try
        {
            ResetSession();
            _loading = true;
            OpenDialProject? project = null;
            string? resultsPath = null;
            if (path.EndsWith(OpenDialProject.Extension, StringComparison.OrdinalIgnoreCase))
            {
                project = await OpenDialProject.LoadAsync(path);
                resultsPath = project.MdprojectPath is not null && File.Exists(project.MdprojectPath) ? project.MdprojectPath
                    : Directory.Exists(project.OutputFolder) && Directory.EnumerateFiles(project.OutputFolder, "*.pai2").Any() ? project.OutputFolder : null;
            }
            else
            {
                resultsPath = path;
                var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? string.Empty;
                var found = OpenDialProject.FindInFolder(folder) ?? OpenDialProject.FindInFolder(Path.GetDirectoryName(folder.TrimEnd(Path.DirectorySeparatorChar)) ?? string.Empty);
                if (found is not null) project = await OpenDialProject.LoadAsync(found);
            }

            OpenedProject? opened = null;
            if (resultsPath is not null)
            {
                opened = await ProjectOpener.OpenAsync(resultsPath);
                Results = ResultSession.From(opened);
            }

            if (project is not null)
            {
                ApplyProject(project);
            }
            else if (opened is not null)
            {
                // reconstruct the batch from the results
                ProjectName = Path.GetFileName(opened.Folder.TrimEnd(Path.DirectorySeparatorChar));
                OutputFolder = opened.Folder;
                var methodFile = Path.Combine(opened.Folder, "opendial_method.txt");
                if (File.Exists(methodFile)) Method.LoadText(await File.ReadAllTextAsync(methodFile), opened.Mode);
                else Method.LoadDefaults(opened.Mode);
                var rows = new List<InputFileViewModel>();
                foreach (var f in opened.AnalysisFiles.OrderBy(f => f.AnalysisFileAnalyticalOrder))
                {
                    var row = new InputFileViewModel(f.AnalysisFilePath, f.AnalysisFileAnalyticalOrder, Method.Parameters.AcquisitionType, WiffSupport.InferSampleIndex(f.AnalysisFilePath), f.AnalysisFileName)
                    {
                        Name = f.AnalysisFileName,
                        Class = f.AnalysisFileClass ?? "1",
                        SampleType = f.AnalysisFileType switch { AnalysisFileType.Blank => SampleType.Blank, AnalysisFileType.QC => SampleType.QC, AnalysisFileType.Standard => SampleType.Standard, _ => SampleType.Sample },
                        Batch = Math.Max(1, f.AnalysisBatch),
                        Included = f.AnalysisFileIncluded,
                    };
                    rows.Add(row);
                }
                Samples.Load(rows);
                Explorer.Load(Samples.Samples.ToList(), Results);
            }
            if (Results is not null)
            {
                Explorer.Load(Samples.Samples.ToList(), Results);
                await Analytics.LoadAsync(Results);
                Statistics.Load(Results, Analytics.AllSpots, Analytics.Samples);
                SelectedWorkspace = 1;
                Status = $"Opened {Results.AnalysisFiles.Count} file(s) from {Results.Folder}";
            }
            else
            {
                SelectedWorkspace = 3;
                Status = $"Opened {ProjectName} — {Samples.Samples.Count} sample(s), not processed yet.";
            }
            RememberRecent(project?.FilePath ?? path, ProjectName);
            Settings.Current.LastOutputFolder = OutputFolder;
            Settings.Current.LastProjectFolder = Path.GetDirectoryName(project?.FilePath ?? path) ?? string.Empty;
            _ = Settings.SaveAsync();
        }
        catch (Exception ex)
        {
            Status = "Could not open: " + ex.Message;
            await _messages.ShowErrorAsync("Could not open", ex.Message);
        }
        finally
        {
            _loading = false;
            IsDirty = false;
        }
    }

    /// <summary>
    /// Adds raw files (one path, or every raw file of a folder) to the batch without any results and opens the
    /// first one in the Explorer. Used by the OPENDIAL_OPEN_RAW hook and by drag-and-drop style quick looks.
    /// </summary>
    public async Task OpenRawAsync(string pathOrFolder)
    {
        pathOrFolder = Path.GetFullPath(pathOrFolder);
        try
        {
            var paths = Directory.Exists(pathOrFolder) && !FileFormats.IsSupported(pathOrFolder)
                ? FileFormats.EnumerateRawFiles(pathOrFolder).ToList()
                : new List<string> { pathOrFolder };
            if (paths.Count == 0)
            {
                Status = $"No supported raw files in {pathOrFolder}.";
                return;
            }
            if (!await ConfirmDiscardAsync("Open the raw files anyway?")) return;
            ResetSession();
            _loading = true;
            var added = Samples.AddPaths(paths);
            ProjectName = Path.GetFileNameWithoutExtension(paths[0].TrimEnd(Path.DirectorySeparatorChar));
            Explorer.Load(Samples.Samples.ToList(), null);
            SelectedWorkspace = 0;
            Status = added == 0
                ? Samples.Message
                : $"Opened {added} sample(s) from {(paths.Count == 1 ? paths[0] : pathOrFolder)} — not processed; the Explorer shows the raw channels." + (string.IsNullOrEmpty(Samples.Message) ? string.Empty : " " + Samples.Message);
        }
        catch (Exception ex)
        {
            Status = "Could not open raw data: " + ex.Message;
            await _messages.ShowErrorAsync("Could not open raw data", ex.Message);
        }
        finally
        {
            _loading = false;
            IsDirty = Samples.HasSamples;
        }
    }

    // ---------------------------------------------------------------- OpenQuant interop

    [RelayCommand]
    private async Task ImportOpenQuantBatchAsync()
    {
        var files = await _dialogs.PickFilesAsync("Import an OpenQuant batch", new[] { "*.oqproj", "*.opvproj" }, allowMultiple: false, Settings.Current.LastProjectFolder);
        if (files.Count == 0) return;
        await ImportOpenQuantBatchAsync(files[0]);
    }

    /// <summary>Adds the samples of an OpenQuant project (.oqproj / .opvproj) to the batch with their type, group, dilution and comment.</summary>
    public async Task ImportOpenQuantBatchAsync(string path)
    {
        try
        {
            var contents = await Task.Run(() => OpenQuantProject.Load(path));
            if (contents.Samples.Count == 0)
            {
                await _messages.ShowErrorAsync("Nothing to import", $"{Path.GetFileName(path)} has no samples.");
                return;
            }
            var rows = new List<InputFileViewModel>();
            var missing = new List<string>();
            var problems = new List<string>();
            var baseFolder = Path.GetDirectoryName(path) ?? string.Empty;
            foreach (var sample in contents.Samples)
            {
                var samplePath = string.IsNullOrWhiteSpace(sample.Path) ? string.Empty : Path.IsPathRooted(sample.Path) ? sample.Path : Path.GetFullPath(Path.Combine(baseFolder, sample.Path));
                if (samplePath.Length == 0) { problems.Add($"{sample.Name}: no path"); continue; }
                var exists = File.Exists(samplePath) || Directory.Exists(samplePath);
                if (!exists) missing.Add(samplePath);
                var rowPath = samplePath;
                var sampleIndex = sample.SampleIndex;
                if (exists && WiffSupport.IsWiff(samplePath) && WiffSupport.IsNativeAvailable)
                {
                    try
                    {
                        var entries = await Task.Run(() => WiffSupport.Expand(samplePath));
                        var entry = entries.FirstOrDefault(e => e.SampleIndex == sample.SampleIndex) ?? entries.FirstOrDefault();
                        if (entry is not null) { rowPath = entry.Path; sampleIndex = entry.SampleIndex; }
                    }
                    catch (Exception ex)
                    {
                        problems.Add($"{Path.GetFileName(samplePath)}: {ex.Message}");
                    }
                }
                var type = sample.ToAnalysisFileType() switch
                {
                    CompMs.Common.Enum.AnalysisFileType.Blank => SampleType.Blank,
                    CompMs.Common.Enum.AnalysisFileType.QC => SampleType.QC,
                    CompMs.Common.Enum.AnalysisFileType.Standard => SampleType.Standard,
                    _ => SampleType.Sample,
                };
                rows.Add(new InputFileViewModel(rowPath, rows.Count + 1, Samples.DefaultAcquisition, sampleIndex, sample.Name)
                {
                    SampleType = type,
                    Class = sample.ToAnalysisClass(),
                    Dilution = sample.DilutionFactor > 0 ? sample.DilutionFactor : 1,
                    Comment = sample.Comment ?? string.Empty,
                });
            }
            var added = Samples.AddRows(rows);
            Explorer.Load(Samples.Samples.ToList(), Results);
            if (string.IsNullOrEmpty(ProjectName)) ProjectName = Path.GetFileNameWithoutExtension(path);
            SelectedWorkspace = 3;
            IsDirty = true;
            Status = $"Imported {added} sample(s) from {Path.GetFileName(path)}" + (missing.Count > 0 ? $" — {missing.Count} file(s) not found" : string.Empty);
            if (missing.Count > 0 || problems.Count > 0)
            {
                var text = string.Empty;
                if (missing.Count > 0) text += "These files do not exist (the rows were added anyway):\n" + string.Join("\n", missing.Take(12)) + (missing.Count > 12 ? $"\n… and {missing.Count - 12} more" : string.Empty);
                if (problems.Count > 0) text += (text.Length > 0 ? "\n\n" : string.Empty) + string.Join("\n", problems);
                await _messages.ShowErrorAsync("OpenQuant batch imported with warnings", text);
            }
        }
        catch (Exception ex)
        {
            Status = "Import failed: " + ex.Message;
            await _messages.ShowErrorAsync("Could not import the OpenQuant batch", ex.Message);
        }
    }

    /// <summary>Writes the reviewed ion table, filter and curation included.</summary>
    [RelayCommand]
    private async Task ExportReviewedTableAsync()
    {
        if (Results is null || !Analytics.HasResults) { Status = "Process the batch (or open results) before exporting."; return; }
        var suggested = (string.IsNullOrEmpty(ProjectName) ? "alignment" : ProjectName) + "_reviewed.txt";
        var path = await _dialogs.SaveFileAsync("Export the reviewed table", suggested, "txt", OutputFolder);
        if (path is null) return;
        try
        {
            Status = "Exporting the reviewed table…";
            var count = await Analytics.ExportReviewedTableAsync(path, areas: false);
            Status = $"{count} feature(s) written to {path}";
        }
        catch (Exception ex)
        {
            Status = "Export failed: " + ex.Message;
            await _messages.ShowErrorAsync("Export failed", ex.Message);
        }
    }

    [RelayCommand]
    private async Task ExportOpenQuantAsync()
    {
        if (Results is null || !Analytics.HasResults) { Status = "Process the batch (or open results) before exporting to OpenQuant."; return; }
        if (ShowOpenQuantExport is null) return;
        var options = await ShowOpenQuantExport();
        if (options is null) return;
        var suggested = (string.IsNullOrEmpty(ProjectName) ? "components" : ProjectName + "_components") + ".csv";
        var path = await _dialogs.SaveFileAsync("Export components to OpenQuant", suggested, "csv", OutputFolder);
        if (path is null) return;
        try
        {
            Status = "Exporting components…";
            var count = await Analytics.ExportOpenQuantAsync(path, options);
            Status = $"{count} components written to {path}";
        }
        catch (Exception ex)
        {
            Status = "Export failed: " + ex.Message;
            await _messages.ShowErrorAsync("Export failed", ex.Message);
        }
    }

    private void RememberRecent(string path, string name)
    {
        Settings.AddRecent(path, string.IsNullOrEmpty(name) ? Path.GetFileName(path) : name);
        RefreshRecent();
        _ = Settings.SaveAsync();
    }

    private OpenDialProject BuildProject() => new()
    {
        Name = string.IsNullOrEmpty(ProjectName) ? "project" : ProjectName,
        Mode = Method.Mode,
        OutputFolder = OutputFolder,
        MdprojectPath = Results?.ProjectFilePath,
        MethodText = Method.Parameters.MethodText,
        Samples = Samples.Samples.Select(s => s.ToProjectSample()).ToList(),
        FilePath = ProjectPath,
    };

    [RelayCommand]
    public async Task<bool> SaveProjectAsync()
    {
        if (string.IsNullOrEmpty(ProjectPath)) return await SaveProjectAsAsync();
        try
        {
            var project = BuildProject();
            await project.SaveAsync(ProjectPath);
            IsDirty = false;
            Status = $"Project saved to {ProjectPath}";
            RememberRecent(ProjectPath, project.Name);
            return true;
        }
        catch (Exception ex)
        {
            await _messages.ShowErrorAsync("Could not save", ex.Message);
            return false;
        }
    }

    [RelayCommand]
    public async Task<bool> SaveProjectAsAsync()
    {
        var suggested = string.IsNullOrEmpty(ProjectName) ? "project" : ProjectName;
        var start = string.IsNullOrEmpty(ProjectPath) ? (string.IsNullOrEmpty(OutputFolder) ? Settings.Current.LastProjectFolder : OutputFolder) : Path.GetDirectoryName(ProjectPath);
        var path = await _dialogs.SaveFileAsync("Save project", suggested + OpenDialProject.Extension, OpenDialProject.Extension.TrimStart('.'), start);
        if (path is null) return false;
        ProjectPath = path;
        if (string.IsNullOrEmpty(ProjectName)) ProjectName = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrEmpty(OutputFolder)) OutputFolder = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, "results");
        return await SaveProjectAsync();
    }

    [RelayCommand]
    private async Task CloseProjectAsync()
    {
        if (!await ConfirmDiscardAsync("Close the project anyway?")) return;
        ResetSession();
        Status = "Project closed.";
    }

    [RelayCommand]
    private async Task BrowseOutputAsync()
    {
        var folder = await _dialogs.PickFolderAsync("Choose the results folder", OutputFolder);
        if (folder is not null) { OutputFolder = folder; IsDirty = true; }
    }

    // ---------------------------------------------------------------- processing

    public bool CanProcess => Samples.HasSamples && !Run.IsRunning;

    [RelayCommand]
    private async Task ProcessBatchAsync()
    {
        if (Run.IsRunning) { Status = "A run is already in progress."; return; }
        if (!Samples.HasSamples) { Status = "Add data files before processing."; SelectedWorkspace = 3; return; }
        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            OutputFolder = string.IsNullOrEmpty(ProjectPath)
                ? Path.Combine(Path.GetDirectoryName(Samples.Samples[0].Path) ?? string.Empty, "opendial_results")
                : Path.Combine(Path.GetDirectoryName(ProjectPath) ?? string.Empty, "results");
        }
        if (!string.IsNullOrWhiteSpace(Method.Parameters.MspFilePath) && !File.Exists(Method.Parameters.MspFilePath))
        {
            await _messages.ShowErrorAsync("Library not found", $"The MSP library was not found:\n{Method.Parameters.MspFilePath}");
            SelectedWorkspace = 2;
            return;
        }
        var request = new PipelineRequest
        {
            OutputFolder = OutputFolder,
            Mode = Method.Mode,
            Parameters = Method.Parameters.Model.Clone(),
            SaveProject = true,
            VendorConversion = new MsconvertVendorConversionService(Settings.Current.VendorConversion),
        };
        foreach (var s in Samples.Samples) request.InputFiles.Add(s.ToModel());

        ShowProgressBand = true;
        Status = "Processing…";
        SelectedWorkspace = 1;
        var result = await Run.RunAsync(request);
        if (result is null)
        {
            Status = Run.LastError is null ? "Run cancelled." : "Run failed: " + Run.LastError;
            if (Run.LastError is not null) ShowLog = true;
            return;
        }
        Results = ResultSession.From(result);
        RawCache.Clear();
        Explorer.Load(Samples.Samples.ToList(), Results);
        await Analytics.LoadAsync(Results);
        Statistics.Load(Results, Analytics.AllSpots, Analytics.Samples);
        Status = $"Run finished in {result.Elapsed:mm\\:ss} — {result.ExportedFiles.Count} files exported to {result.OutputFolder}";
        if (!string.IsNullOrEmpty(ProjectPath))
        {
            await SaveProjectAsync();
        }
        else
        {
            IsDirty = true;
        }
        ShowProgressBand = false;
    }

    [RelayCommand] private void CancelRun() => Run.CancelCommand.Execute(null);
    [RelayCommand] private void HideProgressBand() => ShowProgressBand = false;

    [RelayCommand]
    private async Task ReexportAsync()
    {
        if (Results is null) { Status = "Nothing to export."; return; }
        var path = await _dialogs.SaveFileAsync("Re-export the alignment matrix", (string.IsNullOrEmpty(ProjectName) ? "alignment" : ProjectName) + "_matrix.tsv", "tsv", OutputFolder);
        if (path is null) return;
        try
        {
            await Analytics.ReexportAsync(path);
            Status = $"Exported {path}";
        }
        catch (Exception ex)
        {
            await _messages.ShowErrorAsync("Export failed", ex.Message);
        }
    }

    [RelayCommand] private void OpenOutputFolder() { if (!string.IsNullOrEmpty(OutputFolder)) ShellService.Open(OutputFolder); }

    /// <summary>Set by the window: shows the manual (creating the window the first time) and returns it.</summary>
    public Action? ShowHelpWindow { get; set; }

    /// <summary>The manual, loaded the first time it is asked for.</summary>
    public Help.HelpViewModel EnsureHelp() => Help ??= new Help.HelpViewModel(Desktop.Help.Manual.Load());

    /// <summary>Opens the manual at the page for the workspace that is showing, which is what F1 means.</summary>
    [RelayCommand]
    private void OpenHelp(object? page)
    {
        var help = EnsureHelp();
        var slug = page as string;
        help.Open(string.IsNullOrEmpty(slug) ? Desktop.Help.HelpViewModel.PageForWorkspace(SelectedWorkspace) : slug);
        ShowHelpWindow?.Invoke();
    }
    [RelayCommand] private async Task OpenSettingsAsync() { if (ShowSettings is not null) await ShowSettings(); }
    [RelayCommand] private async Task OpenAboutAsync() { if (ShowAbout is not null) await ShowAbout(); }

    public string UpstreamVersion => PipelineRunner.UpstreamVersion;
}
