using MilX.Pipeline.Results;
using MilX.Pipeline.Statistics;
using Xunit;

namespace MilX.Pipeline.Tests;

public class SupervisedStatisticsTests
{
    private static AlignmentSpotRow Feature(int id, string name, string ontology, IReadOnlyList<SampleInfo> samples, double[] heights) => new()
    {
        Id = id,
        Name = name,
        Ontology = ontology,
        Mz = 700 + id,
        Rt = 5 + id * 0.05,
        AverageHeight = heights.Average(),
        SamplePeaks = samples.Select((s, i) => new AlignedSamplePeak(s.FileId, s.FileName, s.Class, s.SampleType, 5, 4.9, 5.1, 700 + id, heights[i], heights[i] * 10, 20, false)).ToList(),
    };

    // ------------------------------------------------------------------ discriminant analysis

    [Fact]
    public void A_real_separation_gives_a_high_cross_validated_score_and_names_the_features_that_carry_it()
    {
        var samples = Enumerable.Range(0, 12)
            .Select(i => new SampleInfo(i, (i < 6 ? "T" : "C") + i, i < 6 ? "treated" : "control", "Sample", i + 1))
            .ToList();
        var rng = new Random(5);
        double J() => 0.9 + rng.NextDouble() * 0.2;
        var features = new List<AlignmentSpotRow>();
        for (var k = 0; k < 25; k++)
        {
            var level = 1000.0 * (k + 1);
            var heights = samples.Select((s, i) => (k < 5 && i < 6 ? level * 6 : level) * J()).ToArray();
            features.Add(Feature(k, k < 5 ? "driver " + k : "noise " + k, "PC", samples, heights));
        }

        var result = PartialLeastSquares.Compute(DataMatrix.Build(features, samples), components: 2, permutations: 100);

        Assert.Equal(12, result.Scores.Count);
        Assert.Equal(2, result.Classes.Count);
        Assert.True(result.R2Y > 0.5, $"R2Y was {result.R2Y:F2}");
        Assert.True(result.Q2 > 0.4, $"Q2 was {result.Q2:F2}");
        Assert.True(result.PermutationP < 0.05, $"the permutation test gave p {result.PermutationP:F3}");
        Assert.Equal(12, result.CorrectlyClassified);

        // the five features that actually change must be the ones the model leans on
        var top = result.Loadings.OrderByDescending(l => l.Vip).Take(5).ToList();
        Assert.All(top, l => Assert.StartsWith("driver ", l.Label));
        Assert.All(top, l => Assert.True(l.Vip > 1.0, $"{l.Label} had VIP {l.Vip:F2}"));
    }

    [Fact]
    public void Labels_with_no_real_difference_do_not_survive_cross_validation()
    {
        var samples = Enumerable.Range(0, 12)
            .Select(i => new SampleInfo(i, "S" + i, i % 2 == 0 ? "alpha" : "beta", "Sample", i + 1))
            .ToList();
        var rng = new Random(9);
        var features = Enumerable.Range(0, 40)
            .Select(k => Feature(k, "f" + k, "PC", samples, samples.Select(_ => 1000.0 * (k + 1) * (0.8 + rng.NextDouble() * 0.4)).ToArray()))
            .ToList();

        var result = PartialLeastSquares.Compute(DataMatrix.Build(features, samples), components: 2, permutations: 100);

        // it will still fit its own samples; the point is that it must not survive leaving one out
        Assert.True(result.Q2 < 0.3, $"Q2 was {result.Q2:F2} on labels with nothing behind them");
        Assert.True(result.PermutationP > 0.05, $"the permutation test gave p {result.PermutationP:F3}");
        Assert.Contains("Q²", result.Message);
    }

    [Fact]
    public void One_class_or_too_few_injections_is_reported_rather_than_fitted()
    {
        var samples = Enumerable.Range(0, 6).Select(i => new SampleInfo(i, "S" + i, "one", "Sample", i + 1)).ToList();
        var features = Enumerable.Range(0, 10).Select(k => Feature(k, "f" + k, "PC", samples, samples.Select(_ => 1000.0 * (k + 1)).ToArray())).ToList();
        var single = PartialLeastSquares.Compute(DataMatrix.Build(features, samples));
        Assert.Empty(single.Scores);
        Assert.Contains("two classes", single.Message);

        var pair = samples.Take(2).Select((s, i) => new SampleInfo(i, s.FileName, i == 0 ? "a" : "b", "Sample", i + 1)).ToList();
        var few = PartialLeastSquares.Compute(DataMatrix.Build(features.Take(4).ToList(), pair));
        Assert.Empty(few.Scores);
        Assert.Contains("four injections", few.Message);
    }

