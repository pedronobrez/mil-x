namespace MilX.Pipeline.Vendor;

/// <summary>User settings for the msconvert bridge. Persisted by the GUI and handed to <see cref="IVendorConversionService"/>.</summary>
public sealed class VendorConversionOptions
{
    /// <summary>Path to a native msconvert executable (ProteoWizard). Empty = not configured.</summary>
    public string MsconvertPath { get; set; } = string.Empty;

    /// <summary>Run msconvert through Docker (proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses) instead of a native binary.</summary>
    public bool UseDocker { get; set; } = false;

    public string DockerImage { get; set; } = "proteowizard/pwiz-skyline-i-agree-to-the-vendor-licenses:latest";

    /// <summary>Folder where converted mzML files are cached. Empty = next to the source file.</summary>
    public string ConversionCacheFolder { get; set; } = string.Empty;
}
