using MilX.Pipeline.Results;
using MilX.Pipeline.Statistics;
using Xunit;

namespace MilX.Pipeline.Tests;

/// <summary>
/// The one-factor engine against numbers that are known: the distributions against tables, the
/// tests against hand-computable cases, the preprocessing against its own definitions, and the
/// models against data built to have one answer.
/// </summary>
public class OneFactorStatisticsTests
{
    // ------------------------------------------------------------------ distributions

    [Theory]
    [InlineData(0.0, 0.5)]
    [InlineData(1.0, 0.8413447460685429)]
    [InlineData(1.959963984540054, 0.975)]
    [InlineData(-2.5, 0.006209665325776132)]
    public void The_normal_distribution_matches_the_table(double z, double expected)
    {
        Assert.Equal(expected, Distributions.NormalCdf(z), 9);
        Assert.Equal(z, Distributions.NormalQuantile(expected), 7);
    }

    [Theory]
    [InlineData(2.0, 10, 0.07339)]        // t = 2 on 10 df, two-sided
    [InlineData(2.228, 10, 0.05)]         // the 97.5th percentile of t(10)
    [InlineData(1.0, 1e6, 0.31731)]       // ≈ normal
    public void Student_t_two_sided_p_matches_the_table(double t, double df, double expected)
    {
        Assert.Equal(expected, Distributions.StudentTwoSided(t, df), 4);
    }

    [Fact]
    public void F_and_chi_square_tails_match_the_tables()
    {
        Assert.Equal(0.05, Distributions.FisherUpper(4.256, 2, 9), 3);      // F(2, 9) at 5 %
        Assert.Equal(0.05, Distributions.ChiSquareUpper(5.991, 2), 3);      // χ²(2) at 5 %
        Assert.Equal(0.05, Distributions.ChiSquareUpper(3.841, 1), 3);
    }

    [Fact]
    public void The_hypergeometric_tail_is_the_over_representation_test()
    {
        // 5 draws from 20 with 8 marked: P(X ≥ 4) = [C(8,4)C(12,1) + C(8,5)C(12,0)] / C(20,5) = (70·12 + 56) / 15504
        var expected = (70.0 * 12 + 56) / 15504;
        Assert.Equal(expected, Distributions.HypergeometricUpper(4, 5, 8, 20), 10);
        Assert.Equal(1, Distributions.HypergeometricUpper(0, 5, 8, 20));
    }

    [Fact]
    public void Benjamini_Hochberg_matches_R()
    {
        var p = new[] { 0.01, 0.04, 0.03, 0.2, 0.5 };
        var adjusted = MultipleTesting.Adjust(p, PAdjustment.FalseDiscoveryRate);
        // R: p.adjust(c(0.01,0.04,0.03,0.2,0.5), "BH") = 0.05000 0.06667 0.06667 0.25000 0.50000
        Assert.Equal(new[] { 0.05, 0.0666666667, 0.0666666667, 0.25, 0.5 }, adjusted.Select(v => Math.Round(v, 10)));
        var holm = MultipleTesting.Adjust(p, PAdjustment.Holm);
        Assert.Equal(new[] { 0.05, 0.12, 0.12, 0.4, 0.5 }, holm.Select(v => Math.Round(v, 10)));
        Assert.Equal(0.05, MultipleTesting.Adjust(p, PAdjustment.Bonferroni)[0], 10);
    }

    // ------------------------------------------------------------------ tests

    [Fact]
    public void Welch_and_pooled_t_tests_match_R()
    {
        var a = new[] { 5.1, 4.9, 5.6, 5.8, 6.0 };
        var b = new[] { 4.0, 4.2, 3.9, 4.6, 4.1 };
        // by hand: means 5.48 and 4.16, variances 0.217 and 0.073, so t = 1.32 / sqrt(0.058) = 5.481;
        // Welch's df = 0.058² / (0.0434²/4 + 0.0146²/4) = 6.418, the pooled df = 8
        var (welchT, welchP) = Univariate.TTest(a, b, equalVariance: false);
        Assert.Equal(5.481, welchT, 3);
        Assert.Equal(Distributions.StudentTwoSided(5.481, 6.418), welchP, 4);
        var (pooledT, pooledP) = Univariate.TTest(a, b, equalVariance: true);
        Assert.Equal(5.481, pooledT, 3);
        Assert.Equal(Distributions.StudentTwoSided(5.481, 8), pooledP, 4);
        Assert.True(pooledP < welchP);
    }

