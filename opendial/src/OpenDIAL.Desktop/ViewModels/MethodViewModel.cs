using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Pipeline.Model;
using OpenDIAL.Pipeline.Parameters;

namespace OpenDIAL.Desktop.ViewModels;

/// <summary>The processing method as a form, with the console method-file text behind it.</summary>
public sealed partial class MethodViewModel : ViewModelBase
{
    private readonly IFileDialogService _dialogs;
    private readonly SettingsService _settings;

    public static IonizationMode[] Modes { get; } = Enum.GetValues<IonizationMode>();

    public MethodViewModel(SettingsService settings, IFileDialogService dialogs)
    {
        _settings = settings;
        _dialogs = dialogs;
        Parameters = new MethodParametersViewModel();
        Parameters.Changed += (_, _) => { OnPropertyChanged(nameof(Summary)); Changed?.Invoke(this, EventArgs.Empty); };
    }

    public MethodParametersViewModel Parameters { get; }

    public event EventHandler? Changed;

    [ObservableProperty] private bool _showAdvancedText;
    [ObservableProperty] private string _message = string.Empty;

    public IonizationMode Mode
    {
        get => Parameters.Mode;
        set
        {
            if (Parameters.Mode == value) return;
            Parameters.Mode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsGcms));
            OnPropertyChanged(nameof(Summary));
        }
    }

    public bool IsGcms => Mode == IonizationMode.GCMS;
    public string Summary => Parameters.Summary;

    public void LoadText(string text, IonizationMode mode)
    {
        Parameters.Mode = mode;
        Parameters.LoadText(text);
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(IsGcms));
        OnPropertyChanged(nameof(Summary));
    }

    public void LoadDefaults(IonizationMode mode)
    {
        Parameters.Mode = mode;
        Parameters.Load(new MethodParameters());
        if (!string.IsNullOrEmpty(_settings.Current.LastMspFile) && File.Exists(_settings.Current.LastMspFile))
        {
            Parameters.MspFilePath = _settings.Current.LastMspFile;
        }
        OnPropertyChanged(nameof(Mode));
        OnPropertyChanged(nameof(IsGcms));
        OnPropertyChanged(nameof(Summary));
    }

    [RelayCommand]
    private async Task LoadMethodAsync()
    {
        var files = await _dialogs.PickFilesAsync("Load a method file", new[] { "*.txt", "*.method" }, allowMultiple: false, Path.GetDirectoryName(_settings.Current.LastMethodFile));
        if (files.Count == 0) return;
        try
        {
            var text = await File.ReadAllTextAsync(files[0]);
            var mode = text.Contains("Machine category: GCMS", StringComparison.OrdinalIgnoreCase) ? IonizationMode.GCMS : Mode;
            LoadText(text, mode);
            _settings.Current.LastMethodFile = files[0];
            _ = _settings.SaveAsync();
            Message = $"Loaded {Path.GetFileName(files[0])}.";
        }
        catch (Exception ex)
        {
            Message = $"Could not load method file: {ex.Message}";
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
            Message = $"Saved {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            Message = $"Could not save method file: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task BrowseMspAsync()
    {
        var files = await _dialogs.PickFilesAsync("Choose an MSP spectral library", new[] { "*.msp", "*.msp2", "*.lbm2", "*.txt" }, allowMultiple: false, Path.GetDirectoryName(Parameters.MspFilePath));
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
        if (files.Count > 0) Parameters.TextDbFilePath = files[0];
    }

    [RelayCommand]
    private void ResetDefaults() => LoadDefaults(Mode);
}