    // ------------------------------------------------------------------ drift correction

    [Fact]
    public void Correcting_the_drift_tightens_the_quality_controls()
    {
        // twenty injections, every fourth a quality control, response falling to a third across the run
        var samples = Enumerable.Range(0, 20)
            .Select(i => new SampleInfo(i, (i % 4 == 0 ? "QC" : "S") + i, i % 4 == 0 ? "qc" : "sample", i % 4 == 0 ? "QC" : "Sample", i + 1))
            .ToList();
        var rng = new Random(13);
        var features = new List<AlignmentSpotRow>();
        for (var k = 0; k < 15; k++)
        {
            var level = 5000.0 * (k + 1);
            var heights = samples.Select((s, i) =>
            {
                var drift = 1.0 - 0.65 * i / 19.0;                  // the instrument fading
                var noise = 0.98 + rng.NextDouble() * 0.04;
                return level * drift * noise;
            }).ToArray();
            features.Add(Feature(k, "f" + k, "PC", samples, heights));
        }

        var result = BatchCorrection.Apply(features, samples);

        Assert.Equal(5, result.QualityControls);
        Assert.Equal(15, result.Corrected);
        Assert.True(result.MedianCvBefore > 15, $"the drift should show as spread, got {result.MedianCvBefore:F1} %");
        Assert.True(result.MedianCvAfter < 5, $"after correcting, the controls should be tight, got {result.MedianCvAfter:F1} %");
        Assert.All(result.Features, f => Assert.True(f.CvAfter <= f.CvBefore + 0.5));
    }

    [Fact]
    public void Each_batch_is_corrected_on_its_own()
    {
        // two batches, the second running at half the response of the first
        var samples = Enumerable.Range(0, 16)
            .Select(i => new SampleInfo(i, "S" + i, i % 4 == 0 ? "qc" : "sample", i % 4 == 0 ? "QC" : "Sample", i + 1, i < 8 ? 1 : 2))
            .ToList();
        var features = new List<AlignmentSpotRow>();
        for (var k = 0; k < 10; k++)
        {
            var level = 4000.0 * (k + 1);
            var heights = samples.Select((s, i) => level * (s.Batch == 1 ? 1.0 : 0.5)).ToArray();
            features.Add(Feature(k, "f" + k, "PC", samples, heights));
        }

        var result = BatchCorrection.Apply(features, samples);

        Assert.Equal(2, result.Batches);
        Assert.True(result.MedianCvBefore > 20, $"the step between batches should show, got {result.MedianCvBefore:F1} %");
        Assert.True(result.MedianCvAfter < 1, $"after correcting, the step should be gone, got {result.MedianCvAfter:F1} %");
    }

    [Fact]
    public void Without_enough_quality_controls_nothing_is_changed_and_it_says_so()
    {
        var samples = Enumerable.Range(0, 8).Select(i => new SampleInfo(i, "S" + i, "sample", "Sample", i + 1)).ToList();
        var features = Enumerable.Range(0, 5)
            .Select(k => Feature(k, "f" + k, "PC", samples, samples.Select((_, i) => 1000.0 * (k + 1) * (1 - 0.5 * i / 7.0)).ToArray()))
            .ToList();

        var result = BatchCorrection.Apply(features, samples);

        Assert.Equal(0, result.Corrected);
        Assert.Equal(0, result.QualityControls);
        Assert.Contains("marked QC", result.Message);
        // the values must come back untouched
        Assert.Equal(1000.0, result.Values[0, 0], 3);
    }

    [Fact]
    public void The_fast_cross_validation_agrees_with_the_plain_one()
    {
        // a matrix wide enough that the two paths could diverge, and a real class split
        var rng = new Random(5);
        var n = 10;
        var p = 300;
        var x = new double[n, p];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < p; j++)
            {
                var shift = j % 7 == 0 ? (i < n / 2 ? 1.5 : -1.5) : 0;
                x[i, j] = shift + rng.NextDouble() * 2 - 1;
            }
        }
        var classes = Enumerable.Range(0, n).Select(i => i < n / 2 ? "a" : "b").ToArray();
        var y = PartialLeastSquares.DummyFor(classes, new[] { "a", "b" });

        var plain = PartialLeastSquares.CrossValidatePlainly(x, y, 2);
        var quick = PartialLeastSquares.CrossValidateQuickly(x, y, 2);
        Assert.Equal(plain, quick, 9);
    }
}
