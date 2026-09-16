namespace MilX.Pipeline.Vendor;

/// <summary>
/// Converts vendor raw formats to mzML before the upstream reader opens them.
/// The real implementation lives in MilX.RawData (msconvert, native or Docker);
/// <see cref="DefaultVendorConversionService"/> is the placeholder used until it is plugged in.
/// </summary>
public interface IVendorConversionService
{
    bool IsVendorFormat(string path);

    /// <summary>Returns an mzML path for <paramref name="path"/>, converting if necessary.</summary>
    Task<string> EnsureMzmlAsync(string path, IProgress<string> log, CancellationToken ct);
}

public sealed class DefaultVendorConversionService : IVendorConversionService
{
    private readonly VendorConversionOptions _options;

    public DefaultVendorConversionService(VendorConversionOptions? options = null)
    {
        _options = options ?? new VendorConversionOptions();
    }

    public VendorConversionOptions Options => _options;

    public bool IsVendorFormat(string path) => FileFormats.IsVendorFormat(path);

    public Task<string> EnsureMzmlAsync(string path, IProgress<string> log, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsVendorFormat(path))
        {
            return Task.FromResult(path);
        }
        var hint = _options.UseDocker
            ? $"Docker image '{_options.DockerImage}'"
            : string.IsNullOrWhiteSpace(_options.MsconvertPath) ? "no msconvert path configured" : $"msconvert at '{_options.MsconvertPath}'";
        throw new NotSupportedException(
            $"'{Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar))}' is a vendor format ({FileFormats.GetExtension(path)}). " +
            "Converting vendor data requires the msconvert bridge (MilX.RawData), which is not available in this build. " +
            $"Current settings: {hint}. Convert the file to mzML with ProteoWizard msconvert and add the .mzML instead.");
    }
}
