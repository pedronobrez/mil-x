using System.Text.RegularExpressions;
using OpenDIAL.Plugins.SciexWiff;
using OpenDIAL.RawData.Plugins;

namespace OpenDIAL.Pipeline.Vendor;

/// <summary>One injection of a SCIEX .wiff batch: the path MS-DIAL should open (a per-sample link for multi-sample files), its index and its name.</summary>
public sealed record WiffSample(string Path, int SampleIndex, string SampleName);

/// <summary>
/// Native SCIEX .wiff support through the OpenDIAL.Plugins.SciexWiff reader. Registers the plugin with
/// OpenDIAL.RawData (so <c>RawDataAccess</c> reads .wiff directly, no msconvert), expands multi-sample batches
/// into one path per sample and re-registers those paths when a project is re-opened. Every entry point is
/// guarded: when the plugin (or the SCIEX SDK) is missing the file simply stays a vendor format.
/// </summary>
public static class WiffSupport
{
    private static readonly object Sync = new();
    private static readonly Regex LinkSuffix = new(@"\.s(\d+)\.wiff2?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static bool _registered;
    private static bool? _native;

    public static bool IsWiff(string? path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        var ext = Path.GetExtension(path);
        return ext.Equals(".wiff", StringComparison.OrdinalIgnoreCase) || ext.Equals(".wiff2", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when the reader was built against the SCIEX SDK (and could be loaded).</summary>
    public static bool IsNativeAvailable
    {
        get
        {
            lock (Sync)
            {
                if (_native is null)
                {
                    try { EnsureRegistered(); _native = SdkAvailable(); }
                    catch { _native = false; }
                }
                return _native.Value;
            }
        }
    }

    /// <summary>Makes sure the plugin is known to <see cref="RawReaderPlugins"/> (also found by the plugins folder scan; either is fine).</summary>
    public static void EnsureRegistered()
    {
        lock (Sync)
        {
            if (_registered) return;
            _registered = true;
            try { RegisterPlugin(); }
            catch
            {
                // plugin assembly missing: .wiff falls back to msconvert
            }
        }
    }

    /// <summary>True when a raw-file reader plugin opens the file directly (no conversion needed).</summary>
    public static bool CanReadNatively(string? path)
    {
        if (string.IsNullOrEmpty(path) || !FileFormats.IsVendorFormat(path)) return false;
        try
        {
            EnsureRegistered();
            return RawReaderPlugins.Find(path) is not null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Expands a .wiff into its samples. Multi-sample batches get one <c>name.sN.wiff</c> link per sample (in
    /// <paramref name="cacheDir"/> or a "wiff-samples" folder next to the file); other files come back as-is.
    /// </summary>
    public static IReadOnlyList<WiffSample> Expand(string path, string? cacheDir = null)
    {
        var full = Path.GetFullPath(path);
        if (!IsWiff(full) || !IsNativeAvailable)
        {
            return new[] { new WiffSample(full, 0, Path.GetFileNameWithoutExtension(full)) };
        }
        return ExpandCore(full, cacheDir);
    }

    /// <summary>Tells the reader which sample a path stands for (needed again after the app restarts).</summary>
    public static void Register(string path, int sampleIndex)
    {
        if (!IsWiff(path) || !IsNativeAvailable) return;
        try { RegisterCore(path, sampleIndex); } catch { }
    }

    /// <summary>Sample index encoded in a link name produced by <see cref="Expand"/> (<c>name.s3.wiff</c> → 2), 0 otherwise.</summary>
    public static int InferSampleIndex(string? path)
    {
        if (string.IsNullOrEmpty(path)) return 0;
        var m = LinkSuffix.Match(Path.GetFileName(path));
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) && n > 0 ? n - 1 : 0;
    }

    // The plugin types are only touched inside these small methods so a missing assembly surfaces as a
    // catchable exception at the call sites above rather than a type-load failure of the whole class.
    private static void RegisterPlugin() => RawReaderPlugins.Register(new SciexWiffReaderPlugin());
    private static bool SdkAvailable() => SciexWiffReaderPlugin.IsSdkAvailable;
    private static void RegisterCore(string path, int sampleIndex) => WiffSampleLinks.Register(path, sampleIndex);
    private static IReadOnlyList<WiffSample> ExpandCore(string full, string? cacheDir)
        => WiffSampleLinks.Expand(full, cacheDir).Select(e => new WiffSample(e.Path, e.SampleIndex, e.SampleName)).ToList();
}
