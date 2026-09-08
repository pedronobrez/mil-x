using OpenDIAL.Pipeline.Results;
using OpenDIAL.Pipeline.Statistics;
using Xunit;

namespace OpenDIAL.Pipeline.Tests;

/// <summary>
/// The orthogonal discriminant model: that it puts the separation on one component and everything
/// else beside it, that the S-plot picks out the features that carry it, and that it is no easier
/// to fool than the plain model.
/// </summary>
public class OrthogonalStatisticsTests
{
    /// <summary>
    /// Two classes that really differ, with a second source of variation — call it the extraction
    /// batch — laid across both of them. A plain model mixes the two; this one should not.
    /// </summary>
    private static (IReadOnlyList<AlignmentSpotRow> Features, IReadOnlyList<SampleInfo> Samples, HashSet<int> Discriminating) Fixture(int seed = 3)
    {
        var samples = new List<SampleInfo>();
        for (var i = 0; i < 16; i++)
        {
            samples.Add(new SampleInfo(i, (i < 8 ? "T" : "C") + (i % 8 + 1), i < 8 ? "treated" : "control", "Sample", i + 1));
        }

        var rng = new Random(seed);
        var features = new List<AlignmentSpotRow>();
        var discriminating = new HashSet<int>();
        for (var k = 0; k < 60; k++)
        {
            var level = 3000.0 * (k + 1);
            var separates = k % 6 == 0;
            if (separates) discriminating.Add(k);
            var heights = new double[samples.Count];
            for (var i = 0; i < samples.Count; i++)
            {
                var byClass = separates && samples[i].Class == "treated" ? 4.0 : 1.0;
                // the nuisance: every other injection, whatever its class, sits higher
                var nuisance = i % 2 == 0 ? 1.8 : 1.0;
                heights[i] = level * byClass * nuisance * (0.9 + rng.NextDouble() * 0.2);
            }
            features.Add(new AlignmentSpotRow
            {
                Id = k,
                Name = separates ? $"PC {30 + k}:1" : "Unknown",
                Ontology = separates ? "PC" : string.Empty,
                Mz = 700 + k,
                Rt = 5 + k * 0.1,
                AverageHeight = heights.Average(),
                SamplePeaks = samples.Select((s, i) => new AlignedSamplePeak(
                    s.FileId, s.FileName, s.Class, s.SampleType, 5, 4.9, 5.1, 700 + k, heights[i], heights[i] * 10, 20, false)).ToList(),
            });
        }
        return (features, samples, discriminating);
    }

    [Fact]
    public void The_separation_lands_on_the_predictive_component_and_the_nuisance_beside_it()
    {
        var (features, samples, discriminating) = Fixture();
        var result = OrthogonalProjection.Compute(DataMatrix.Build(features, samples), orthogonalComponents: 1, permutations: 200);

        Assert.NotEmpty(result.Scores);
        Assert.Equal(2, result.Classes.Count);
        Assert.Equal(samples.Count, result.CorrectlyClassified);

        // left to right is the class difference: no overlap between the two groups
        var treated = result.Scores.Where(s => s.Class == "treated").Select(s => s.Predictive).ToList();
        var control = result.Scores.Where(s => s.Class == "control").Select(s => s.Predictive).ToList();
        Assert.True(treated.Max() < control.Min() || control.Max() < treated.Min(),
            "the two classes should not overlap along the predictive component");

        // up and down is the nuisance, which cuts across both classes
        var even = result.Scores.Where((s, i) => i % 2 == 0).Average(s => s.Orthogonal);
        var odd = result.Scores.Where((s, i) => i % 2 == 1).Average(s => s.Orthogonal);
        Assert.True(Math.Abs(even - odd) > 0.5, $"the orthogonal component should hold the nuisance; means {even:F2} and {odd:F2}");

        // and the features that actually differ are the ones out at the ends of the S-plot
        var top = result.Loadings.OrderByDescending(l => Math.Abs(l.Covariance)).Take(discriminating.Count).Select(l => l.FeatureId).ToHashSet();
        Assert.All(discriminating, id => Assert.Contains(id, top));
        Assert.All(result.Loadings.Where(l => discriminating.Contains(l.FeatureId)), l =>
        {
            Assert.True(Math.Abs(l.Correlation) > 0.8, $"{l.Label} carries the separation but correlates only {l.Correlation:F2}");
            Assert.Equal("treated", l.Side);
        });
    }

    [Fact]
    public void It_survives_cross_validation_when_the_difference_is_real()
    {
        var (features, samples, _) = Fixture();
        var result = OrthogonalProjection.Compute(DataMatrix.Build(features, samples), 1, 200);

        Assert.True(result.Q2 > 0.5, $"Q2 was only {result.Q2:F2}");
        Assert.True(result.PermutationP <= 0.05, $"permutation p was {result.PermutationP:F3}");
        Assert.True(result.PredictiveVarianceX > 0, "the predictive part should hold some of the variance");
        Assert.True(result.OrthogonalVarianceX > 0, "and the stripped part should hold some too");
    }

    [Fact]
    public void Labels_with_nothing_behind_them_do_not_survive()
    {
        var (features, samples, _) = Fixture();
        // The same injections under a label that describes nothing: it splits each real class down
        // the middle and takes equal numbers from each side of the nuisance, so there is no
        // chemistry behind it at all.
        var scrambled = samples.Select((s, i) => s with { Class = i / 2 % 2 == 0 ? "left" : "right" }).ToList();
        Assert.Equal(4, scrambled.Count(s => s.Class == "left" && s.FileId < 8));
        Assert.Equal(4, scrambled.Count(s => s.Class == "left" && s.FileId % 2 == 0));
        var result = OrthogonalProjection.Compute(DataMatrix.Build(features, scrambled), 1, 200);

        Assert.False(result.Q2 >= 0.4 && result.PermutationP <= 0.05,
            $"a rotation is not evidence; Q2 {result.Q2:F2}, p {result.PermutationP:F3}");
    }

    [Fact]
    public void Three_classes_are_refused_with_a_reason()
    {
        var (features, samples, _) = Fixture();
        var three = samples.Select((s, i) => s with { Class = i < 5 ? "a" : i < 10 ? "b" : "c" }).ToList();
        var result = OrthogonalProjection.Compute(DataMatrix.Build(features, three), 1, 0);

        Assert.Empty(result.Scores);
        Assert.Contains("two classes", result.Message);
    }

    [Fact]
    public void The_fast_fit_agrees_with_the_plain_one()
    {
        var (features, samples, _) = Fixture(seed: 9);
        var matrix = DataMatrix.Build(features, samples);
        var y = samples.Select(s => s.Class == "treated" ? -1.0 : 1.0).ToArray();
        var mean = y.Average();
        for (var i = 0; i < y.Length; i++) y[i] -= mean;

        for (var ortho = 0; ortho <= 2; ortho++)
        {
            for (var left = 0; left < samples.Count; left++)
            {
                var plain = OrthogonalProjection.PredictPlainly(matrix.Values, y, left, ortho);
                var quick = OrthogonalProjection.PredictQuickly(matrix.Values, y, left, ortho);
                Assert.Equal(plain, quick, 8);
            }
        }
    }
}
