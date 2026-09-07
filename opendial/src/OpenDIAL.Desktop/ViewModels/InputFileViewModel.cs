using CommunityToolkit.Mvvm.ComponentModel;
using OpenDIAL.Pipeline.Model;
using OpenDIAL.Pipeline.Vendor;

namespace OpenDIAL.Desktop.ViewModels;

/// <summary>Editable row of the input-file table.</summary>
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
        Badge = FileFormats.GetBadge(path);
    }

    public string Path { get; }
    public string FileName { get; }
    public bool IsVendorFormat { get; }
    public string Badge { get; }
    public bool HasBadge => Badge.Length > 0;

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _class;
    [ObservableProperty] private SampleType _sampleType = SampleType.Sample;
    [ObservableProperty] private AcquisitionMode _acquisition;
    [ObservableProperty] private int _analyticalOrder;
    [ObservableProperty] private bool _included = true;

    public InputFile ToModel() => new(Path)
    {
        Name = string.IsNullOrWhiteSpace(Name) ? System.IO.Path.GetFileNameWithoutExtension(FileName) : Name.Trim(),
        Class = string.IsNullOrWhiteSpace(Class) ? "1" : Class.Trim(),
        SampleType = SampleType,
        Acquisition = Acquisition,
        AnalyticalOrder = AnalyticalOrder,
        Included = Included,
    };
}
