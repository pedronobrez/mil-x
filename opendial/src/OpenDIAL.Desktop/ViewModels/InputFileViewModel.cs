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

    public InputFileViewModel(string path, int order, AcquisitionMode acquisition)
    {
        Path = path;
        FileName = System.IO.Path.GetFileName(path.TrimEnd(System.IO.Path.DirectorySeparatorChar));
        _name = System.IO.Path.GetFileNameWithoutExtension(FileName);
        _class = "1";
        _analyticalOrder = order;
        _acquisition = acquisition;
        IsVendorFormat = FileFormats.IsVendorFormat(path);
        Exists = File.Exists(path) || Directory.Exists(path);
        var ext = FileFormats.GetExtension(path);
        Badge = IsVendorFormat
            ? (ext == ".wiff" || ext == ".wiff2" ? "wiff · msconvert" : $"{ext.TrimStart('.')} · msconvert")
            : ext == ".mzml" ? "mzML" : ext.TrimStart('.').ToUpperInvariant();
        BadgeIsWarning = IsVendorFormat;
    }

    public string Path { get; }
    public string FileName { get; }
    public bool IsVendorFormat { get; }
    public bool Exists { get; }
    public string Badge { get; }
    public bool BadgeIsWarning { get; }
    public string Tooltip => Exists ? Path : Path + "\n(file not found)";

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
    };

    public static InputFileViewModel FromProjectSample(ProjectSample s) => new(s.Path, s.AnalyticalOrder, s.Acquisition)
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
