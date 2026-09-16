using System.Diagnostics;
using CompMs.Common.DataObj;
using CompMs.RawDataHandler.Core;
using MilX.Pipeline.Caching;
using MilX.Pipeline.Results;
using Xunit;

namespace MilX.Pipeline.Tests;

public class RawSnapshotCacheTests
{
    private static RawMeasurement Synthetic(int scans, int peaksPerScan)
    {
        var list = new List<RawSpectrum>(scans);
        for (var i = 0; i < scans; i++)
        {
            var peaks = new RawPeakElement[peaksPerScan];
            for (var j = 0; j < peaksPerScan; j++)
            {
                peaks[j].Mz = 100.0 + j * 0.5;
                // one mass rises and falls across the run, the rest are flat
                peaks[j].Intensity = j == peaksPerScan / 2 ? 1000 * Math.Exp(-Math.Pow(i - scans / 2.0, 2) / 50.0) : 10;
            }
            list.Add(new RawSpectrum
            {
                Index = i,
                MsLevel = 1,
                ScanStartTime = i * 0.01,
                ScanStartTimeUnit = Units.Minute,
                Spectrum = peaks,
            });
        }
        return new RawMeasurement { SpectrumList = list };
    }

    [Fact]
    public void A_snapshot_extracts_the_same_chromatogram_as_the_raw_measurement()
    {
        var raw = Synthetic(200, 40);
        var indices = Enumerable.Range(0, 200).ToList();
        var target = 100.0 + 20 * 0.5;

        var fromRaw = RawExplorer.Xic(raw, indices, target, 0.01);
        var fromSnapshot = RawSnapshot.FromMeasurement(raw).Xic(target, 0.01);

        Assert.Equal(fromRaw.Points.Count, fromSnapshot.Points.Count);
        for (var i = 0; i < fromRaw.Points.Count; i++)
        {
            Assert.Equal(fromRaw.Points[i].Rt, fromSnapshot.Points[i].Rt, 6);
            Assert.Equal(fromRaw.Points[i].Intensity, fromSnapshot.Points[i].Intensity, 3);
        }
    }

    [Fact]
    public void A_snapshot_survives_a_round_trip_through_the_file_format()
    {
        var raw = Synthetic(120, 25);
        var indices = Enumerable.Range(0, 120).ToList();
        var original = RawSnapshot.FromMeasurement(raw);

        using var buffer = new MemoryStream();
        original.Write(buffer);
        buffer.Position = 0;
        var restored = RawSnapshot.Read(buffer);

        Assert.NotNull(restored);
        Assert.Equal(original.ScanCount, restored!.ScanCount);
        Assert.Equal(original.PeakCount, restored.PeakCount);
        var a = original.Xic(110.0, 0.02);
        var b = restored.Xic(110.0, 0.02);
        for (var i = 0; i < a.Points.Count; i++) Assert.Equal(a.Points[i].Intensity, b.Points[i].Intensity, 3);
    }

    /// <summary>The measurement rebuilt from the cache has to be the one the interface would have read.</summary>
    [Fact]
    public void A_rebuilt_measurement_keeps_every_spectrum_and_its_identity()
    {
        var raw = Synthetic(30, 12);
        // give half of them a precursor, as a data-dependent acquisition would
        for (var i = 1; i < raw.SpectrumList.Count; i += 2)
        {
            var s = raw.SpectrumList[i];
            s.MsLevel = 2;
            s.ExperimentID = 3;
            s.CollisionEnergy = 35;
            s.Precursor = new CompMs.Common.DataObj.RawPrecursorIon
            {
                SelectedIonMz = 500.25,
                IsolationTargetMz = 500.25,
                IsolationWindowLowerOffset = 0.5,
                IsolationWindowUpperOffset = 0.5,
                CollisionEnergy = 35,
            };
        }

        var rebuilt = RawSnapshot.FromMeasurement(raw).ToMeasurement();

        Assert.Equal(raw.SpectrumList.Count, rebuilt.SpectrumList.Count);
        for (var i = 0; i < raw.SpectrumList.Count; i++)
        {
            var a = raw.SpectrumList[i];
            var b = rebuilt.SpectrumList[i];
            Assert.Equal(a.Index, b.Index);
            Assert.Equal(a.MsLevel, b.MsLevel);
            Assert.Equal(a.ExperimentID, b.ExperimentID);
            Assert.Equal(a.ScanStartTime, b.ScanStartTime, 6);
            Assert.Equal(a.Spectrum.Length, b.Spectrum.Length);
            for (var j = 0; j < a.Spectrum.Length; j++)
            {
                Assert.Equal(a.Spectrum[j].Mz, b.Spectrum[j].Mz, 4);
                Assert.Equal(a.Spectrum[j].Intensity, b.Spectrum[j].Intensity, 3);
            }
            if (a.Precursor is null)
            {
                Assert.Null(b.Precursor);
            }
            else
            {
                Assert.NotNull(b.Precursor);
                Assert.Equal(a.Precursor.SelectedIonMz, b.Precursor!.SelectedIonMz, 3);
                Assert.Equal(a.Precursor.IsolationWindowLowerOffset, b.Precursor.IsolationWindowLowerOffset, 3);
                Assert.Equal(a.CollisionEnergy, b.CollisionEnergy, 3);
            }
        }
    }

