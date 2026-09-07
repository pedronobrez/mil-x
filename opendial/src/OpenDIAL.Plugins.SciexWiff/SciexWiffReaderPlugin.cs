using CompMs.Common.DataObj;
using OpenDIAL.RawData.Plugins;

namespace OpenDIAL.Plugins.SciexWiff;

/// <summary>
/// Native SCIEX .wiff / .wiff.scan reader (IRawFileReaderPlugin). Reads the acquisition through
/// SCIEX's own Clearcore2 assemblies, exactly like OpenQuant does, so no conversion is needed.
///
/// Options (environment variables):
///   OPENDIAL_WIFF_SAMPLE     sample index (0-based) or sample name inside a multi-sample .wiff (default 0)
///   OPENDIAL_WIFF_CENTROID   1 (default) centroid profile spectra (MS-DIAL's local-maximum method),
///                            0 keep profile data (set "MS1/MS2 data type: Profile" in the method)
///   OPENDIAL_WIFF_MIN_INTENSITY  drop centroided peaks below this intensity (default 0)
/// </summary>
public sealed class SciexWiffReaderPlugin : IRawFileReaderPlugin
{
    public string Name => "SCIEX WIFF (Clearcore2)";
    public int Priority => 10;

    /// <summary>Selects which sample of a multi-sample .wiff is read; receives (path, sampleNames) and returns an index.</summary>
    public static Func<string, IReadOnlyList<string>, int>? SampleSelector { get; set; }

    public static bool IsSdkAvailable {
        get {
#if SCIEX_SDK
            return true;
#else
            return false;
#endif
        }
    }

    public bool CanRead(string path) {
        if (string.IsNullOrEmpty(path)) return false;
        var ext = Path.GetExtension(path);
        if (!ext.Equals(".wiff", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".wiff2", StringComparison.OrdinalIgnoreCase)) return false;
        return IsSdkAvailable && File.Exists(path);
    }

    public RawMeasurement Read(string path, int fileId, RawReadOptions options) {
#if SCIEX_SDK
        return ClearcoreReader.Read(path, fileId, options);
#else
        throw new NotSupportedException("The SCIEX WIFF plugin was built without the Clearcore2 SDK assemblies. Run scripts/fetch-sciex-assemblies.sh and rebuild.");
#endif
    }

    /// <summary>Sample names inside a .wiff (a batch file can hold several injections).</summary>
    public static IReadOnlyList<string> ListSamples(string path) {
#if SCIEX_SDK
        return ClearcoreReader.ListSamples(path);
#else
        throw new NotSupportedException("SCIEX SDK not available in this build.");
#endif
    }

    internal static int ResolveSampleIndex(string path, IReadOnlyList<string> names) {
        if (names.Count == 0) return 0;
        if (SampleSelector != null) {
            var chosen = SampleSelector(path, names);
            if (chosen >= 0 && chosen < names.Count) return chosen;
        }
        var env = Environment.GetEnvironmentVariable("OPENDIAL_WIFF_SAMPLE");
        if (!string.IsNullOrWhiteSpace(env)) {
            if (int.TryParse(env, out var idx) && idx >= 0 && idx < names.Count) return idx;
            for (var i = 0; i < names.Count; i++) {
                if (string.Equals(names[i], env, StringComparison.OrdinalIgnoreCase)) return i;
            }
        }
        return 0;
    }
}
