using System.Globalization;
using System.Text.Json.Nodes;
using MilX.Cli.Commands;
using MilX.Pipeline.Results;
using Xunit;

namespace MilX.Cli.Tests;

/// <summary>The contract's subcommands on the synthetic study, run in-process the way the server runs them.</summary>
public class CommandTests
{
    private static async Task<JsonObject> Run(string command, params string[] argv)
    {
        var store = new RawStore(TestData.Scratch("cache"));
        var doc = await Cli.RunAsync(command, Args.Parse(argv), store, null, true, CancellationToken.None);
        return (JsonObject)JsonNode.Parse(doc.ToJson())!;
    }

    [Fact]
    public void The_argument_parser_takes_flags_values_repeats_and_positionals()
    {
        var a = Args.Parse(new[] { "FILE", "--mz", "195.0877, 138.0662", "--tol=0.02", "--trace", "--subtract", "0:0.5", "--subtract", "4:5", "-i", "in", "-p" });
        Assert.Equal(new[] { "FILE" }, a.Positional);
        Assert.Equal("195.0877, 138.0662", a.Get("mz"));
        Assert.Equal(0.02, a.Double("tol", 0));
        Assert.True(a.Flag("trace"));
        Assert.Equal(new[] { "0:0.5", "4:5" }, a.All("subtract"));
        Assert.Equal("in", a.Get("i"));
        Assert.True(a.Flag("p"));
        Assert.Equal((1.0, 2.5), a.Range("rt") ?? Args.Parse(new[] { "--rt", "1:2.5" }).Range("rt"));
        Assert.Throws<CliError>(() => Args.Parse(new[] { "--tol", "abc" }).Double("tol", 0));
        // a negative number is a value, not a flag
        Assert.Equal("-3.5", Args.Parse(new[] { "--shift", "-3.5" }).Get("shift"));
    }

    [Fact]
    public async Task Info_lists_the_channels_and_every_document_starts_with_the_envelope()
    {
        var doc = await Run("info", TestData.SampleA01);
        Assert.Equal("mil-cli/1", doc["schema"]!.GetValue<string>());
        Assert.Equal("milx", doc["tool"]!.GetValue<string>());
        Assert.Equal(Cli.Version, doc["version"]!.GetValue<string>());
        Assert.Equal("info", doc["command"]!.GetValue<string>());
        Assert.Equal(TestData.SampleA01, doc["input"]!["file"]!.GetValue<string>());
        Assert.Equal(0, doc["input"]!["sample"]!.GetValue<int>());
        var channels = doc["channels"]!.AsArray();
        Assert.Equal(2, channels.Count);
        Assert.Equal("ms1", channels[0]!["kind"]!.GetValue<string>());
        Assert.Equal(501, channels[0]!["scans"]!.GetValue<int>());
        Assert.Equal("dda-events", channels[1]!["kind"]!.GetValue<string>());
        Assert.Equal(62, channels[1]!["scans"]!.GetValue<int>());
        Assert.Equal("dda", doc["acquisition"]!.GetValue<string>());
    }

    [Fact]
    public async Task Scans_filters_by_parent_and_level_and_counts()
    {
        var gated = await Run("scans", TestData.SampleA01, "--source", "gated:195.0877:0.02");
        Assert.Equal(5, gated["count"]!.GetValue<int>());
        Assert.All(gated["scans"]!.AsArray(), s => Assert.Equal(195.0877, s!["parent_mz"]!.GetValue<double>(), 3));
        Assert.All(gated["scans"]!.AsArray(), s => Assert.Equal(2, s!["ms_level"]!.GetValue<int>()));
        var all = await Run("scans", TestData.SampleA01, "--source", "ms2-all", "--parent", "195.0877", "--count");
        Assert.Equal(5, all["count"]!.GetValue<int>());
        var ms1 = await Run("scans", TestData.SampleA01, "--rt", "1:1.1", "--ms-level", "1");
        Assert.Equal(11, ms1["count"]!.GetValue<int>());
        Assert.Null(ms1["scans"]![0]!["parent_mz"]);
        var rt = ms1["scans"]![0]!["rt"]!.GetValue<double>();
        Assert.True(rt >= 1.0 && rt <= 1.1);
    }

