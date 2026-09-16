using MilX.Pipeline.Curation;
using MilX.Pipeline.Results;
using Xunit;

namespace MilX.Pipeline.Tests;

/// <summary>
/// Two runs as one table. A compound both polarities saw has to appear once — a matrix where half
/// the rows are copies of the other half fits every model on a lie — and the row has to carry one
/// run's numbers whole, on the other run's injections.
/// </summary>
public class PolarityMergeTests
{
    private const double Protonated = 760.5851;
    private const double Deprotonated = 758.5705;
    private static readonly double[] Rising = { 100, 210, 320, 480 };
    private static readonly double[] RisingLower = { 40, 86, 128, 195 };
    private static readonly double[] Flat = { 900, 880, 910, 870 };

    private static IReadOnlyList<SampleInfo> Positive() => new[]
    {
        new SampleInfo(0, "liver_01_pos.wiff", "control", "Sample", 1),
        new SampleInfo(1, "liver_02_pos.wiff", "control", "Sample", 2),
        new SampleInfo(2, "liver_03_pos.wiff", "treated", "Sample", 3),
        new SampleInfo(3, "liver_04_pos.wiff", "treated", "Sample", 4),
    };

    // the negative run numbered its files the other way round, which is what the pairing is for
    private static IReadOnlyList<SampleInfo> Negative() => new[]
    {
        new SampleInfo(10, "liver_04_neg.wiff", "treated", "Sample", 4),
        new SampleInfo(11, "liver_03_neg.wiff", "treated", "Sample", 3),
        new SampleInfo(12, "liver_02_neg.wiff", "control", "Sample", 2),
        new SampleInfo(13, "liver_01_neg.wiff", "control", "Sample", 1),
    };

    private static AlignmentSpotRow Row(int id, string name, double mz, double rt, string adduct, double[] heights, double sn, IReadOnlyList<SampleInfo> samples)
        => new()
        {
            Id = id, Name = name, Mz = mz, Rt = rt, Adduct = adduct,
            AverageHeight = heights.Average(), SignalToNoiseAverage = sn, FillPercent = 100, MsmsAssigned = true, IsotopeWeight = 0,
            SampleHeights = samples.Select((s, i) => new SampleValue(s.FileId, s.FileName, s.Class, heights[i])).ToList(),
            SamplePeaks = samples.Select((s, i) => new AlignedSamplePeak(s.FileId, s.FileName, s.Class, s.SampleType, rt, rt - 0.1, rt + 0.1, mz, heights[i], heights[i] * 10, sn, false)).ToList(),
        };

    private static (AlignmentTable Positive, AlignmentTable Negative, PolarityLinkResult Link) Batch()
    {
        var p = Positive();
        var n = Negative();
        var positive = new AlignmentTable(p, new[]
        {
            Row(1, "PC 34:1", Protonated, 5.00, "[M+H]+", Rising, 20, p),          // paired, negative quantifies
            Row(2, "Unknown", 501.2573, 3.00, "[M+H]+", Flat, 30, p),              // positive only
        }, "test");
        // the negative run's heights are in ITS file order: liver_04, 03, 02, 01
        var negative = new AlignmentTable(n, new[]
        {
            Row(11, "PC 34:1", Deprotonated, 5.02, "[M-H]-", RisingLower.Reverse().ToArray(), 55, n),
            Row(12, "FA 18:0", 283.2637, 2.10, "[M-H]-", Flat, 40, n),             // negative only
        }, "test");
        return (positive, negative, PolarityLink.Link(positive, negative));
    }

