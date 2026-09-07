using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Pipeline;
using OpenDIAL.Pipeline.Results;

namespace OpenDIAL.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IFileDialogService _dialogs;

    public MainWindowViewModel(SettingsService settings, IFileDialogService dialogs)
    {
        Settings = settings;
        _dialogs = dialogs;
        Project = new ProjectViewModel(settings, dialogs);
        Run = new RunViewModel();
        Results = new ResultsViewModel();
        Run.Completed += async (_, result) =>
        {
            await Results.LoadSessionAsync(ResultSession.From(result));
            SelectedTab = 2;
        };
    }

    public SettingsService Settings { get; }
    public ProjectViewModel Project { get; }
    public RunViewModel Run { get; }
    public ResultsViewModel Results { get; }

    public string Title => $"OpenDIAL — open port of MS-DIAL {PipelineRunner.UpstreamVersion}";

    [ObservableProperty] private int _selectedTab;
    [ObservableProperty] private string _globalStatus = "Ready";

    [RelayCommand]
    private async Task RunPipelineAsync()
    {
        if (Run.IsRunning)
        {
            GlobalStatus = "A run is already in progress.";
            SelectedTab = 1;
            return;
        }
        var request = Project.BuildRequest(out var error);
        if (request is null)
        {
            Project.StatusMessage = error ?? "Invalid project.";
            GlobalStatus = Project.StatusMessage;
            return;
        }
        SelectedTab = 1;
        GlobalStatus = "Running…";
        var result = await Run.RunAsync(request);
        GlobalStatus = result is null ? (Run.LastError ?? "Run cancelled") : $"Run finished — {result.ExportedFiles.Count} exports";
    }

    [RelayCommand]
    private async Task OpenResultsFolderAsync()
    {
        var folder = await _dialogs.PickFolderAsync("Open an OpenDIAL / MS-DIAL console output folder", Settings.Current.LastOutputFolder);
        if (folder is null) return;
        await OpenResultsAsync(folder);
    }

    [RelayCommand]
    private async Task OpenProjectFileAsync()
    {
        var files = await _dialogs.PickFilesAsync("Open a .mdproject file", new[] { "*.mdproject" }, allowMultiple: false, Settings.Current.LastOutputFolder);
        if (files.Count == 0) return;
        await OpenResultsAsync(files[0]);
    }

    public async Task OpenResultsAsync(string path)
    {
        GlobalStatus = $"Opening {path}…";
        try
        {
            var opened = await ProjectOpener.OpenAsync(path);
            await Results.LoadSessionAsync(ResultSession.From(opened));
            SelectedTab = 2;
            GlobalStatus = $"Opened {opened.AnalysisFiles.Count} file(s) from {opened.Folder} ({opened.Source})";
        }
        catch (Exception ex)
        {
            GlobalStatus = "Could not open results: " + ex.Message;
        }
    }
}