    [Fact]
    public async Task Xic_summary_names_the_apex_and_the_gated_source_follows_a_precursor()
    {
        var doc = await Run("xic", TestData.SampleA01, "--mz", "195.0877", "--tol", "0.01");
        var x = doc["xics"]![0]!;
        Assert.True(x["found"]!.GetValue<bool>());
        Assert.Equal(1.2, x["apex_rt"]!.GetValue<double>(), 2);
        Assert.True(x["apex_height"]!.GetValue<double>() > 500000);
        Assert.True(x["points_across_peak"]!.GetValue<int>() > 10);
        Assert.True(x["fwhm"]!.GetValue<double>() is > 0.05 and < 0.2);
        Assert.NotNull(doc["definitions"]!["snr"]);

        var frag = await Run("xic", TestData.SampleA01, "--mz", "138.0662", "--source", "gated:195.0877", "--reference", "195.0877@ms1");
        Assert.Equal("gated:195.0877:0.05", frag["source"]!.GetValue<string>());
        Assert.Equal(5, frag["scans_in_source"]!.GetValue<int>());
        var vs = frag["xics"]![0]!["vs_reference"]!;
        Assert.True(vs["pearson_r"]!.GetValue<double>() > 0.8, vs.ToJsonString());
        Assert.True(Math.Abs(vs["apex_delta_rt"]!.GetValue<double>()) < 0.05);
        Assert.Equal(195.0877, frag["reference"]!["mz"]!.GetValue<double>());

        var trace = await Run("xic", TestData.SampleA01, "--mz", "195.0877", "--rt", "1:1.5", "--trace");
        var points = trace["xics"]![0]!["trace"]!.AsArray();
        Assert.Equal(trace["scans_in_window"]!.GetValue<int>(), points.Count);
        Assert.Equal(2, points[0]!.AsArray().Count);
    }

    [Fact]
    public async Task Spectrum_averages_subtracts_and_writes_msp()
    {
        var scratch = TestData.Scratch("msp");
        try
        {
            var msp = Path.Combine(scratch, "caffeine.msp");
            var doc = await Run("spectrum", TestData.SampleA01, "--source", "gated:195.0877", "--rt", "1.1:1.3", "--subtract", "4:5", "--top", "5", "--msp", msp);
            Assert.Equal(5, doc["peak_count"]!.GetValue<int>());
            Assert.Equal(195.0877, doc["parent_mz"]!.GetValue<double>(), 3);
            var peaks = doc["peaks"]!.AsArray();
            Assert.Contains(peaks, p => Math.Abs(p!["mz"]!.GetValue<double>() - 138.0662) < 0.01);
            Assert.Equal(100.0, peaks.Max(p => p!["relative"]!.GetValue<double>()));
            Assert.True(File.Exists(msp));
            var text = File.ReadAllText(msp);
            Assert.Contains("PRECURSORMZ: 195.0877", text);
            Assert.Contains("Num Peaks: 5", text);
            var one = await Run("spectrum", TestData.SampleA01, "--scan", "0");
            Assert.Equal(1, one["scans_used"]!.AsArray().Count);
            Assert.Null(one["parent_mz"]);
        }
        finally { Directory.Delete(scratch, true); }
    }