    [Fact]
    public void The_cache_keys_on_the_file_identity_and_evicts_by_capacity()
    {
        var root = Path.Combine(Path.GetTempPath(), "milx-cache-" + Guid.NewGuid().ToString("N"));
        var work = Path.Combine(Path.GetTempPath(), "milx-raw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var rawPath = Path.Combine(work, "sample.mzML");
            File.WriteAllText(rawPath, "first");
            var cache = new RawSnapshotCache(root, capacityBytes: 1024 * 1024);
            var snapshot = RawSnapshot.FromMeasurement(Synthetic(40, 10));

            Assert.Null(cache.TryLoad(rawPath));
            cache.Save(rawPath, snapshot);
            Assert.NotNull(cache.TryLoad(rawPath));
            Assert.Equal(1, cache.Count());

            // touching the file changes its identity, so the entry no longer answers for it
            File.WriteAllText(rawPath, "second content, a different length");
            Assert.Null(cache.TryLoad(rawPath));

            // and a capacity of nothing empties the store on the next write
            var tiny = new RawSnapshotCache(root, capacityBytes: 1);
            tiny.Save(rawPath, snapshot);
            Assert.Equal(0, tiny.Count());
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
            try { Directory.Delete(work, true); } catch { }
        }
    }

    /// <summary>
    /// The point of the cache, measured on a real acquisition: the first read goes through the
    /// vendor library, the second comes off disk. Runs only when MILX_TEST_WIFF is set.
    /// </summary>
    [Fact]
    public void Reading_a_real_acquisition_is_far_faster_from_the_cache()
    {
        var wiff = Environment.GetEnvironmentVariable("MILX_TEST_WIFF");
        if (string.IsNullOrWhiteSpace(wiff) || !File.Exists(wiff))
        {
            Console.WriteLine("skipped: set MILX_TEST_WIFF to a .wiff");
            return;
        }
        var root = Path.Combine(Path.GetTempPath(), "milx-cache-" + Guid.NewGuid().ToString("N"));
        try
        {
            var cache = new RawSnapshotCache(root);
            var vendor = Stopwatch.StartNew();
            RawMeasurement raw;
            using (var access = new RawDataAccess(wiff, 0, false, false, true))
            {
                raw = access.GetMeasurement()!;
            }
            var snapshot = RawSnapshot.FromMeasurement(raw);
            vendor.Stop();
            cache.Save(wiff, snapshot);

            var fromDisk = Stopwatch.StartNew();
            var restored = cache.TryLoad(wiff);
            fromDisk.Stop();

            Assert.NotNull(restored);
            Assert.Equal(snapshot.ScanCount, restored!.ScanCount);
            Assert.Equal(snapshot.PeakCount, restored.PeakCount);

            var target = 614.5758;
            var a = snapshot.Xic(target, 0.01);
            var b = restored.Xic(target, 0.01);
            for (var i = 0; i < a.Points.Count; i++) Assert.Equal(a.Points[i].Intensity, b.Points[i].Intensity, 3);

            Console.WriteLine($"[cache] vendor read {vendor.ElapsedMilliseconds} ms, from disk {fromDisk.ElapsedMilliseconds} ms, " +
                              $"{snapshot.Ms1Count} survey + {snapshot.MsnCount} product scans, {snapshot.PeakCount} centroids, " +
                              $"{new FileInfo(cache.PathFor(wiff)).Length / 1024 / 1024} MB");
            Assert.True(fromDisk.ElapsedMilliseconds * 4 < vendor.ElapsedMilliseconds,
                $"the cache should be far faster: vendor {vendor.ElapsedMilliseconds} ms, disk {fromDisk.ElapsedMilliseconds} ms");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }
}
