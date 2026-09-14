using OpenDIAL.Pipeline.Results;
using Xunit;

namespace OpenDIAL.Pipeline.Tests;

/// <summary>
/// The same batch run twice, once in each polarity. What joins the two runs is the neutral molecule:
/// [M+H]+ here and [M-H]- there, 2.0146 Da apart, at the same time, rising and falling together
/// across the injections because it is the same liver in both files.
/// </summary>
public class PolarityLinkTests
{
    // a neutral of 759.5778: protonated 760.5851, deprotonated 758.5705, sodiated 782.5670
    private const double Neutral = 759.5778;
    private const double Protonated = 760.5851;
    private const double Deprotonated = 758.5705;

    private static readonly double[] Rising = { 100, 210, 320, 480, 600 };
    private static readonly double[] RisingLower = { 40, 88, 130, 195, 250 };
    private static readonly double[] Falling = { 600, 480, 320, 210, 100 };

    private static PolarityCandidate Ion(int id, double rt, double mz, double height, double[] profile,
        string adduct = "", string name = "", double signalToNoise = 10) =>
        new(id, rt, mz, height, adduct, name, signalToNoise, profile);

    [Fact]
    public void The_protonated_and_the_deprotonated_molecule_are_one_compound()
    {
        var result = PolarityLink.Link(
            new[] { Ion(1, 5.00, Protonated, 10000, Rising) },
            new[] { Ion(11, 5.01, Deprotonated, 4000, RisingLower) });

        var pair = Assert.Single(result.Pairs);
        Assert.Equal(1, pair.PositiveId);
        Assert.Equal(11, pair.NegativeId);
        Assert.Equal(Neutral, pair.NeutralMass, 3);
        Assert.Equal("[M+H]+", pair.PositiveAdduct);
        Assert.Equal("[M-H]-", pair.NegativeAdduct);
        Assert.Empty(result.PositiveOnly);
        Assert.Empty(result.NegativeOnly);
        Assert.Contains("↔", pair.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void A_compound_that_only_answers_in_one_polarity_is_kept_as_it_is()
    {
        var result = PolarityLink.Link(
            new[] { Ion(1, 5.00, Protonated, 10000, Rising) },
            new[] { Ion(11, 2.00, 281.2486, 8000, Falling) });   // a free fatty acid, negative only

        Assert.Empty(result.Pairs);
        Assert.Equal(new[] { 1 }, result.PositiveOnly);
        Assert.Equal(new[] { 11 }, result.NegativeOnly);
        Assert.Equal(2, result.Compounds);
    }

    [Fact]
    public void Retention_times_further_apart_than_the_tolerance_are_not_paired()
    {
        var apart = PolarityLink.Link(
            new[] { Ion(1, 5.00, Protonated, 10000, Rising) },
            new[] { Ion(11, 5.16, Deprotonated, 4000, RisingLower) });   // 0.16 min, over the 0.1 default
        Assert.Empty(apart.Pairs);

        var together = PolarityLink.Link(
            new[] { Ion(1, 5.00, Protonated, 10000, Rising) },
            new[] { Ion(11, 5.06, Deprotonated, 4000, RisingLower) });
        Assert.Single(together.Pairs);
    }

    [Fact]
    public void Profiles_that_contradict_each_other_are_not_paired()
    {
        // the right neutral mass, the right time, and heights that say the opposite across the batch
        var result = PolarityLink.Link(
            new[] { Ion(1, 5.00, Protonated, 10000, Rising) },
            new[] { Ion(11, 5.00, Deprotonated, 4000, Falling) });
        Assert.Empty(result.Pairs);
    }

    [Fact]
    public void The_polarity_that_measured_it_better_is_the_one_that_quantifies()
    {
        var negativeIsBetter = PolarityLink.Link(
            new[] { Ion(1, 5.00, Protonated, 10000, Rising, signalToNoise: 12) },
            new[] { Ion(11, 5.00, Deprotonated, 4000, RisingLower, signalToNoise: 44) });
        Assert.Equal(PolarityChoice.Negative, Assert.Single(negativeIsBetter.Pairs).Quantify);

        var positiveIsBetter = PolarityLink.Link(
            new[] { Ion(1, 5.00, Protonated, 10000, Rising, signalToNoise: 90) },
            new[] { Ion(11, 5.00, Deprotonated, 4000, RisingLower, signalToNoise: 44) });
        Assert.Equal(PolarityChoice.Positive, Assert.Single(positiveIsBetter.Pairs).Quantify);
    }

    [Fact]
    public void An_adduct_the_run_named_is_taken_at_its_word()
    {
        // the positive side is the sodium adduct, so the m/z is 782.567 and only the neutral agrees
        var result = PolarityLink.Link(
            new[] { Ion(1, 5.00, 782.5670, 10000, Rising, adduct: "[M+Na]+") },
            new[] { Ion(11, 5.00, Deprotonated, 4000, RisingLower, adduct: "[M-H]-") });

        var pair = Assert.Single(result.Pairs);
        Assert.Equal("[M+Na]+", pair.PositiveAdduct);
        Assert.Equal(Neutral, pair.NeutralMass, 3);
    }

    [Fact]
    public void A_feature_is_paired_once_and_the_better_offer_wins()
    {
        var result = PolarityLink.Link(
            new[] { Ion(1, 5.00, Protonated, 10000, Rising) },
            new[]
            {
                Ion(11, 5.00, Deprotonated, 4000, RisingLower),          // dead on
                Ion(12, 5.09, Deprotonated + 0.008, 900, RisingLower),   // the same neutral, further away
            });

        var pair = Assert.Single(result.Pairs);
        Assert.Equal(11, pair.NegativeId);
        Assert.Equal(new[] { 12 }, result.NegativeOnly);
    }

    [Fact]
    public void Both_runs_naming_it_the_same_makes_the_pair_stronger()
    {
        var named = PolarityLink.Link(
            new[] { Ion(1, 5.00, Protonated, 10000, Rising, name: "PC 34:1") },
            new[] { Ion(11, 5.04, Deprotonated, 4000, RisingLower, name: "PC 34:1") });
        var anonymous = PolarityLink.Link(
            new[] { Ion(1, 5.00, Protonated, 10000, Rising) },
            new[] { Ion(11, 5.04, Deprotonated, 4000, RisingLower) });

        Assert.True(Assert.Single(named.Pairs).Score > Assert.Single(anonymous.Pairs).Score);
    }

    [Fact]
    public void Injections_are_matched_across_the_polarities_by_name()
    {
        var positive = new[]
        {
            new SampleInfo(0, "liver_01_pos.wiff", "Control", "Sample", 1),
            new SampleInfo(1, "liver_02_pos.wiff", "Treated", "Sample", 2),
        };
        var negative = new[]
        {
            new SampleInfo(0, "liver_02_neg.wiff", "Treated", "Sample", 2),
            new SampleInfo(1, "liver_01_neg.wiff", "Control", "Sample", 1),
        };

        var pairs = PolarityLink.PairSamples(positive, negative);
        Assert.Equal(2, pairs.Count);
        Assert.Equal("liver_01_neg.wiff", pairs[0].NegativeFile);
        Assert.Equal("liver_02_neg.wiff", pairs[1].NegativeFile);
        Assert.All(pairs, p => Assert.Equal("name", p.How));
    }

    [Fact]
    public void Injections_the_names_do_not_match_fall_back_to_class_and_order()
    {
        var positive = new[] { new SampleInfo(0, "20240612_A.wiff", "Control", "Sample", 3) };
        var negative = new[] { new SampleInfo(7, "20240613_B.wiff", "control", "Sample", 3) };

        var pair = Assert.Single(PolarityLink.PairSamples(positive, negative));
        Assert.Equal(7, pair.NegativeFileId);
        Assert.Equal("class and injection order", pair.How);
    }

    [Fact]
    public void The_pairing_survives_a_round_trip_through_the_sidecar()
    {
        var folder = Path.Combine(Path.GetTempPath(), "opendial-polarity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var positiveAlignment = Path.Combine(folder, "batch_pos.arf2");
            var negativeAlignment = Path.Combine(folder, "batch_neg.arf2");
            var result = PolarityLink.Link(
                new[] { Ion(1, 5.00, Protonated, 10000, Rising, signalToNoise: 5) },
                new[] { Ion(11, 5.00, Deprotonated, 4000, RisingLower, signalToNoise: 50) },
                null,
                new[] { new SamplePairing(0, "liver_01_pos.wiff", 0, "liver_01_neg.wiff", "name") });

            var path = PolarityPairFile.FileFor(positiveAlignment);
            Assert.Equal(Path.Combine(folder, "batch_pos_polarity-pairs.json"), path);
            PolarityPairFile.Save(path, result, positiveAlignment, negativeAlignment, new PolarityLinkOptions());

            var read = PolarityPairFile.Load(path);
            Assert.NotNull(read);
            Assert.Equal(negativeAlignment, read!.NegativeAlignment);
            Assert.Equal(0.1, read.RtTolerance);
            var pair = Assert.Single(read.Result.Pairs);
            Assert.Equal(11, pair.NegativeId);
            Assert.Equal(PolarityChoice.Negative, pair.Quantify);
            Assert.Equal("liver_01_neg.wiff", Assert.Single(read.Result.Samples).NegativeFile);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void A_missing_sidecar_is_not_an_error()
    {
        Assert.Null(PolarityPairFile.Load(Path.Combine(Path.GetTempPath(), "no-such-" + Guid.NewGuid().ToString("N") + ".json")));
    }

    [Fact]
    public void Two_alignments_link_end_to_end_through_their_compounds()
    {
        var samples = new[]
        {
            new SampleInfo(0, "liver_01_pos.wiff", "Control", "Sample", 1),
            new SampleInfo(1, "liver_02_pos.wiff", "Control", "Sample", 2),
            new SampleInfo(2, "liver_03_pos.wiff", "Treated", "Sample", 3),
            new SampleInfo(3, "liver_04_pos.wiff", "Treated", "Sample", 4),
        };
        var negativeSamples = samples.Select(s => new SampleInfo(s.FileId, s.FileName.Replace("_pos", "_neg"), s.Class, s.SampleType, s.InjectionOrder)).ToList();

        var positive = new AlignmentTable(samples, new[]
        {
            Row(1, 5.00, Protonated, "[M+H]+", new double[] { 100, 210, 320, 480 }, 20),
            Row(2, 5.00, 782.5670, "[M+Na]+", new double[] { 40, 84, 128, 192 }, 8),    // the sodium of the same compound
            Row(3, 1.20, 300.1000, "[M+H]+", new double[] { 900, 880, 910, 870 }, 30),  // positive only
        }, "test");

        var negative = new AlignmentTable(negativeSamples, new[]
        {
            Row(11, 5.02, Deprotonated, "[M-H]-", new double[] { 60, 126, 190, 288 }, 55),
            Row(12, 2.10, 281.2486, "[M-H]-", new double[] { 700, 690, 720, 680 }, 40), // negative only
        }, "test");

        var result = PolarityLink.Link(positive, negative);

        Assert.Equal(4, result.Samples.Count);
        var pair = Assert.Single(result.Pairs);
        Assert.Equal(1, pair.PositiveId);                          // the protonated ion, not its sodium adduct
        Assert.Equal(11, pair.NegativeId);
        Assert.Equal(PolarityChoice.Negative, pair.Quantify);      // 55 against 20
        Assert.Equal(new[] { 3 }, result.PositiveOnly);            // the sodium is not a compound of its own
        Assert.Equal(new[] { 12 }, result.NegativeOnly);
        Assert.Equal(3, result.Compounds);
        Assert.Contains("3 compound(s)", result.Sentence(), StringComparison.Ordinal);
    }

    private static AlignmentSpotRow Row(int id, double rt, double mz, string adduct, double[] heights, double signalToNoise) => new()
    {
        Id = id,
        Rt = rt,
        Mz = mz,
        Adduct = adduct,
        AverageHeight = heights.Average(),
        SignalToNoiseAverage = signalToNoise,
        IsotopeWeight = 0,
        SampleHeights = heights.Select((h, i) => new SampleValue(i, "f" + i, "c", h)).ToList(),
    };
}