    [Fact]
    public async Task Isotopes_measure_the_envelope_against_the_formula()
    {
        var doc = await Run("isotopes", TestData.SampleA01, "--mz", "195.0877", "--rt", "1.1:1.3", "--formula", "C8H10N4O2");
        var env = doc["envelope"]!.AsArray();
        Assert.Equal("M", env[0]!["isotope"]!.GetValue<string>());
        Assert.True(env[1]!["found"]!.GetValue<bool>());
        // the generator writes M+1 at 1.07 % per carbon: eight carbons, 8.6 %, against theory's 9.2 % (nitrogen too)
        Assert.InRange(env[1]!["measured_relative"]!.GetValue<double>(), 0.07, 0.11);
        Assert.InRange(env[1]!["expected_relative"]!.GetValue<double>(), 0.09, 0.11);
        Assert.True(Math.Abs(env[1]!["spacing_error_mda"]!.GetValue<double>()) < 5);
        Assert.True(doc["spacing_check"]!["ok"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Batch_xic_reports_a_row_per_file_and_the_skips()
    {
        var scratch = TestData.Scratch("batch");
        try
        {
            File.Copy(TestData.SampleA01, Path.Combine(scratch, "A.mzML"));
            File.Copy(Path.Combine(TestData.Synthetic, "SampleB02.mzML"), Path.Combine(scratch, "B.mzML"));
            File.WriteAllText(Path.Combine(scratch, "lonely.wiff"), "not a wiff");   // no .wiff.scan beside it
            var doc = await Run("batch-xic", scratch, "--mz", "195.0877,999.9", "--rt-window", "1.2±0.3");
            var rows = doc["rows"]!.AsArray();
            Assert.Equal(4, rows.Count);   // 2 readable files × 2 masses; the .wiff without its companion is named and skipped, not guessed at
            Assert.Equal(2, rows.Count(r => r!["found"]!.GetValue<bool>()));
            var skipped = doc["skipped"]!.AsArray();
            Assert.Contains(skipped, s => s!["kind"]!.GetValue<string>() == "missing-scan" && s["finding"]!.GetValue<string>().Contains("lonely.wiff has no lonely.wiff.scan beside it"));
            Assert.Contains(skipped, s => s!["kind"]!.GetValue<string>() == "unreadable" && s["path"]!.GetValue<string>().EndsWith("lonely.wiff"));
            Assert.Equal(2, doc["files_read"]!.GetValue<int>());
        }
        finally { Directory.Delete(scratch, true); }
    }

    [Fact]
    public async Task Convert_round_trips_the_precursor_of_every_scan()
    {
        var scratch = TestData.Scratch("convert");
        try
        {
            var outPath = Path.Combine(scratch, "round.mzML");
            var doc = await Run("convert", TestData.SampleA01, "--out", outPath);
            Assert.Equal(outPath, doc["out"]!.GetValue<string>());
            Assert.Equal(62, doc["ms2_with_precursor"]!.GetValue<int>());
            var before = await Run("scans", TestData.SampleA01, "--source", "ms2-all");
            var after = await Run("scans", outPath, "--source", "ms2-all");
            var a = before["scans"]!.AsArray().Select(s => (s!["rt"]!.GetValue<double>(), s["parent_mz"]!.GetValue<double>())).ToList();
            var b = after["scans"]!.AsArray().Select(s => (s!["rt"]!.GetValue<double>(), s["parent_mz"]!.GetValue<double>())).ToList();
            Assert.Equal(a, b);
            var x1 = await Run("xic", TestData.SampleA01, "--mz", "195.0877");
            var x2 = await Run("xic", outPath, "--mz", "195.0877");
            Assert.Equal(x1["xics"]![0]!["apex_height"]!.GetValue<double>(), x2["xics"]![0]!["apex_height"]!.GetValue<double>());
            Assert.Equal(x1["xics"]![0]!["area"]!.GetValue<double>(), x2["xics"]![0]!["area"]!.GetValue<double>());
        }
        finally { Directory.Delete(scratch, true); }
    }

    [Fact]
    public async Task Errors_are_the_contracts()
    {
        var store = new RawStore(TestData.Scratch("cache"));
        var missing = await Assert.ThrowsAsync<CliError>(() => Cli.RunAsync("info", Args.Parse(new[] { "/no/such/file.mzML" }), store, null, true, CancellationToken.None));
        Assert.Equal(3, missing.ExitCode);
        Assert.Equal("unreadable", missing.Name);
        var bad = await Assert.ThrowsAsync<CliError>(() => Cli.RunAsync("xic", Args.Parse(new[] { TestData.SampleA01 }), store, null, true, CancellationToken.None));
        Assert.Equal(2, bad.ExitCode);
        var none = await Assert.ThrowsAsync<CliError>(() => Cli.RunAsync("xic", Args.Parse(new[] { TestData.SampleA01, "--mz", "999.9", "--require" }), store, null, true, CancellationToken.None));
        Assert.Equal(4, none.ExitCode);
        Assert.Equal("nothing_found", none.Name);
        var unknown = await Assert.ThrowsAsync<CliError>(() => Cli.RunAsync("peptide", Args.Parse(Array.Empty<string>()), store, null, true, CancellationToken.None));
        Assert.Equal("not_allowed", unknown.Name);
        Assert.Contains("\"error\":\"bad_arguments\"", Cli.ErrorDocument("bad_arguments", "x"));
    }

    [Fact]
    public async Task The_same_request_gives_the_same_bytes_and_tsv_has_the_rows()
    {
        var one = await Cli.RunAsync("xic", Args.Parse(new[] { TestData.SampleA01, "--mz", "195.0877 138.0662" }), new RawStore(TestData.Scratch("c1")), null, true, CancellationToken.None);
        var two = await Cli.RunAsync("xic", Args.Parse(new[] { TestData.SampleA01, "--mz", "195.0877 138.0662" }), new RawStore(TestData.Scratch("c2")), null, true, CancellationToken.None);
        Assert.Equal(one.ToJson(), two.ToJson());
        var tsv = one.ToTsv().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("mz\tfound\tapex_rt\tapex_height\tarea\tfwhm\tpoints_across_peak\tsnr", tsv[0]);
        Assert.Equal(3, tsv.Length);
        Assert.StartsWith("195.0877\ttrue\t1.2\t", tsv[1]);
        // numbers carry a period: the document never contains a decimal comma
        Assert.DoesNotContain(",\n", one.ToTsv());
        Assert.Matches(@"""apex_rt"": 1\.2,", one.ToJson());
    }

    [Fact]
    public void The_peak_summary_reads_a_gaussian_right()
    {
        var points = Enumerable.Range(0, 200).Select(i => { var rt = i * 0.01; return new ChromatogramPoint(rt, 1000 * Math.Exp(-0.5 * Math.Pow((rt - 1.0) / 0.05, 2)) + 10); }).ToList();
        var s = PeakSummary.Of(points);
        Assert.True(s.Found);
        Assert.Equal(1.0, s.ApexRt, 6);
        Assert.Equal(1010, s.ApexHeight, 6);
        Assert.Equal(2.3548 * 0.05, s.Fwhm!.Value, 2);
        // the area of a gaussian: height × sigma × √(2π), plus the baseline over the bounds
        Assert.InRange(s.Area, 1000 * 0.05 * Math.Sqrt(2 * Math.PI) * 0.95, 1000 * 0.05 * Math.Sqrt(2 * Math.PI) * 1.1);
        Assert.True(s.Points > 20);
        Assert.Equal(1.0, s.CorrelationWith(points)!.Value, 9);
        Assert.False(PeakSummary.Of(Array.Empty<ChromatogramPoint>()).Found);
        Assert.False(PeakSummary.Of(new[] { new ChromatogramPoint(0, 0), new ChromatogramPoint(1, 0) }).Found);
    }

    [Fact]
    public async Task On_an_ida_like_file_the_gate_gathers_one_precursor_from_several_slots()
    {
        var ida = TestData.IdaFile();
        if (ida is null) return;   // python3 with numpy is not at hand
        var info = await Run("info", ida);
        Assert.Equal("dda", info["acquisition"]!.GetValue<string>());
        var gated = await Run("scans", ida, "--source", "gated:195.0877:0.02");
        var count = gated["count"]!.GetValue<int>();
        Assert.True(count >= 10, $"only {count} scans isolated caffeine");
        // the same precursor appears in more than one cycle position (slot): the ranks change as it elutes
        var positions = gated["scans"]!.AsArray().Select(s => Math.Round((s!["rt"]!.GetValue<double>() * 60) % 0.6 / 0.2)).Distinct().Count();
        Assert.True(positions >= 2, "caffeine was expected in more than one slot");
        var frag = await Run("xic", ida, "--mz", "138.0662", "--source", "gated:195.0877", "--reference", "195.0877@ms1");
        Assert.True(frag["xics"]![0]!["vs_reference"]!["pearson_r"]!.GetValue<double>() > 0.9);
    }
}