    [Fact]
    public void A_compound_both_runs_saw_is_one_row_not_two()
    {
        var (positive, negative, link) = Batch();
        var merged = PolarityMerge.Build(positive, negative, link);

        Assert.Equal(3, merged.Spots.Count);              // one paired + one each side
        Assert.Equal(1, merged.FromBoth);
        Assert.Equal(1, merged.FromPositiveOnly);
        Assert.Equal(1, merged.FromNegativeOnly);
        Assert.Equal(4, merged.Samples.Count);
        Assert.Contains("1 measured in both polarities", merged.Sentence(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_paired_row_carries_the_numbers_of_the_run_that_measured_it_better()
    {
        var (positive, negative, link) = Batch();
        var merged = PolarityMerge.Build(positive, negative, link);
        var paired = merged.Spots.Single(s => s.Id == 1);

        Assert.Equal("[M-H]-", paired.Adduct);            // the negative run quantifies: S/N 55 against 20
        Assert.Equal(55, paired.SignalToNoiseAverage);
        // and its heights sit on the positive run's columns, in the positive run's order
        Assert.Equal(new[] { 0, 1, 2, 3 }, paired.SampleHeights.Select(h => h.FileId));
        Assert.Equal(RisingLower, paired.SampleHeights.Select(h => h.Height));
    }

    [Fact]
    public void A_negative_only_compound_comes_across_on_the_shared_columns()
    {
        var (positive, negative, link) = Batch();
        var merged = PolarityMerge.Build(positive, negative, link);
        var only = merged.Spots.Single(s => s.Id == 12 + PolarityMerge.NegativeIdOffset);

        Assert.Equal("FA 18:0", only.Name);
        Assert.Equal(new[] { 0, 1, 2, 3 }, only.SampleHeights.Select(h => h.FileId));
        Assert.Equal(new[] { "liver_01_pos.wiff", "liver_02_pos.wiff", "liver_03_pos.wiff", "liver_04_pos.wiff" },
                     only.SampleHeights.Select(h => h.FileName));
        Assert.All(only.SamplePeaks, p => Assert.False(double.IsNaN(p.Height)));
    }

    [Fact]
    public void An_injection_the_pairing_could_not_match_reads_as_not_measured()
    {
        var p = Positive();
        var n = Negative();
        var positive = new AlignmentTable(p, new[] { Row(1, "PC 34:1", Protonated, 5.00, "[M+H]+", Rising, 20, p) }, "test");
        var negative = new AlignmentTable(n, new[] { Row(11, "FA 18:0", 283.2637, 2.10, "[M-H]-", Flat, 40, n) }, "test");
        // a pairing that matched only two of the four injections
        var link = new PolarityLinkResult(
            Array.Empty<PolarityPair>(), new[] { 1 }, new[] { 11 },
            new[] { new SamplePairing(0, "liver_01_pos.wiff", 13, "liver_01_neg.wiff", "name"),
                    new SamplePairing(1, "liver_02_pos.wiff", 12, "liver_02_neg.wiff", "name") });

        var merged = PolarityMerge.Build(positive, negative, link);
        var only = merged.Spots.Single(s => s.Id == 11 + PolarityMerge.NegativeIdOffset);
        Assert.Equal(2, merged.Samples.Count);
        Assert.All(only.SampleHeights, h => Assert.False(double.IsNaN(h.Height)));

        var wider = PolarityMerge.Build(positive, negative,
            link with { Samples = link.Samples.Append(new SamplePairing(2, "liver_03_pos.wiff", 99, "missing", "name")).ToList() });
        var widened = wider.Spots.Single(s => s.Id == 11 + PolarityMerge.NegativeIdOffset);
        Assert.True(double.IsNaN(widened.SampleHeights.Last().Height), "an unmatched injection is not measured, not zero");
    }

    [Fact]
    public void The_review_of_both_runs_comes_with_the_matrix()
    {
        var (positive, negative, link) = Batch();
        var folder = Path.Combine(Path.GetTempPath(), "milx-merge-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var positiveCuration = CurationStore.Load(Path.Combine(folder, "pos.arf"));
            var negativeCuration = CurationStore.Load(Path.Combine(folder, "neg.arf"));
            positiveCuration.SetTag(1, PeakSpotTagKind.Confirmed, true);
            negativeCuration.SetTag(12, PeakSpotTagKind.Misannotation, true);

            var merged = PolarityMerge.Build(positive, negative, link, positiveCuration, negativeCuration);
            Assert.True(merged.Curation.HasTag(1, PeakSpotTagKind.Confirmed));
            Assert.True(merged.Curation.HasTag(12 + PolarityMerge.NegativeIdOffset, PeakSpotTagKind.Misannotation));

            // it belongs to no alignment, so saving it must write nothing rather than throw
            merged.Curation.Save();
            Assert.False(File.Exists(Path.Combine(folder, "_tags.xml")));
        }
        finally { Directory.Delete(folder, true); }
    }
}
