using System.Collections.Concurrent;

namespace OpenDIAL.Plugins.SciexWiff;

/// <summary>
/// A .wiff batch file can hold several injections (samples). MS-DIAL identifies an analysis
/// file by its path only, so to process every sample of a batch each one needs a path of its
/// own. This helper creates one symbolic link per sample (<c>name.sN.wiff</c> plus the matching
/// <c>.wiff.scan</c> link) in a cache folder and registers which sample each link stands for,
/// so that <see cref="SciexWiffReaderPlugin"/> opens the right one.
/// </summary>
public static class WiffSampleLinks
{
    private static readonly ConcurrentDictionary<string, int> Registry = new(StringComparer.OrdinalIgnoreCase);

    static WiffSampleLinks() {
        SciexWiffReaderPlugin.SampleSelector = (path, _) => Registry.TryGetValue(Path.GetFullPath(path), out var idx) ? idx : -1;
    }

    public sealed record WiffSampleEntry(string Path, int SampleIndex, string SampleName);

    /// <summary>Registers that <paramref name="path"/> must be read as sample <paramref name="sampleIndex"/>.</summary>
    public static void Register(string path, int sampleIndex) {
        Registry[Path.GetFullPath(path)] = sampleIndex;
    }

    /// <summary>
    /// Expands a .wiff into one entry per sample. Single-sample files are returned as-is; for
    /// multi-sample files symbolic links are created under <paramref name="cacheDir"/>
    /// (default: a "wiff-samples" folder next to the file).
    /// </summary>
    public static IReadOnlyList<WiffSampleEntry> Expand(string wiffPath, string? cacheDir = null) {
        // a .wiff2 is read through the .wiff beside it, so that is what the links point at
        var full = Path.GetFullPath(SciexWiffReaderPlugin.ResolvePath(wiffPath));
        var names = SciexWiffReaderPlugin.ListSamples(full);
        if (names.Count <= 1) {
            return new[] { new WiffSampleEntry(full, 0, names.Count == 1 ? names[0] : Path.GetFileNameWithoutExtension(full)) };
        }
        var dir = cacheDir ?? Path.Combine(Path.GetDirectoryName(full)!, "wiff-samples");
        Directory.CreateDirectory(dir);
        var stem = Path.GetFileNameWithoutExtension(full);
        var scan = full + ".scan";
        var result = new List<WiffSampleEntry>();
        for (var i = 0; i < names.Count; i++) {
            var link = Path.Combine(dir, $"{stem}.s{i + 1}.wiff");
            EnsureLink(link, full);
            if (File.Exists(scan)) EnsureLink(link + ".scan", scan);
            Register(link, i);
            result.Add(new WiffSampleEntry(link, i, names[i]));
        }
        return result;
    }

    private static void EnsureLink(string link, string target) {
        if (File.Exists(link)) {
            var info = new FileInfo(link);
            if (info.LinkTarget == target) return;
            File.Delete(link);
        }
        try {
            File.CreateSymbolicLink(link, target);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            // fall back to a hard copy when links are not permitted (e.g. some network shares)
            File.Copy(target, link, overwrite: true);
        }
    }
}
