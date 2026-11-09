using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Xunit;

namespace MilX.Cli.Tests;

/// <summary>
/// The contract test: on the synthetic DDA study (and its IDA-like variant), info, scans, xic --summary
/// (ms1 and gated) and spectrum must give the same numbers from milq and from milx within the stated
/// tolerances — RT exact, m/z 1e-4, intensity relative 1e-6 for the same centroiding mode. MIL-Q's side
/// runs when MILQ_CLI names its executable (and MILX_TEST_WIFF adds a real .wiff); without it the test
/// holds milx to itself across formats and the cache, which is the half it can check alone.
/// </summary>
public class ContractCrossCheckTests
{
    private static async Task<JsonObject> Milx(string command, params string[] argv)
    {
        var doc = await Cli.RunAsync(command, Args.Parse(argv), new RawStore(TestData.Scratch("cache")), null, true, CancellationToken.None);
        return (JsonObject)JsonNode.Parse(doc.ToJson())!;
    }

    private static JsonObject? Milq(string command, params string[] argv)
    {
        var milq = Environment.GetEnvironmentVariable("MILQ_CLI");
        if (string.IsNullOrEmpty(milq)) return null;
        var psi = new ProcessStartInfo(milq) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        psi.ArgumentList.Add(command);
        foreach (var a in argv) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add("--quiet");
        using var p = Process.Start(psi)!;
        var text = p.StandardOutput.ReadToEnd();
        var err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        Assert.True(p.ExitCode == 0, $"milq {command} failed: {err}");
        return (JsonObject)JsonNode.Parse(text)!;
    }

    private static void SameNumbers(JsonObject a, JsonObject b, string context)
    {
        Assert.Equal(a["found"]?.GetValue<bool>(), b["found"]?.GetValue<bool>());
        if (a["found"]?.GetValue<bool>() != true) return;
        Assert.Equal(a["apex_rt"]!.GetValue<double>(), b["apex_rt"]!.GetValue<double>());                                           // RT exact
        Assert.Equal(a["apex_height"]!.GetValue<double>(), b["apex_height"]!.GetValue<double>(), 1e-6 * a["apex_height"]!.GetValue<double>());   // intensity relative 1e-6
        Assert.Equal(a["points_across_peak"]!.GetValue<int>(), b["points_across_peak"]!.GetValue<int>());
    }

    public static IEnumerable<object[]> Files()
    {
        yield return new object[] { TestData.SampleA01 };
        if (TestData.IdaFile() is { } ida) yield return new object[] { ida };
        if (Environment.GetEnvironmentVariable("MILX_TEST_WIFF") is { Length: > 0 } wiff && File.Exists(wiff)) yield return new object[] { wiff };
    }

    [Theory, MemberData(nameof(Files))]
    public async Task Info_scans_xic_and_spectrum_agree(string file)
    {
        var info = await Milx("info", file);
        var scans = await Milx("scans", file, "--source", "ms2-all", "--count");
        var xic = await Milx("xic", file, "--mz", "195.0877", "--tol", "0.01");
        var gated = await Milx("xic", file, "--mz", "138.0662", "--source", "gated:195.0877", "--tol", "0.01");
        var spectrum = await Milx("spectrum", file, "--source", "ms1", "--scan", "10", "--centroid", "none");

        // milx against itself: the cache does not change a number, and TSV carries the same values as JSON
        var again = await Milx("xic", file, "--mz", "195.0877", "--tol", "0.01");
        Assert.Equal(xic["xics"]!.ToJsonString(), again["xics"]!.ToJsonString());
        var tsv = (await Cli.RunAsync("xic", Args.Parse(new[] { file, "--mz", "195.0877", "--tol", "0.01" }), new RawStore(TestData.Scratch("c")), null, true, CancellationToken.None)).ToTsv().Split('\n');
        Assert.Equal(xic["xics"]![0]!["apex_rt"]!.GetValue<double>().ToString("R", CultureInfo.InvariantCulture), tsv[1].Split('\t')[2]);

        var other = Milq("info", file);
        if (other is null) return;   // MIL-Q's executable is not at hand; the self-check above is what stands
        Assert.Equal(info["channels"]!.AsArray().Count, other["channels"]!.AsArray().Count);
        var otherScans = Milq("scans", file, "--source", "ms2-all", "--count")!;
        Assert.Equal(scans["count"]!.GetValue<int>(), otherScans["count"]!.GetValue<int>());
        var otherXic = Milq("xic", file, "--mz", "195.0877", "--tol", "0.01")!;
        SameNumbers((JsonObject)xic["xics"]![0]!, (JsonObject)otherXic["xics"]![0]!, "ms1 xic");
        var otherGated = Milq("xic", file, "--mz", "138.0662", "--source", "gated:195.0877", "--tol", "0.01")!;
        SameNumbers((JsonObject)gated["xics"]![0]!, (JsonObject)otherGated["xics"]![0]!, "gated xic");
        var otherSpectrum = Milq("spectrum", file, "--source", "ms1", "--scan", "10", "--centroid", "none")!;
        var mine = spectrum["peaks"]!.AsArray();
        var theirs = otherSpectrum["peaks"]!.AsArray();
        Assert.Equal(mine.Count, theirs.Count);
        for (var i = 0; i < mine.Count; i++)
        {
            Assert.Equal(mine[i]!["mz"]!.GetValue<double>(), theirs[i]!["mz"]!.GetValue<double>(), 1e-4);
            var a = mine[i]!["intensity"]!.GetValue<double>();
            Assert.Equal(a, theirs[i]!["intensity"]!.GetValue<double>(), 1e-6 * Math.Max(1, a));
        }
    }
}