    [Fact]
    public void The_exact_Mann_Whitney_matches_R()
    {
        var a = new[] { 1.1, 2.3, 3.5, 4.2 };
        var b = new[] { 5.1, 6.2, 7.0, 8.4 };
        // R: wilcox.test(a, b): W = 0, p-value = 0.02857 (exact)
        var (u, p) = Univariate.MannWhitney(a, b);
        Assert.Equal(0, u);
        Assert.Equal(0.02857, p, 4);
        var (_, same) = Univariate.MannWhitney(new[] { 1.0, 2, 3, 4 }, new[] { 1.5, 2.5, 3.5, 4.5 });
        Assert.True(same > 0.5);
    }

    [Fact]
    public void One_way_anova_matches_R()
    {
        var groups = new[] { new[] { 1.0, 2, 3 }, new[] { 2.0, 3, 4 }, new[] { 5.0, 6, 7 } };
        // by hand: between = 3·(1.667² + 0.667² + 2.333²) = 26 on 2 df, within = 6 on 6 df, F = 13
        var table = Table(groups, out _);
        var result = Univariate.Anova(table, adjustment: PAdjustment.None);
        Assert.Single(result.Features);
        Assert.Equal(13, result.Features[0].Statistic, 6);
        Assert.Equal(Distributions.FisherUpper(13, 2, 6), result.Features[0].P, 8);
        Assert.True(result.Features[0].P < 0.01);
        Assert.Equal(3, result.Features[0].PostHoc.Count);
        var kruskal = Univariate.Anova(table, nonParametric: true, adjustment: PAdjustment.None);
        Assert.True(kruskal.Features[0].P < 0.1);
    }

    [Fact]
    public void Fold_change_is_the_ratio_of_class_means_and_the_p_comes_from_the_transformed_values()
    {
        var samples = new[]
        {
            new SampleInfo(0, "a1", "A", "Sample"), new SampleInfo(1, "a2", "A", "Sample"), new SampleInfo(2, "a3", "A", "Sample"),
            new SampleInfo(3, "b1", "B", "Sample"), new SampleInfo(4, "b2", "B", "Sample"), new SampleInfo(5, "b3", "B", "Sample"),
        };
        var feature = new AlignmentSpotRow { Id = 7, Name = "PC 34:1", Ontology = "PC" };
        var linear = new AnalysisTable(new double[,] { { 400 }, { 420 }, { 380 }, { 100 }, { 110 }, { 90 } }, samples, new[] { feature }, "ratio");
        var transformed = Preprocessing.Transform(linear, Transformation.Log10);
        var result = Univariate.Compare(linear, transformed, "A", "B");
        var row = Assert.Single(result.Features);
        Assert.Equal(4, row.FoldChange, 6);
        Assert.Equal(2, row.Log2FoldChange, 6);
        Assert.True(row.P < 0.001);
        Assert.Equal(row.P, row.AdjustedP, 10);
    }

    // ------------------------------------------------------------------ preprocessing

    [Fact]
    public void Preprocessing_drops_imputes_filters_and_normalises_as_it_says()
    {
        var samples = Enumerable.Range(0, 4).Select(i => new SampleInfo(i, "s" + i, i < 2 ? "A" : "B", i == 3 ? "QC" : "Sample")).ToList();
        var features = Enumerable.Range(0, 6).Select(j => new AlignmentSpotRow { Id = j, Name = "F" + j }).ToList();
        var values = new double[4, 6];
        var rng = new Random(1);
        for (var i = 0; i < 4; i++) for (var j = 0; j < 6; j++) values[i, j] = 100 * (j + 1) + rng.NextDouble();
        values[0, 5] = double.NaN; values[1, 5] = double.NaN; values[2, 5] = double.NaN;   // 75 % missing: dropped
        values[0, 4] = double.NaN;                                                          // imputed
        var raw = new AnalysisTable(values, samples, features, "height");
        var options = new PreprocessingOptions { Filter = FilterMethod.None, Normalization = SampleNormalization.Sum, Transform = Transformation.Log10 };
        var data = Preprocessing.Run(raw, options);
        Assert.Equal(1, data.Report.DroppedForMissing);
        Assert.Equal(1, data.Report.Imputed);
        Assert.Equal(5, data.Report.FeaturesOut);
        // a fifth of the smallest positive value stands in for the gap
        var column = data.Filtered.Column(4);
        Assert.Equal(column.Skip(1).Min() / 5, column[0], 6);
        // sum normalisation leaves every injection with the same total
        var sums = Enumerable.Range(0, 4).Select(i => data.Normalized.Row(i).Sum()).ToList();
        Assert.All(sums, s => Assert.Equal(sums[0], s, 6));
        // and the log went through
        Assert.Equal(Math.Log10(data.Normalized.Values[0, 0]), data.Transformed.Values[0, 0], 10);
        Assert.Equal(5, data.Scaled.FeatureCount);

        // the variance filter takes the flattest features off the bottom
        var (filtered, dropped) = Preprocessing.Filter(data.Filtered, FilterMethod.InterquartileRange, 0.4);
        Assert.Equal(2, dropped);
        Assert.Equal(3, filtered.FeatureCount);
        Assert.Equal(0, Preprocessing.DefaultFilterFraction(100));
        Assert.Equal(0.25, Preprocessing.DefaultFilterFraction(2000));
    }

