using System.Collections.Concurrent;
using CompMs.Common.DataObj;
using CompMs.RawDataHandler.Core;
using OpenDIAL.Pipeline.Results;

namespace OpenDIAL.Desktop.Services;

/// <summary>
/// Loads raw files once and shares them between the Explorer and Analytics workspaces. Loading happens on a
/// worker thread; concurrent requests for the same file await the same task. Channel discovery is cached too.
/// </summary>
public sealed class RawDataCache
{
    private readonly ConcurrentDictionary<string, Task<RawMeasurement>> _raw = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IReadOnlyList<RawChannel>> _channels = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(2);

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

    public IReadOnlyList<RawChannel> Channels(string path, RawMeasurement raw)
        => _channels.GetOrAdd(path, _ => RawExplorer.DiscoverChannels(raw));

    public void Clear()
    {
        _raw.Clear();
        _channels.Clear();
    }
}
