using System.Security.Cryptography;
using System.Text;

namespace MilX.Pipeline.Caching;

/// <summary>
/// Keeps the spectra of the raw files on disk, so the second time a project is opened the
/// chromatograms, the channel tree and the product spectra all come back without the vendor reader. Entries are keyed by the file's own identity
/// — path, size and last write time — so an edited or replaced file never serves a stale entry.
///
/// The store is capped and evicted least-recently-used, which is what makes it safe to leave on: a
/// lipidomics batch is a few hundred megabytes of survey scans and a working directory holds many.
/// </summary>
public sealed class RawSnapshotCache
{
    private const string Extension = ".od2";

    public RawSnapshotCache(string? root = null, long capacityBytes = 8L * 1024 * 1024 * 1024)
    {
        Root = root ?? DefaultRoot();
        CapacityBytes = capacityBytes;
    }

    public string Root { get; }
    public long CapacityBytes { get; }
    public bool Enabled { get; set; } = true;

    /// <summary>~/Library/Caches/MIL-X on macOS, $XDG_CACHE_HOME/MIL-X on Linux, LocalAppData on Windows.</summary>
    public static string DefaultRoot()
    {
        var overridden = Environment.GetEnvironmentVariable("MILX_CACHE");
        if (!string.IsNullOrWhiteSpace(overridden)) return Path.Combine(overridden, "spectra");
        string baseDir;
        if (OperatingSystem.IsMacOS())
        {
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library", "Caches");
        }
        else if (OperatingSystem.IsWindows())
        {
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        }
        else
        {
            baseDir = Environment.GetEnvironmentVariable("XDG_CACHE_HOME") is { Length: > 0 } xdg
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");
        }
        return Path.Combine(baseDir, "MIL-X", "spectra");
    }

    /// <summary>Identity of the raw file: a different size or write time is a different entry.</summary>
    public static string KeyFor(string rawPath)
    {
        var full = Path.GetFullPath(rawPath);
        long length = 0;
        long ticks = 0;
        var info = new FileInfo(full);
        if (info.Exists)
        {
            length = info.Length;
            ticks = info.LastWriteTimeUtc.Ticks;
        }
        else if (Directory.Exists(full))
        {
            // Agilent and Bruker acquisitions are directories
            var dir = new DirectoryInfo(full);
            ticks = dir.LastWriteTimeUtc.Ticks;
            foreach (var f in dir.EnumerateFiles("*", SearchOption.AllDirectories)) length += f.Length;
        }
        var payload = $"{full}|{length}|{ticks}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))[..32].ToLowerInvariant();
    }

    public string PathFor(string rawPath) => Path.Combine(Root, KeyFor(rawPath) + Extension);

    public RawSnapshot? TryLoad(string rawPath)
    {
        if (!Enabled) return null;
        var path = PathFor(rawPath);
        if (!File.Exists(path)) return null;
        try
        {
            using var stream = File.OpenRead(path);
            var snapshot = RawSnapshot.Read(stream);
            if (snapshot is not null) Touch(path);
            return snapshot;
        }
        catch (Exception)
        {
            // a truncated or unreadable entry is simply a miss
            try { File.Delete(path); } catch { }
            return null;
        }
    }

    public void Save(string rawPath, RawSnapshot snapshot)
    {
        if (!Enabled) return;
        try
        {
            Directory.CreateDirectory(Root);
            var path = PathFor(rawPath);
            // write beside and move, so a crash never leaves a half-written entry to be read later
            var temp = path + ".writing";
            using (var stream = File.Create(temp)) snapshot.Write(stream);
            File.Move(temp, path, overwrite: true);
            Evict();
        }
        catch (Exception)
        {
            // the cache is an optimisation; failing to write one must not fail the read
        }
    }

    private static void Touch(string path)
    {
        try { File.SetLastAccessTimeUtc(path, DateTime.UtcNow); } catch { }
    }

    /// <summary>Drops the least recently used entries until the store is inside its capacity.</summary>
    public void Evict()
    {
        try
        {
            if (!Directory.Exists(Root)) return;
            var files = new DirectoryInfo(Root).GetFiles("*" + Extension);
            var total = files.Sum(f => f.Length);
            if (total <= CapacityBytes) return;
            foreach (var file in files.OrderBy(f => f.LastAccessTimeUtc))
            {
                if (total <= CapacityBytes) break;
                total -= file.Length;
                try { file.Delete(); } catch { }
            }
        }
        catch (Exception)
        {
        }
    }

    public long SizeBytes()
    {
        try
        {
            return Directory.Exists(Root) ? new DirectoryInfo(Root).GetFiles("*" + Extension).Sum(f => f.Length) : 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public int Count()
    {
        try
        {
            return Directory.Exists(Root) ? Directory.GetFiles(Root, "*" + Extension).Length : 0;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public void Clear()
    {
        try
        {
            if (!Directory.Exists(Root)) return;
            foreach (var file in Directory.GetFiles(Root, "*" + Extension)) File.Delete(file);
        }
        catch (Exception)
        {
        }
    }
}
