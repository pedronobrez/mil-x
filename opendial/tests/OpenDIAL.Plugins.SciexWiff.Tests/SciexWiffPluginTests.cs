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

    /// <summary>
    /// SCIEX calls DDA "IDA": experiment 0 is the TOF MS survey and experiments 1..n are dependent
    /// product-ion slots whose precursor is picked per cycle from the most intense survey ions. The
    /// acquisition method still writes a placeholder FixedMasses into every dependent experiment —
    /// the same value in all of them — so a reader that trusts it reports one identical precursor for
    /// every channel. The precursors must instead track the spectra.
    /// </summary>
    [Fact]
    public void Ida_dependent_experiments_carry_a_precursor_per_scan() {
        var wiff = TestWiff;
        if (wiff == null || !SciexWiffReaderPlugin.IsSdkAvailable) {
            Console.WriteLine("skipped: set OPENDIAL_TEST_WIFF and build with the SCIEX assemblies");
            return;
        }
        RawReaderPlugins.Register(new SciexWiffReaderPlugin());
        using var access = new RawDataAccess(wiff, 0, false, false, true);
        var m = access.GetMeasurement()!;
        var ms2 = m.SpectrumList.Where(s => s.MsLevel > 1 && s.Precursor is not null).ToList();
        if (ms2.Count < 50) {
            Console.WriteLine("skipped: not an IDA acquisition");
            return;
        }
        var experiments = ms2.Select(s => s.ExperimentID).Distinct().Count();
        var precursors = ms2.Select(s => Math.Round(s.Precursor.SelectedIonMz, 3)).Distinct().Count();
        // one precursor per experiment would be the placeholder bug; IDA gives far more
        Assert.True(precursors > experiments * 4,
            $"expected many precursors across {experiments} experiments, got {precursors}");
        // and within a single dependent experiment the precursor must change from cycle to cycle
        var oneSlot = ms2.Where(s => s.ExperimentID == ms2[0].ExperimentID).ToList();
        Assert.True(oneSlot.Select(s => Math.Round(s.Precursor.SelectedIonMz, 3)).Distinct().Count() > 1,
            "an IDA dependent experiment reported a single precursor for all of its cycles");
        Assert.All(ms2, s => Assert.True(s.Precursor.SelectedIonMz > 0));
    }
}
