using CompMs.Common.DataObj;
using OpenDIAL.RawData.Plugins;

namespace OpenDIAL.Plugins.SciexWiff;

/// <summary>
/// Native SCIEX .wiff / .wiff.scan reader (IRawFileReaderPlugin). Reads the acquisition through
/// SCIEX's own Clearcore2 assemblies, exactly like OpenQuant does, so no conversion is needed.
///
/// A .wiff2 is the same acquisition in SCIEX OS's newer container; the SDK's .wiff2 path needs
/// System.Data.SQLite's native library, which does not exist for this platform, but SCIEX OS
/// writes a .wiff beside every .wiff2 (both point at the one .wiff.scan), so a .wiff2 is read
/// through the .wiff next to it. A .wiff2 on its own is left to the msconvert bridge.
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
        if (ext.Equals(".wiff2", StringComparison.OrdinalIgnoreCase)) return IsSdkAvailable && SiblingWiff(path) is not null;
        if (!ext.Equals(".wiff", StringComparison.OrdinalIgnoreCase)) return false;
        return IsSdkAvailable && File.Exists(path);
    }

    /// <summary>The .wiff SCIEX OS wrote beside a .wiff2 — the same acquisition, the same .wiff.scan — or null when there is none.</summary>
    public static string? SiblingWiff(string path) {
        if (string.IsNullOrEmpty(path) || !Path.GetExtension(path).Equals(".wiff2", StringComparison.OrdinalIgnoreCase)) return null;
        var sibling = Path.ChangeExtension(path, ".wiff");
        return File.Exists(sibling) ? sibling : null;
    }

    /// <summary>The path the SDK actually opens: a .wiff2 becomes the .wiff beside it, anything else is itself.</summary>
    public static string ResolvePath(string path) => SiblingWiff(path) ?? path;

    public RawMeasurement Read(string path, int fileId, RawReadOptions options) {
#if SCIEX_SDK
        var sibling = SiblingWiff(path);
        if (sibling is not null) {
            options.Log?.Invoke($"[wiff] {Path.GetFileName(path)}: read through {Path.GetFileName(sibling)}, the same acquisition in the container the SDK opens here");
            // whichever sample the .wiff2 path was registered for is the sample of the .wiff too
            var registered = SampleSelector?.Invoke(Path.GetFullPath(path), Array.Empty<string>()) ?? -1;
            if (registered >= 0) WiffSampleLinks.Register(sibling, registered);
            return ClearcoreReader.Read(sibling, fileId, options);
        }
        if (Path.GetExtension(path).Equals(".wiff2", StringComparison.OrdinalIgnoreCase)) {
            throw new NotSupportedException($"{Path.GetFileName(path)}: a .wiff2 on its own cannot be opened natively on this platform (the SDK's .wiff2 reader needs System.Data.SQLite's native library); keep the .wiff SCIEX OS writes beside it, or let the msconvert bridge convert it.");
        }
        return ClearcoreReader.Read(path, fileId, options);
#else
        throw new NotSupportedException("The SCIEX WIFF plugin was built without the Clearcore2 SDK assemblies. Run scripts/fetch-sciex-assemblies.sh and rebuild.");
#endif
    }

    /// <summary>Sample names inside a .wiff (a batch file can hold several injections); a .wiff2 answers through the .wiff beside it.</summary>
    public static IReadOnlyList<string> ListSamples(string path) {
#if SCIEX_SDK
        return ClearcoreReader.ListSamples(ResolvePath(path));
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
