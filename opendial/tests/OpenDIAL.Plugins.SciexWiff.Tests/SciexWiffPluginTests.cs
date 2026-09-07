using CompMs.Common.DataObj;
using CompMs.RawDataHandler.Core;
using OpenDIAL.Plugins.SciexWiff;
using OpenDIAL.RawData.Plugins;
using Xunit;

namespace OpenDIAL.Plugins.SciexWiff.Tests;

/// <summary>
/// The SDK and real acquisitions cannot be committed, so the reading tests run only when
/// OPENDIAL_TEST_WIFF points at a .wiff (with its .wiff.scan beside it) and the plugin was
/// built with the SCIEX assemblies; otherwise they are skipped with a message.
/// </summary>
public class SciexWiffPluginTests
{
    private static string? TestWiff {
        get {
            var p = Environment.GetEnvironmentVariable("OPENDIAL_TEST_WIFF");
            return !string.IsNullOrWhiteSpace(p) && File.Exists(p) ? p : null;
        }
    }

    [Fact]
    public void Plugin_reports_wiff_extensions() {
        var plugin = new SciexWiffReaderPlugin();
        Assert.Equal(10, plugin.Priority);
        Assert.False(plugin.CanRead("missing.wiff"));
        Assert.False(plugin.CanRead("x.mzML"));
    }

    [Fact]
    public void Sample_index_resolution_prefers_registry_then_env() {
        var names = new[] { "A", "B", "C" };
        WiffSampleLinks.Register("/tmp/x.wiff", 2);
        Assert.Equal(2, SciexWiffReaderPlugin.ResolveSampleIndex("/tmp/x.wiff", names));
        Environment.SetEnvironmentVariable("OPENDIAL_WIFF_SAMPLE", "B");
        try {
            Assert.Equal(1, SciexWiffReaderPlugin.ResolveSampleIndex("/tmp/other.wiff", names));
        }
        finally {
            Environment.SetEnvironmentVariable("OPENDIAL_WIFF_SAMPLE", null);
        }
        Assert.Equal(0, SciexWiffReaderPlugin.ResolveSampleIndex("/tmp/other.wiff", names));
    }

    [Fact]
    public void Reads_a_real_acquisition_when_available() {
        var wiff = TestWiff;
        if (wiff == null || !SciexWiffReaderPlugin.IsSdkAvailable) {
            Console.WriteLine("skipped: set OPENDIAL_TEST_WIFF and build with the SCIEX assemblies");
            return;
        }
        RawReaderPlugins.Register(new SciexWiffReaderPlugin());
        using var access = new RawDataAccess(wiff, 0, false, false, true);
        var m = access.GetMeasurement();
        Assert.NotNull(m);
        Assert.True(m!.SpectrumList.Count > 10);
        Assert.Contains(m.SpectrumList, s => s.MsLevel == 1 && s.Spectrum.Length > 0);
        // positional indexing and monotonic retention times, as every MS-DIAL reader guarantees
        for (var i = 1; i < m.SpectrumList.Count; i++) {
            Assert.Equal(i, m.SpectrumList[i].Index);
            Assert.True(m.SpectrumList[i - 1].ScanStartTime <= m.SpectrumList[i].ScanStartTime);
        }
        var ms2 = m.SpectrumList.FirstOrDefault(s => s.MsLevel == 2);
        if (ms2 != null) {
            Assert.NotNull(ms2.Precursor);
            Assert.True(ms2.Precursor.SelectedIonMz > 0);
        }
        Assert.Equal(Units.Minute, m.SpectrumList[0].ScanStartTimeUnit);
        Assert.True(SciexWiffReaderPlugin.ListSamples(wiff).Count >= 1);
    }
}
