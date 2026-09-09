using OpenDIAL.Pipeline.Results;
using OpenDIAL.Pipeline.Statistics;
using Xunit;

namespace OpenDIAL.Pipeline.Tests;

/// <summary>The two-factor analysis: the two-way ANOVA held to hand-computed sums of squares, and ASCA held to its own partition.</summary>
public class TwoFactorTests
{
    /// <summary>
    /// A balanced 2 × 2 design with three replicates per cell. Feature 0 responds to A only,
    /// feature 1 to B only, feature 2 to the interaction only (a crossover), feature 3 to nothing.
    /// </summary>
    private static (AnalysisTable Table, List<string> A, List<string> B) Design(double noise = 0.05, int seed = 3)
    {
        var samples = new List<SampleInfo>();
        var a = new List<string>();
        var b = new List<string>();
        var rng = new Random(seed);
        var rows = new List<double[]>();
        var id = 0;
        foreach (var la in new[] { "control", "treated" })
            foreach (var lb in new[] { "day0", "day7" })
                for (var r = 0; r < 3; r++)
                {
                    samples.Add(new SampleInfo(id, $"{la}-{lb}-{r + 1}", la, "Sample", id + 1) { Factor = lb });
                    a.Add(la); b.Add(lb);
                    var isA = la == "treated" ? 1 : 0;
                    var isB = lb == "day7" ? 1 : 0;
                    rows.Add(new[]
                    {
                        10 + 2.0 * isA + noise * rng.NextDouble(),
                        10 + 1.5 * isB + noise * rng.NextDouble(),
                        10 + 2.0 * (isA == isB ? 1 : 0) + noise * rng.NextDouble(),   // high on the diagonal: pure interaction
                        10 + noise * rng.NextDouble(),
                    });
                    id++;
                }
        var values = new double[rows.Count, 4];
        for (var i = 0; i < rows.Count; i++) for (var j = 0; j < 4; j++) values[i, j] = rows[i][j];
        var features = Enumerable.Range(0, 4).Select(j => new AlignmentSpotRow { Id = j, Name = $"PC 3{j}:1", Ontology = "PC", Mz = 700 + j, Rt = 5 + j }).ToList();
        return (new AnalysisTable(values, samples, features, "log10"), a, b);
    }

    [Fact]
    public void The_two_way_anova_assigns_each_feature_to_its_effect()
    {
        var (table, a, b) = Design();
        var result = TwoFactor.Anova(table, a, b, "treatment", "day", interaction: true);
        Assert.True(result.Interaction);
        Assert.Equal(new[] { "control", "treated" }, result.LevelsA);
        Assert.Equal(new[] { "day0", "day7" }, result.LevelsB);
        Assert.Equal(4, result.Features.Count);

        var byA = result.Features[0];
        Assert.True(byA.AdjustedPA < 1e-6 && byA.AdjustedPB > 0.05 && byA.AdjustedPAB > 0.05, $"{byA.PA} {byA.PB} {byA.PAB}");
        var byB = result.Features[1];
        Assert.True(byB.AdjustedPB < 1e-6 && byB.AdjustedPA > 0.05 && byB.AdjustedPAB > 0.05, $"{byB.PA} {byB.PB} {byB.PAB}");
        var byAB = result.Features[2];
        Assert.True(byAB.AdjustedPAB < 1e-6 && byAB.AdjustedPA > 0.05 && byAB.AdjustedPB > 0.05, $"{byAB.PA} {byAB.PB} {byAB.PAB}");
        var none = result.Features[3];
        Assert.True(none.AdjustedPA > 0.05 && none.AdjustedPB > 0.05 && none.AdjustedPAB > 0.05);
        Assert.Equal("A", byA.Significant(0.05));
        Assert.Equal("A×B", byAB.Significant(0.05));
        Assert.Equal(4, byA.CellMeans.Count);
        Assert.Contains("2 × 2 design, 4 of 4 cells filled, 12 injections", result.Message);
    }

    /// <summary>
    /// The F of a balanced two-way design by hand: SS_A = b·r·Σ(ȳ_a − ȳ)², and so on, with the
    /// residual on (a·b·(r−1)) degrees of freedom. The model-comparison route must give the same.
    /// </summary>
    [Fact]
    public void The_f_matches_the_textbook_sums_of_squares_on_a_balanced_design()
    {
        var (table, a, b) = Design(noise: 0.5, seed: 11);
        var result = TwoFactor.Anova(table, a, b, "A", "B", interaction: true, adjustment: PAdjustment.None);
        var j = 2;
        var y = Enumerable.Range(0, 12).Select(i => table.Values[i, j]).ToArray();
        var grand = y.Average();
        double SsOf(Func<int, string> level, IEnumerable<string> levels) => levels.Sum(l => { var idx = Enumerable.Range(0, 12).Where(i => level(i) == l).ToList(); return idx.Count * Math.Pow(idx.Average(i => y[i]) - grand, 2); });
        var ssA = SsOf(i => a[i], new[] { "control", "treated" });
        var ssB = SsOf(i => b[i], new[] { "day0", "day7" });
        var ssCells = SsOf(i => a[i] + "|" + b[i], new[] { "control|day0", "control|day7", "treated|day0", "treated|day7" });
        var ssAB = ssCells - ssA - ssB;
        var ssTotal = y.Sum(v => (v - grand) * (v - grand));
        var ssE = ssTotal - ssCells;
        var mse = ssE / 8;
        Assert.Equal(ssA / 1 / mse, result.Features[j].FA, 6);
        Assert.Equal(ssB / 1 / mse, result.Features[j].FB, 6);
        Assert.Equal(ssAB / 1 / mse, result.Features[j].FAB, 6);
        Assert.Equal(Distributions.FisherUpper(ssAB / mse, 1, 8), result.Features[j].PAB, 9);
    }

