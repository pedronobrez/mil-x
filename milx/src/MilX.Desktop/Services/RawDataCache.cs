using System.Collections.Concurrent;
using CompMs.Common.DataObj;
using CompMs.RawDataHandler.Core;
using MilX.Pipeline.Caching;
using MilX.Pipeline.Results;

namespace MilX.Desktop.Services;

/// <summary>
/// Loads raw files once and shares them between the Explorer and Analytics workspaces. Loading happens on a
/// worker thread; concurrent requests for the same file await the same task. Channel discovery is cached too.
/// </summary>
public sealed class RawDataCache
{
    private readonly ConcurrentDictionary<string, Task<RawMeasurement>> _raw = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IReadOnlyList<RawChannel>> _channels = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Task<RawSnapshot>> _snapshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(2);

    /// <summary>Spectra kept on disk between sessions; see <see cref="RawSnapshotCache"/>.</summary>
    public RawSnapshotCache Disk { get; } = new();

    public event EventHandler<string>? Status;

    public bool IsLoaded(string path) => _raw.TryGetValue(path, out var t) && t.IsCompletedSuccessfully;

    public Task<RawMeasurement> GetAsync(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new FileNotFoundException("No raw file path.");
        return _raw.GetOrAdd(path, p => LoadAsync(p, ct));
    }

    private async Task<RawMeasurement> LoadAsync(string path, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                throw new FileNotFoundException("Raw data file not found (was the project folder moved?)", path);
            }
            // the cache holds every spectrum, so a second visit skips the vendor library entirely
            var cached = await Task.Run(() => Disk.TryLoad(path), ct).ConfigureAwait(false);
            if (cached is not null)
            {
                var restored = await Task.Run(cached.ToMeasurement, ct).ConfigureAwait(false);
                _snapshots[path] = Task.FromResult(cached);
                Status?.Invoke(this, $"{Path.GetFileName(path)}: {restored.SpectrumList.Count} spectra from the cache");
                return restored;
            }
            Status?.Invoke(this, $"Reading {Path.GetFileName(path)}…");
            var raw = await Task.Run(() =>
            {
                ct.ThrowIfCancellationRequested();
                using var access = new RawDataAccess(path, 0, false, false, true);
                var m = access.GetMeasurement();
                if (m?.SpectrumList is null || m.SpectrumList.Count == 0)
                {
                    throw new InvalidDataException($"No spectra could be read from {Path.GetFileName(path)}");
                }
                return m;
            }, ct).ConfigureAwait(false);
            Status?.Invoke(this, $"{Path.GetFileName(path)}: {raw.SpectrumList.Count} spectra");
            _ = Task.Run(() =>
            {
                var snapshot = RawSnapshot.FromMeasurement(raw);
                Disk.Save(path, snapshot);
            }, CancellationToken.None);
            return raw;
        }
        catch
        {
            _raw.TryRemove(path, out _);
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// The spectra of a file in their compact form, for extracting chromatograms. It comes from the
    /// disk cache when it is there, which is the difference between a review pass starting straight
    /// away and one that waits for the vendor reader on every file again.
    /// </summary>
    public Task<RawSnapshot> GetMs1Async(string path, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path)) throw new FileNotFoundException("No raw file path.");
        return _snapshots.GetOrAdd(path, p => LoadSnapshotAsync(p, ct));
    }

    private async Task<RawSnapshot> LoadSnapshotAsync(string path, CancellationToken ct)
    {
        try
        {
            var cached = await Task.Run(() => Disk.TryLoad(path), ct).ConfigureAwait(false);
            if (cached is not null)
            {
                Status?.Invoke(this, $"{Path.GetFileName(path)}: spectra from the cache");
                return cached;
            }
            var raw = await GetAsync(path, ct).ConfigureAwait(false);
            var snapshot = await Task.Run(() => RawSnapshot.FromMeasurement(raw), ct).ConfigureAwait(false);
            _ = Task.Run(() => Disk.Save(path, snapshot), CancellationToken.None);
            return snapshot;
        }
        catch
        {
            _snapshots.TryRemove(path, out _);
            throw;
        }
    }

    public IReadOnlyList<RawChannel> Channels(string path, RawMeasurement raw)
        => _channels.GetOrAdd(path, _ => RawExplorer.DiscoverChannels(raw));

    public void Clear()
    {
        _raw.Clear();
        _channels.Clear();
        _snapshots.Clear();
    }
}
