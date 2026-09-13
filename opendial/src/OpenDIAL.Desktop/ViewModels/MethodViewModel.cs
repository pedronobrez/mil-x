using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Pipeline.Model;
using OpenDIAL.Pipeline.Parameters;
using OpenDIAL.Pipeline.Results;

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
        Parameters.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(Summary));
            Changed?.Invoke(this, EventArgs.Empty);
            if (!string.Equals(_scannedLibrary, Parameters.MspFilePath, StringComparison.Ordinal)) _ = ScanLibraryAsync();
        };
        _ = ScanLibraryAsync();
    }

    // ---- the library, read rather than assumed -------------------------------------------------

    private string? _scannedLibrary;

    [ObservableProperty] private string _libraryReport = "No library chosen. Every feature will stay unknown.";
    [ObservableProperty] private string _libraryWarning = string.Empty;
    [ObservableProperty] private bool _libraryTimesLookForeign;

    /// <summary>Set by the shell: the names and retention times the current result actually saw.</summary>
    public Func<IReadOnlyDictionary<string, double>>? ObservedRetentionTimes { get; set; }

    /// <summary>
    /// What the chosen library holds — how many records, which adducts, what range of mass and time.
    /// A library is the biggest single lever on how many features get a name, and until now the
    /// interface said nothing at all about the one that was loaded.
    /// </summary>
    [RelayCommand]
    private async Task ScanLibraryAsync()
    {
        var path = Parameters.MspFilePath;
        _scannedLibrary = path;
        if (string.IsNullOrWhiteSpace(path))
        {
            LibraryReport = "No library chosen. Every feature will stay unknown.";
            LibraryWarning = string.Empty;
            LibraryTimesLookForeign = false;
            return;
        }
        if (!File.Exists(path))
        {
            LibraryReport = "No file at " + path;
            LibraryWarning = string.Empty;
            LibraryTimesLookForeign = false;
            return;
        }
        LibraryReport = "Reading " + Path.GetFileName(path) + "…";
        try
        {
            var summary = await Task.Run(() => SpectralLibrary.Scan(path));
            LibraryReport = summary.Sentence();
            var fits = summary.RetentionTimesCouldFit(Parameters.RetentionTimeBegin, Parameters.RetentionTimeEnd);
            LibraryTimesLookForeign = summary.CarriesRetentionTimes && !fits;
            LibraryWarning = LibraryTimesLookForeign
                ? $"Its retention times ({summary.RetentionTimeLow:0.#}–{summary.RetentionTimeHigh:0.#} min) do not overlap this method's window ({Parameters.RetentionTimeBegin:0.#}–{Parameters.RetentionTimeEnd:0.#} min). Scoring with them throws correct matches away."
                : string.Empty;
        }
        catch (Exception ex)
        {
            LibraryReport = "Could not read the library: " + ex.Message;
        }
    }

    /// <summary>Leaves the library's own times out of the score, which is the default anyway.</summary>
    [RelayCommand]
    private void IgnoreLibraryRetentionTimes()
    {
        Parameters.UseRetentionInformationForScoring = false;
        Parameters.UseRetentionInformationForFiltering = false;
        Message = "The library's retention times are out of the score.";
    }

    /// <summary>
    /// Fits the library's times to the ones this run measured for what it named, writes a calibrated
    /// copy beside the original, and points the method at it. Then the retention term is worth
    /// having, so it goes back into the score.
    /// </summary>
    [RelayCommand]
    private async Task CalibrateLibraryRetentionTimesAsync()
    {
        var observed = ObservedRetentionTimes?.Invoke();
        if (observed is null || observed.Count == 0)
        {
            Message = "Process a batch first: the calibration is fitted to what this run saw.";
            return;
        }
        var path = Parameters.MspFilePath;
        Message = "Fitting the library's retention times…";
        try
        {
            var fit = await Task.Run(() => SpectralLibrary.Recalibrate(path, observed));
            if (fit is null)
            {
                Message = $"Not enough shared names to fit: {observed.Count} named feature(s), and fewer than twenty of them are in the library with a time.";
                return;
            }
            Parameters.MspFilePath = fit.WrittenTo;
            Parameters.UseRetentionInformationForScoring = true;
            Message = $"Calibrated on {fit.Pairs} compounds (R² {fit.RSquared:0.###}): library time × {fit.Slope:0.###} + {fit.Intercept:0.##} min. Written to {Path.GetFileName(fit.WrittenTo)}, and the method now points at it.";
            await ScanLibraryAsync();
        }
        catch (Exception ex)
        {
            Message = "Could not calibrate: " + ex.Message;
        }
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