    [Fact]
    public void An_unbalanced_design_still_tests_and_an_empty_cell_drops_the_interaction_it_cannot_see()
    {
        var (table, a, b) = Design();
        // drop two of the treated-day7 injections: unbalanced but every cell filled
        var keep = Enumerable.Range(0, 12).Where(i => !(a[i] == "treated" && b[i] == "day7" && i % 3 != 0)).ToList();
        var sub = table.SelectSamples(keep);
        var result = TwoFactor.Anova(sub, keep.Select(i => a[i]).ToList(), keep.Select(i => b[i]).ToList(), "A", "B");
        Assert.True(result.Interaction);
        Assert.True(result.Features[0].AdjustedPA < 1e-4);
        Assert.Contains("4 of 4 cells filled, 10 injections", result.Message);

        // a factor with one level is refused with a sentence, not a crash
        var single = TwoFactor.Anova(table, a, Enumerable.Repeat("day0", 12).ToList(), "A", "B");
        Assert.Empty(single.Features);
        Assert.Contains("at least two levels", single.Message);

        // one injection per cell: the interaction cannot be told from the error
        var one = Enumerable.Range(0, 12).Where(i => i % 3 == 0).ToList();
        var oneEach = TwoFactor.Anova(table.SelectSamples(one), one.Select(i => a[i]).ToList(), one.Select(i => b[i]).ToList(), "A", "B", interaction: true);
        Assert.False(oneEach.Interaction);
        Assert.Contains("no cell has a replicate", oneEach.Message);
    }

    [Fact]
    public void Asca_partitions_the_variation_by_factor_and_scores_the_injections()
    {
        var (table, a, b) = Design();
        var scaled = table.ToDataMatrix(ValueScaling.Auto);
        var result = TwoFactor.Asca(scaled, a, b, "treatment", "day", interaction: true, permutations: 199);
        Assert.Equal(3, result.Effects.Count);
        var (ea, eb, eab) = (result.Effects[0], result.Effects[1], result.Effects[2]);
        Assert.Equal("treatment", ea.Name);
        Assert.Equal("treatment × day", eab.Name);
        // one feature answers each effect, so each holds about a quarter of the (auto-scaled) variation, the noise feature the rest
        Assert.InRange(ea.PercentOfVariation, 18, 32);
        Assert.InRange(eb.PercentOfVariation, 18, 32);
        Assert.InRange(eab.PercentOfVariation, 18, 32);
        Assert.InRange(result.ResidualPercent, 15, 40);
        Assert.InRange(ea.PercentOfVariation + eb.PercentOfVariation + eab.PercentOfVariation + result.ResidualPercent, 99.9, 100.1);
        Assert.True(ea.PermutationP < 0.05 && eb.PermutationP < 0.05 && eab.PermutationP < 0.05, $"{ea.PermutationP} {eb.PermutationP} {eab.PermutationP}");
        Assert.Equal(12, ea.Scores.Count);
        // on the treatment effect the two levels sit on opposite sides of the first component
        var control = ea.Scores.Where(s => s.LevelA == "control").Average(s => s.Pc1);
        var treated = ea.Scores.Where(s => s.LevelA == "treated").Average(s => s.Pc1);
        Assert.True(Math.Sign(control) != Math.Sign(treated) && Math.Abs(control - treated) > 1);
        Assert.Contains("permutations", result.Message);

        // shuffle the design and nothing is left to find
        var rng = new Random(1);
        var shuffledA = a.OrderBy(_ => rng.Next()).ToList();
        var nothing = TwoFactor.Asca(scaled, shuffledA, b, "treatment", "day", interaction: false, permutations: 199);
        Assert.True(nothing.Effects[0].PermutationP > 0.05, nothing.Effects[0].PermutationP.ToString());
    }

    [Fact]
    public void Least_squares_reports_the_rank_of_a_dependent_design()
    {
        var x = new[] { new[] { 1.0, 1, 0 }, new[] { 1.0, 0, 1 }, new[] { 1.0, 1, 0 }, new[] { 1.0, 0, 1 } };   // the third column is 1 − the second
        var (rss, rank) = Ols.Fit(x, new[] { 2.0, 4, 2, 4 });
        Assert.Equal(2, rank);
        Assert.Equal(0, rss, 9);
    }
}
