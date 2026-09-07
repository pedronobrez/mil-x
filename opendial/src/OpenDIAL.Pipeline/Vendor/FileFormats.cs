namespace OpenDIAL.Pipeline.Vendor;

/// <summary>Raw-data formats the file picker accepts and how they are handled.</summary>
public static class FileFormats
{
    /// <summary>Formats the upstream reader opens directly (open formats plus MS-DIAL's own containers).</summary>
    public static readonly IReadOnlyList<string> NativeExtensions = new[] { ".mzml", ".abf", ".cdf", ".ibf", ".lcd", ".qgd" };

    /// <summary>Vendor formats that must go through the msconvert bridge (OpenDIAL.RawData) first.</summary>
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
        => IsVendorFormat(path) ? "vendor format – requires msconvert bridge" : string.Empty;

    /// <summary>Enumerates supported raw files (and vendor directories) directly under <paramref name="folder"/>.</summary>
    public static IEnumerable<string> EnumerateRawFiles(string folder)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(folder, "*", SearchOption.TopDirectoryOnly).OrderBy(e => e, StringComparer.OrdinalIgnoreCase))
        {
            var ext = GetExtension(entry);
            var isVendorDirectory = Directory.Exists(entry) && (ext == ".raw" || ext == ".d");
            if (isVendorDirectory || (File.Exists(entry) && IsSupported(entry)))
            {
                yield return entry;
            }
        }
    }
}
