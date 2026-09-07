using OpenDIAL.Pipeline.Parameters;
using OpenDIAL.Pipeline.Vendor;

namespace OpenDIAL.Pipeline.Model;

/// <summary>Everything <see cref="PipelineRunner"/> needs to run one MS-DIAL processing job.</summary>
public sealed class PipelineRequest
{
    public List<InputFile> InputFiles { get; } = new();

    /// <summary>Folder where all intermediate (.pai2/.dcl/.arf2) and exported files are written.</summary>
    public string OutputFolder { get; set; } = string.Empty;

    public MethodParameters Parameters { get; set; } = new();

    public IonizationMode Mode { get; set; } = IonizationMode.LCMS;

    /// <summary>When true a .mdproject/.mddata pair is written so results can be re-opened without re-processing.</summary>
    public bool SaveProject { get; set; } = true;

    /// <summary>Optional bridge for vendor formats; when null <see cref="DefaultVendorConversionService"/> is used.</summary>
    public IVendorConversionService? VendorConversion { get; set; }
}
