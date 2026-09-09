using CommunityToolkit.Mvvm.ComponentModel;
using OpenDIAL.Pipeline.Model;
using OpenDIAL.Pipeline.Project;
using OpenDIAL.Pipeline.Vendor;

namespace OpenDIAL.Desktop.ViewModels;

/// <summary>Editable row of the batch table (Samples workspace).</summary>
public sealed partial class InputFileViewModel : ViewModelBase
{
    public static SampleType[] SampleTypes { get; } = Enum.GetValues<SampleType>();
    public static AcquisitionMode[] AcquisitionModes { get; } = Enum.GetValues<AcquisitionMode>();

    public InputFileViewModel(string path, int order, AcquisitionMode acquisition, int sampleIndex = 0, string? sampleName = null)
    {
        Path = path;
        SampleIndex = sampleIndex;
        FileName = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        _name = string.IsNullOrWhiteSpace(sampleName) ? System.IO.Path.GetFileNameWithoutExtension(FileName) : sampleName.Trim();
        _class = "1";
        _analyticalOrder = order;
        _acquisition = acquisition;
        IsVendorFormat = FileFormats.IsVendorFormat(path);
        Exists = File.Exists(path) || Directory.Exists(path);
        var ext = FileFormats.GetExtension(path);
        var isWiff = ext == ".wiff" || ext == ".wiff2";
        ReadsNatively = isWiff && WiffSupport.CanReadNatively(path);
        Badge = IsVendorFormat
            ? (isWiff ? (ReadsNatively ? "wiff · native" : "wiff · msconvert") : $"{ext.TrimStart('.')} · msconvert")
            : ext == ".mzml" ? "mzML" : ext.TrimStart('.').ToUpperInvariant();
        BadgeIsWarning = IsVendorFormat && !ReadsNatively;
        if (isWiff) WiffSupport.Register(path, sampleIndex);
    }

    public string Path { get; }
    public string FileName { get; }
    public bool IsVendorFormat { get; }
    /// <summary>A vendor file that a raw-file plugin opens directly (no msconvert).</summary>
    public bool ReadsNatively { get; }
    /// <summary>True when the file must go through msconvert before processing.</summary>
    public bool NeedsConversion => IsVendorFormat && !ReadsNatively;
    /// <summary>0-based sample inside a multi-sample .wiff batch (0 for any other file).</summary>
    public int SampleIndex { get; }
    public bool Exists { get; }
    public string Badge { get; }
    public bool BadgeIsWarning { get; }
    public string Tooltip => (Exists ? Path : Path + "\n(file not found)") + (SampleIndex > 0 ? $"\nsample {SampleIndex + 1} of the batch" : string.Empty);

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _class;
    [ObservableProperty] private SampleType _sampleType = SampleType.Sample;
    [ObservableProperty] private AcquisitionMode _acquisition;
    [ObservableProperty] private int _analyticalOrder;
    [ObservableProperty] private int _batch = 1;
    [ObservableProperty] private double _dilution = 1;
    [ObservableProperty] private string _comment = string.Empty;
    [ObservableProperty] private bool _included = true;

    /// <summary>Raised for any edit so the shell can mark the project dirty.</summary>
    public event EventHandler? Edited;

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        Edited?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Rows for one path: a .wiff batch expands into one row per sample (named after the sample in the batch),
    /// anything else is a single row. A broken .wiff is reported through <paramref name="error"/> and added as one row.
    /// </summary>
    public static IReadOnlyList<InputFileViewModel> FromPath(string path, int firstOrder, AcquisitionMode acquisition, out string? error)
    {
        error = null;
        if (WiffSupport.IsWiff(path) && WiffSupport.IsNativeAvailable && (File.Exists(path) || Directory.Exists(path)))
        {
            try
            {
                var samples = WiffSupport.Expand(path);
                var rows = new List<InputFileViewModel>(samples.Count);
                foreach (var s in samples)
                {
                    rows.Add(new InputFileViewModel(s.Path, firstOrder + rows.Count, acquisition, s.SampleIndex, s.SampleName));
                }
                if (rows.Count > 0) return rows;
            }
            catch (Exception ex)
            {
                error = $"{System.IO.Path.GetFileName(path)}: {ex.Message}";
            }
        }
        return new[] { new InputFileViewModel(path, firstOrder, acquisition) };
    }

    public InputFile ToModel() => new(Path)
    {
        Name = string.IsNullOrWhiteSpace(Name) ? System.IO.Path.GetFileNameWithoutExtension(FileName) : Name.Trim(),
        Class = string.IsNullOrWhiteSpace(Class) ? "1" : Class.Trim(),
        SampleType = SampleType,
        Acquisition = Acquisition,
        AnalyticalOrder = AnalyticalOrder,
        Batch = Batch,
        Included = Included,
    };

    public ProjectSample ToProjectSample() => new()
    {
        Path = Path,
        Name = Name,
        Class = Class,
        SampleType = SampleType,
        Acquisition = Acquisition,
        AnalyticalOrder = AnalyticalOrder,
        Batch = Batch,
        Dilution = Dilution,
        Comment = Comment,
        Included = Included,
        SampleIndex = SampleIndex,
    };

    public static InputFileViewModel FromProjectSample(ProjectSample s) => new(s.Path, s.AnalyticalOrder, s.Acquisition, s.SampleIndex > 0 ? s.SampleIndex : WiffSupport.InferSampleIndex(s.Path), s.Name)
    {
        Name = s.Name,
        Class = s.Class,
        SampleType = s.SampleType,
        Batch = s.Batch,
        Dilution = s.Dilution,
        Comment = s.Comment,
        Included = s.Included,
    };
}
