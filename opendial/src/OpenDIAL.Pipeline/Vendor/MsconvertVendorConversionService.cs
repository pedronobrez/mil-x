using CompMs.RawDataHandler.Core;
using OpenDIAL.RawData.Vendor;

namespace OpenDIAL.Pipeline.Vendor;

/// <summary>
/// Real vendor bridge: delegates to <see cref="VendorConverter"/> from OpenDIAL.RawData
/// (native msconvert or the ProteoWizard Docker image). It also installs the same converter
/// as the process-wide default so that raw files opened later by MS-DIAL's own data providers
/// (alignment, gap filling, EIC display) are converted with identical settings.
/// </summary>
public sealed class MsconvertVendorConversionService : IVendorConversionService
{
    private readonly VendorConverter _converter;

    public MsconvertVendorConversionService(VendorConversionOptions? options = null, Action<string>? log = null)
    {
        options ??= new VendorConversionOptions();
        var converterOptions = new VendorConverterOptions {
            Log = log,
        };
        if (!string.IsNullOrWhiteSpace(options.MsconvertPath)) converterOptions.MsconvertPath = options.MsconvertPath;
        if (options.UseDocker) converterOptions.MsconvertPath = "docker";
        if (!string.IsNullOrWhiteSpace(options.DockerImage)) converterOptions.DockerImage = options.DockerImage;
        if (!string.IsNullOrWhiteSpace(options.ConversionCacheFolder)) converterOptions.CacheDirectory = options.ConversionCacheFolder;
        _converter = new VendorConverter(converterOptions);
        RawDataAccessOptions.Vendor = _converter;
        if (log != null) RawDataAccessOptions.Log = log;
    }

    public VendorConverter Converter => _converter;

    public bool IsVendorFormat(string path) => FileFormats.IsVendorFormat(path) || VendorConverter.IsVendorPath(path);

    public ConverterAvailability Detect() => _converter.Detect();

    public async Task<string> EnsureMzmlAsync(string path, IProgress<string> log, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsVendorFormat(path)) return path;
        var previous = _converter.Options.Log;
        _converter.Options.Log = m => { previous?.Invoke(m); log.Report(m); };
        try {
            return await _converter.EnsureMzmlAsync(path, ct).ConfigureAwait(false);
        }
        finally {
            _converter.Options.Log = previous;
        }
    }
}
