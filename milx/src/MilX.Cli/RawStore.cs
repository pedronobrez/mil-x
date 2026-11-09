using System.Diagnostics;
using CompMs.Common.DataObj;
using CompMs.RawDataHandler.Core;
using MilX.Pipeline.Caching;
using MilX.Pipeline.Results;
using MilX.Pipeline.Vendor;
using MilX.Plugins.SciexWiff;

namespace MilX.Cli;

/// <summary>One open raw file: the measurement, its channels, where it came from and what it cost to read.</summary>
public sealed class OpenFile
{
    public OpenFile(string path, int sample, string sampleName, RawMeasurement raw, IReadOnlyList<RawChannel> channels, TimeSpan readTime, string readFrom)
    {
        Path = path;
        Sample = sample;
        SampleName = sampleName;
        Raw = raw;
        Channels = channels;
        ReadTime = readTime;
        ReadFrom = readFrom;
    }

    public string Path { get; }
    public int Sample { get; }
    public string SampleName { get; }
    public RawMeasurement Raw { get; }
    public IReadOnlyList<RawChannel> Channels { get; }
    public TimeSpan ReadTime { get; }
    /// <summary>"reader" when the vendor library or the mzML parser read it, "cache" when the sidecar index served it.</summary>
    public string ReadFrom { get; }
    public long Spectra => Raw.SpectrumList.Count;
}

/// <summary>
/// Opens raw files for the commands and keeps them open across requests: in <c>serve</c> mode the
/// same file is asked about many times, and the vendor read is the expensive part. The store is bounded
/// by spectra held, least recently used out first. A per-file sidecar (the display cache the desktop
/// application keeps, <c>--cache DIR</c>) serves <c>info</c> and <c>scans</c> without the vendor library;
/// intensities for <c>xic</c> and <c>spectrum</c> always come from the reader, so a document is the same
/// bytes cold and warm.
/// </summary>
public sealed class RawStore
{
    private readonly object _sync = new();
    private readonly Dictionary<string, OpenFile> _open = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _recent = new();
    private readonly long _capacitySpectra;
    private readonly RawSnapshotCache _sidecar;

    public RawStore(string? cacheDir = null, long capacitySpectra = 400_000)
    {
        _capacitySpectra = capacitySpectra;
        _sidecar = new RawSnapshotCache(cacheDir is null ? null : Path.Combine(cacheDir, "spectra"));
        WiffSupport.EnsureRegistered();
        // nothing but documents on stdout: the reader's log and its "i / N" progress go nowhere unless a caller wires Log
        RawDataAccessOptions.Log = line => Log?.Invoke(line);
        RawDataAccessOptions.Mzml.ConsoleProgress = false;
    }

    public string CacheRoot => _sidecar.Root;
    public Action<string>? Log { get; set; }

    private static string Key(string path, int sample) => Path.GetFullPath(path) + "#" + sample;

    /// <summary>The sample names of a multi-sample .wiff, or the file's own name for anything else.</summary>
    public static IReadOnlyList<string> SampleNames(string path)
    {
        if (WiffSupport.IsWiff(path) && WiffSupport.IsNativeAvailable)
        {
            try { return SciexWiffReaderPlugin.ListSamples(path); } catch { }
        }
        return new[] { Path.GetFileNameWithoutExtension(path) };
    }

    /// <summary>A sample spelt as an index or a name, resolved against the file; 0 when absent.</summary>
    public static int ResolveSample(string path, string? sample)
    {
        if (string.IsNullOrWhiteSpace(sample)) return 0;
        var names = SampleNames(path);
        if (int.TryParse(sample, out var index))
        {
            if (index < 0 || index >= names.Count) throw new CliError(ErrorCode.BadArguments, $"sample {index} does not exist; {Path.GetFileName(path)} has {names.Count}");
            return index;
        }
        for (var i = 0; i < names.Count; i++)
        {
            if (string.Equals(names[i], sample, StringComparison.OrdinalIgnoreCase)) return i;
        }
        throw new CliError(ErrorCode.BadArguments, $"no sample named '{sample}' in {Path.GetFileName(path)}; the samples are {string.Join(", ", names)}");
    }

    /// <summary>Opens (or returns the open) file. <paramref name="headersOnly"/> lets the sidecar answer when it can.</summary>
    public OpenFile Open(string path, int sample = 0, bool headersOnly = false, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full) && !Directory.Exists(full)) throw new CliError(ErrorCode.Unreadable, $"{full} is not there");
        if (WiffSupport.IsWiff(full) && FileFormats.GetExtension(full) == ".wiff" && !File.Exists(FolderCheck.ScanFileOf(full)))
        {
            throw new CliError(ErrorCode.Unreadable, $"{Path.GetFileName(full)} has no {Path.GetFileName(FolderCheck.ScanFileOf(full))} beside it; the spectra cannot be read without it");
        }
        var key = Key(full, sample);
        lock (_sync)
        {
            if (_open.TryGetValue(key, out var hit) && (!headersOnly || true))
            {
                _recent.Remove(key);
                _recent.AddFirst(key);
                return hit;
            }
        }
        var watch = Stopwatch.StartNew();
        RawMeasurement? raw = null;
        var from = "reader";
        if (headersOnly)
        {
            var snapshot = _sidecar.TryLoad(full);
            if (snapshot is not null)
            {
                raw = snapshot.ToMeasurement();
                from = "cache";
            }
        }
        if (raw is null)
        {
            raw = Read(full, sample, ct);
            // the sidecar is the desktop application's display cache; writing it is what makes the next info/scans fast
            var measurement = raw;
            try { _sidecar.Save(full, RawSnapshot.FromMeasurement(measurement)); } catch { }
        }
        var channels = RawExplorer.DiscoverChannels(raw);
        var names = SampleNames(full);
        var file = new OpenFile(full, sample, sample < names.Count ? names[sample] : Path.GetFileNameWithoutExtension(full), raw, channels, watch.Elapsed, from);
        if (from == "cache") return file;   // a header-only answer is not kept: the next xic wants the reader's intensities
        lock (_sync)
        {
            _open[key] = file;
            _recent.AddFirst(key);
            Evict();
        }
        Log?.Invoke($"[store] {Path.GetFileName(full)} sample {sample}: {raw.SpectrumList.Count} spectra in {watch.Elapsed.TotalSeconds:F2} s");
        return file;
    }

    private RawMeasurement Read(string full, int sample, CancellationToken ct)
    {
        if (WiffSupport.IsWiff(full)) WiffSupport.Register(full, sample);
        using var access = new RawDataAccess(full, 0, false, false, false);
        RawMeasurement? m;
        try { m = access.GetMeasurement(); }
        catch (Exception ex) { throw new CliError(ErrorCode.Unreadable, $"{Path.GetFileName(full)} could not be read: {ex.Message}"); }
        ct.ThrowIfCancellationRequested();
        if (m?.SpectrumList is null || m.SpectrumList.Count == 0) throw new CliError(ErrorCode.Unreadable, $"no spectra could be read from {Path.GetFileName(full)}");
        return m;
    }

    private void Evict()
    {
        long held = _open.Values.Sum(f => f.Spectra);
        while (held > _capacitySpectra && _recent.Count > 1)
        {
            var oldest = _recent.Last!.Value;
            _recent.RemoveLast();
            if (_open.Remove(oldest, out var gone)) held -= gone.Spectra;
        }
    }

    public int OpenCount { get { lock (_sync) return _open.Count; } }

    public void Clear() { lock (_sync) { _open.Clear(); _recent.Clear(); } }
}
