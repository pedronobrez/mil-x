namespace MilX.Pipeline.Vendor;

/// <summary>Raw-data formats the file picker accepts and how they are handled.</summary>
public static class FileFormats
{
    /// <summary>Formats the upstream reader opens directly (open formats plus MS-DIAL's own containers).</summary>
    public static readonly IReadOnlyList<string> NativeExtensions = new[] { ".mzml", ".abf", ".cdf", ".ibf", ".lcd", ".qgd" };

    /// <summary>Vendor formats that must go through the msconvert bridge (MilX.RawData) first.</summary>
    public static readonly IReadOnlyList<string> VendorExtensions = new[] { ".raw", ".d", ".wiff", ".wiff2" };

    public static IReadOnlyList<string> AllExtensions { get; } = NativeExtensions.Concat(VendorExtensions).ToArray();

    /// <summary>File-picker patterns (e.g. "*.mzML").</summary>
    public static IReadOnlyList<string> PickerPatterns { get; } = new[] { "*.mzML", "*.mzml", "*.raw", "*.d", "*.wiff", "*.wiff2", "*.abf", "*.cdf", "*.ibf" };

    public static string GetExtension(string path)
        => Path.GetExtension(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).ToLowerInvariant();

    public static bool IsSupported(string path) => AllExtensions.Contains(GetExtension(path));

    public static bool IsVendorFormat(string path) => VendorExtensions.Contains(GetExtension(path));

    public static bool IsMzml(string path) => GetExtension(path) == ".mzml";

    /// <summary>Human readable badge for the GUI; empty for open formats.</summary>
    public static string GetBadge(string path)
        => IsVendorFormat(path) ? (WiffSupport.CanReadNatively(path) ? "read natively" : "vendor format – requires msconvert bridge") : string.Empty;

    /// <summary>Enumerates supported raw files (and vendor directories) directly under <paramref name="folder"/>, one per acquisition.</summary>
    public static IEnumerable<string> EnumerateRawFiles(string folder)
    {
        var found = new List<string>();
        foreach (var entry in Directory.EnumerateFileSystemEntries(folder, "*", SearchOption.TopDirectoryOnly).OrderBy(e => e, StringComparer.OrdinalIgnoreCase))
        {
            var ext = GetExtension(entry);
            var isVendorDirectory = Directory.Exists(entry) && (ext == ".raw" || ext == ".d");
            if (isVendorDirectory || (File.Exists(entry) && IsSupported(entry)))
            {
                found.Add(entry);
            }
        }
        return OnePerAcquisition(found);
    }

    /// <summary>
    /// SCIEX OS writes a .wiff and a .wiff2 for every acquisition, both over the one .wiff.scan;
    /// adding both would process the same injection twice. The .wiff is kept — it is the one the
    /// reader opens — and the .wiff2 beside it is dropped.
    /// </summary>
    public static IEnumerable<string> OnePerAcquisition(IEnumerable<string> paths)
    {
        var list = paths.ToList();
        var wiffs = new HashSet<string>(list.Where(p => GetExtension(p) == ".wiff").Select(p => Path.ChangeExtension(Path.GetFullPath(p), null)), StringComparer.OrdinalIgnoreCase);
        foreach (var p in list)
        {
            if (GetExtension(p) == ".wiff2")
            {
                var stem = Path.ChangeExtension(Path.GetFullPath(p), null);
                if (wiffs.Contains(stem) || File.Exists(Path.ChangeExtension(Path.GetFullPath(p), ".wiff"))) continue;
            }
            yield return p;
        }
    }

    /// <summary>Why a path was left out of a batch, or null when it was not.</summary>
    public static string? WhyDropped(string path) =>
        GetExtension(path) == ".wiff2" && File.Exists(Path.ChangeExtension(Path.GetFullPath(path), ".wiff"))
            ? $"{Path.GetFileName(path)} is the same acquisition as {Path.GetFileNameWithoutExtension(path)}.wiff, which is the one read"
            : null;
}
