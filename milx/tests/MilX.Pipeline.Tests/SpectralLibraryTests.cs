using System.Text;
using MilX.Pipeline.Results;
using Xunit;
using Xunit.Abstractions;

namespace MilX.Pipeline.Tests;

/// <summary>
/// Reading a library for what it holds, and putting its retention times on the gradient actually
/// run. The big real library is not in the repository, so the tests that need one write their own;
/// MILX_TEST_MSP points at a real one when there is time to read it.
/// </summary>
public class SpectralLibraryTests
{
    private readonly ITestOutputHelper _out;
    public SpectralLibraryTests(ITestOutputHelper output) => _out = output;

    private static string WriteLibrary(params (string Name, double Mz, double Rt, string Adduct)[] records)
    {
        var path = Path.Combine(Path.GetTempPath(), "milx-msp-" + Guid.NewGuid().ToString("N") + ".msp");
        var text = new StringBuilder();
        foreach (var (name, mz, rt, adduct) in records)
        {
            text.AppendLine($"NAME: {name}");
            text.AppendLine($"PRECURSORMZ: {mz.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            text.AppendLine($"PRECURSORTYPE: {adduct}");
            text.AppendLine($"RETENTIONTIME: {rt.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            text.AppendLine("Num Peaks: 2");
            text.AppendLine("100.1\t500");
            text.AppendLine("200.2\t999");
            text.AppendLine();
        }
        File.WriteAllText(path, text.ToString());
        return path;
    }

    [Fact]
    public void A_library_says_what_it_holds()
    {
        var path = WriteLibrary(
            ("PC 34:1", 760.5851, 14.2, "[M+H]+"),
            ("PC 36:2", 786.6007, 15.1, "[M+H]+"),
            ("TG 52:2", 876.8017, 17.8, "[M+NH4]+"));
        try
        {
            var summary = SpectralLibrary.Scan(path);
            Assert.Equal(3, summary.Records);
            Assert.Equal(3, summary.WithRetentionTime);
            Assert.True(summary.CarriesRetentionTimes);
            Assert.Equal(14.2, summary.RetentionTimeLow, 3);
            Assert.Equal(17.8, summary.RetentionTimeHigh, 3);
            Assert.Equal(760.5851, summary.MassLow, 3);
            Assert.Equal(2, summary.MedianPeakCount);
            Assert.Equal("[M+H]+", summary.Adducts[0].Adduct);
            Assert.Contains("3 records", summary.Sentence(), StringComparison.Ordinal);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Times_from_another_gradient_are_seen_for_what_they_are()
    {
        var path = WriteLibrary(("A", 100, 14.0, "[M+H]+"), ("B", 200, 16.0, "[M+H]+"), ("C", 300, 18.0, "[M+H]+"));
        try
        {
            var summary = SpectralLibrary.Scan(path);
            Assert.False(summary.RetentionTimesCouldFit(0, 8));      // the liver batch
            Assert.True(summary.RetentionTimesCouldFit(0, 25));      // a long gradient
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void The_calibration_puts_the_library_on_this_gradient()
    {
        // a library on a long gradient; the run saw everything at half the time, less a minute
        var records = Enumerable.Range(1, 30).Select(i => ($"C{i}", 100.0 + i, 4.0 + i * 0.4, "[M+H]+")).ToArray();
        var path = WriteLibrary(records);
        try
        {
            var observed = records.ToDictionary(r => r.Item1, r => r.Item3 * 0.5 - 1);
            var fit = SpectralLibrary.Recalibrate(path, observed);
            Assert.NotNull(fit);
            Assert.Equal(30, fit!.Pairs);
            Assert.Equal(0.5, fit.Slope, 3);
            Assert.Equal(-1, fit.Intercept, 3);
            Assert.True(fit.RSquared > 0.999, fit.RSquared.ToString());

            var calibrated = SpectralLibrary.Scan(fit.WrittenTo);
            Assert.Equal(records.Length, calibrated.Records);
            Assert.True(calibrated.RetentionTimesCouldFit(0, 8), $"{calibrated.RetentionTimeLow}–{calibrated.RetentionTimeHigh}");
            File.Delete(fit.WrittenTo);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Too_few_shared_names_is_no_calibration_at_all()
    {
        var path = WriteLibrary(("A", 100, 14.0, "[M+H]+"), ("B", 200, 16.0, "[M+H]+"));
        try
        {
            Assert.Null(SpectralLibrary.Recalibrate(path, new Dictionary<string, double> { ["A"] = 2.0 }));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void A_real_library_reads_when_one_is_pointed_at()
    {
        var path = Environment.GetEnvironmentVariable("MILX_TEST_MSP");
        if (path is null || !File.Exists(path)) { _out.WriteLine("skipped: set MILX_TEST_MSP"); return; }
        var started = DateTime.Now;
        var summary = SpectralLibrary.Scan(path);
        _out.WriteLine($"{summary.Sentence()}  ({(DateTime.Now - started).TotalSeconds:0.0} s)");
        Assert.True(summary.Records > 0);
    }
}