    [Fact]
    public void Quotient_normalisation_undoes_a_dilution()
    {
        var samples = Enumerable.Range(0, 3).Select(i => new SampleInfo(i, "s" + i, "A", "Sample")).ToList();
        var features = Enumerable.Range(0, 5).Select(j => new AlignmentSpotRow { Id = j, Name = "F" + j }).ToList();
        var values = new double[3, 5];
        for (var j = 0; j < 5; j++) { values[0, j] = 100 * (j + 1); values[1, j] = 100 * (j + 1); values[2, j] = 50 * (j + 1); }   // the third is diluted by half
        var table = new AnalysisTable(values, samples, features, "height");
        var pqn = Preprocessing.Normalize(table, SampleNormalization.ProbabilisticQuotient, null);
        for (var j = 0; j < 5; j++) Assert.Equal(pqn.Values[0, j], pqn.Values[2, j], 6);
    }

    // ------------------------------------------------------------------ ratios and names

    [Fact]
    public void Lipid_names_are_read_for_class_chains_and_standards()
    {
        var pc = LipidNames.Parse("PC 34:1");
        Assert.Equal(("PC", 34, 1), (pc.Class, pc.Carbons, pc.DoubleBonds));
        var tg = LipidNames.Parse("TG 52:2|TG 16:0_18:1_18:1");
        Assert.Equal((52, 2), (tg.Carbons, tg.DoubleBonds));
        var sm = LipidNames.Parse("SM d18:1/16:0", "SM");
        Assert.Equal((34, 1), (sm.Carbons, sm.DoubleBonds));
        Assert.False(sm.IsStandard);
        var cer = LipidNames.Parse("Cer 18:1;O2/16:0", "Cer");
        Assert.Equal((34, 1), (cer.Carbons, cer.DoubleBonds));
        var ether = LipidNames.Parse("PC O-34:1", "EtherPC");
        Assert.Equal(("EtherPC", 34, 1), (ether.Class, ether.Carbons, ether.DoubleBonds));
        Assert.True(LipidNames.Parse("CE 15:0 d7").IsStandard);
        Assert.True(LipidNames.Parse("PC 15:0-18:1(d7)").IsStandard);
        Assert.True(LipidNames.Parse("Cholesterol(d7)").IsStandard);
        Assert.False(LipidNames.Parse("SM d70:1;2O").IsStandard);
        Assert.Equal("PC", LipidNames.Parse("low score: PC 36:2").Class);
        Assert.True(LipidNames.StandardScore("PC 15:0-18:1(d7)", "PC", "PC") > LipidNames.StandardScore("PC 17:0/18:1", "PC", "PC"));
        Assert.True(LipidNames.StandardScore("PC 17:0/18:1", "PC", "PC") > LipidNames.StandardScore("PC 16:0/18:1", "PC", "PC"));
    }

    [Fact]
    public void Ratios_divide_each_class_by_its_own_standard_and_leave_the_standard_out()
    {
        var samples = new[] { new SampleInfo(0, "a", "A", "Sample"), new SampleInfo(1, "b", "B", "Sample") };
        AlignmentSpotRow Spot(int id, string name, string cls, double h0, double h1) => new()
        {
            Id = id, Name = name, Ontology = cls,
            SamplePeaks = new[]
            {
                new AlignedSamplePeak(0, "a", "A", "Sample", 1, 0.9, 1.1, 700, h0, h0 * 10, 10, false),
                new AlignedSamplePeak(1, "b", "B", "Sample", 1, 0.9, 1.1, 700, h1, h1 * 10, 10, false),
            },
        };
        var confirmed = new[]
        {
            Spot(1, "PC 15:0-18:1(d7)", "PC", 1000, 2000),
            Spot(2, "PC 34:1", "PC", 500, 500),
            Spot(3, "PE 36:2", "PE", 300, 600),
        };
        var suggested = RelativeAbundance.Suggest(confirmed);
        Assert.Equal(1, suggested.First(s => s.Class == "PC").StandardFeatureId);
        Assert.Null(suggested.First(s => s.Class == "PE").StandardFeatureId);
        var (table, report) = RelativeAbundance.Build(confirmed, samples, suggested, useArea: true);
        Assert.Equal(2, table.FeatureCount);
        Assert.Equal(0.5, table.Values[0, 0], 9);     // PC 34:1 / standard in a
        Assert.Equal(0.25, table.Values[1, 0], 9);    // and in b, where the standard doubled
        Assert.Equal(3000, table.Values[0, 1], 9);    // PE keeps its raw area
        Assert.Contains("PE", report.ClassesWithoutStandard);
    }

    // ------------------------------------------------------------------ correlations, clustering, models

    [Fact]
    public void Correlations_and_the_pattern_search_agree_with_the_definitions()
    {
        var x = new[] { 1.0, 2, 3, 4, 5 };
        var y = new[] { 2.0, 4, 6, 8, 10 };
        var z = new[] { 5.0, 4, 3, 2, 1 };
        Assert.Equal(1, Correlations.Pearson(x, y), 10);
        Assert.Equal(-1, Correlations.Spearman(x, z), 10);
        Assert.Equal(-1, Correlations.Kendall(x, z), 10);
        Assert.Equal(1, Correlations.Kendall(x, y), 10);
        Assert.True(Correlations.PValue(0.9, 10) < 0.01);

        var samples = Enumerable.Range(0, 5).Select(i => new SampleInfo(i, "s" + i, i < 3 ? "A" : "B", "Sample")).ToList();
        var features = new[] { new AlignmentSpotRow { Id = 1, Name = "up" }, new AlignmentSpotRow { Id = 2, Name = "down" }, new AlignmentSpotRow { Id = 3, Name = "flat" } };
        var values = new double[5, 3];
        for (var i = 0; i < 5; i++) { values[i, 0] = i; values[i, 1] = -i; values[i, 2] = 1; }
        var table = new AnalysisTable(values, samples, features, "v");
        var hits = Correlations.PatternSearch(table, new[] { 1.0, 2, 3, 4, 5 }, CorrelationKind.Pearson);
        Assert.Equal("up", hits[0].Label);
        Assert.Equal(1, hits[0].Correlation, 10);
        Assert.Equal(-1, hits.First(h => h.Label == "down").Correlation, 10);
        Assert.DoesNotContain(hits, h => h.Label == "flat");
        var matrix = Correlations.BetweenFeatures(table, new[] { 0, 1 }, CorrelationKind.Pearson);
        Assert.Equal(-1, matrix.Values[0, 1], 10);
    }

    [Fact]
    public void Clustering_joins_the_alike_first_under_every_linkage()
    {
        var labels = new[] { "a", "b", "c", "d" };
        var groups = new[] { "x", "x", "y", "y" };
        var d = new double[4, 4];
        void Set(int i, int j, double v) { d[i, j] = d[j, i] = v; }
        Set(0, 1, 1); Set(2, 3, 1.5); Set(0, 2, 10); Set(0, 3, 10); Set(1, 2, 10); Set(1, 3, 10);
        foreach (var linkage in Enum.GetValues<LinkageKind>())
        {
            var root = Clustering.Cluster(d, labels, groups, linkage)!;
            var leaves = root.Leaves().Select(l => l.Label).ToList();
            Assert.True((leaves[0] == "a" && leaves[1] == "b") || (leaves[0] == "b" && leaves[1] == "a") || (leaves[2] == "a" && leaves[3] == "b") || (leaves[2] == "b" && leaves[3] == "a"), linkage.ToString());
            Assert.True(root.Height > root.Left!.Height && root.Height > root.Right!.Height, linkage.ToString());
        }
    }

    private static (AnalysisTable Table, DataMatrix Matrix) Separable(int perClass = 4, int features = 40, int seed = 5)
    {
        var rng = new Random(seed);
        var samples = new List<SampleInfo>();
        for (var i = 0; i < perClass * 2; i++) samples.Add(new SampleInfo(i, "s" + i, i < perClass ? "A" : "B", "Sample"));
        var rows = Enumerable.Range(0, features).Select(j => new AlignmentSpotRow { Id = j, Name = j % 4 == 0 ? "PC 3" + j + ":1" : "Unknown", Ontology = j % 4 == 0 ? "PC" : string.Empty }).ToList();
        var values = new double[samples.Count, features];
        for (var i = 0; i < samples.Count; i++)
            for (var j = 0; j < features; j++)
            {
                var shift = j % 4 == 0 ? (i < perClass ? 3 : 0) : 0;     // every fourth feature separates the classes
                values[i, j] = 10 + shift + rng.NextDouble() * 0.5;
            }
        var table = new AnalysisTable(values, samples, rows, "v");
        return (table, table.ToDataMatrix(ValueScaling.Auto));
    }

    [Fact]
    public void KMeans_finds_the_two_groups_and_the_forest_names_the_features_that_separate_them()
    {
        var (table, matrix) = Separable();
        var kmeans = Clustering.KMeans(matrix, 2);
        var clustersA = kmeans.Assignment.Take(4).Distinct().Count();
        var clustersB = kmeans.Assignment.Skip(4).Distinct().Count();
        Assert.Equal(1, clustersA);
        Assert.Equal(1, clustersB);
        Assert.NotEqual(kmeans.Assignment[0], kmeans.Assignment[4]);

        var forest = RandomForest.Compute(matrix, trees: 200);
        Assert.True(forest.OutOfBagError <= 0.25, $"out-of-bag error {forest.OutOfBagError}");
        var top = forest.Importance.Take(5).Select(i => i.FeatureId).ToList();
        Assert.All(top, id => Assert.Equal(0, id % 4));

        var heatmap = Clustering.Heatmap(table, Enumerable.Range(0, 12).ToList(), DistanceKind.Euclidean, LinkageKind.Average, standardizeRows: true, clusterColumns: true);
        Assert.Equal(12, heatmap.RowLabels.Count);
        Assert.Equal(8, heatmap.ColumnLabels.Count);
        // the columns cluster by class
        var order = heatmap.ColumnGroups;
        Assert.True(order.Take(4).Distinct().Count() == 1 && order.Skip(4).Distinct().Count() == 1, string.Join(",", order));
    }

    [Fact]
    public void Enrichment_finds_the_class_the_changed_features_come_from()
    {
        var features = new List<AlignmentSpotRow>();
        for (var j = 0; j < 40; j++)
        {
            var cls = j < 10 ? "PC" : j < 20 ? "PE" : j < 30 ? "TG" : "SM";
            features.Add(new AlignmentSpotRow { Id = j, Name = $"{cls} {30 + j % 10}:{j % 3}", Ontology = cls });
        }
        var significant = Enumerable.Range(0, 8).Concat(new[] { 15, 25 }).ToHashSet();   // eight of ten PCs plus two strays
        var result = Enrichment.OverRepresentation(features, significant);
        Assert.Equal("PC", result.Rows[0].Set);
        Assert.Equal("lipid class", result.Rows[0].Kind);
        Assert.True(result.Rows[0].P < 0.001, result.Rows[0].P.ToString());
        Assert.True(result.Rows[0].EnrichmentRatio > 3);
        Assert.Contains(result.Rows, r => r.Kind == "chain length");
    }

    // ------------------------------------------------------------------ helpers

    private static AnalysisTable Table(double[][] groups, out IReadOnlyList<SampleInfo> samples)
    {
        var list = new List<SampleInfo>();
        var values = new List<double>();
        for (var g = 0; g < groups.Length; g++)
            foreach (var v in groups[g]) { list.Add(new SampleInfo(list.Count, "s" + list.Count, "G" + g, "Sample")); values.Add(v); }
        samples = list;
        var matrix = new double[list.Count, 1];
        for (var i = 0; i < list.Count; i++) matrix[i, 0] = values[i];
        return new AnalysisTable(matrix, list, new[] { new AlignmentSpotRow { Id = 1, Name = "f" } }, "v");
    }
}
