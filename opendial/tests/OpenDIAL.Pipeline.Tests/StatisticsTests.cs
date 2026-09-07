using OpenDIAL.Pipeline.Results;
using OpenDIAL.Pipeline.Statistics;
using Xunit;

namespace OpenDIAL.Pipeline.Tests;

public class StatisticsTests
{
    private static IReadOnlyList<SampleInfo> Samples(params (string Name, string Class)[] entries) =>
        entries.Select((e, i) => new SampleInfo(i, e.Name, e.Class, "Sample")).ToList();

    private static AlignmentSpotRow Feature(int id, string name, string ontology, IReadOnlyList<SampleInfo> samples, params double[] heights) => new()
    {
        Id = id,
        Name = name,
        Ontology = ontology,
        Mz = 700 + id,
        Rt = 5 + id * 0.1,
        AverageHeight = heights.Average(),
        SamplePeaks = samples.Select((s, i) => new AlignedSamplePeak(s.FileId, s.FileName, s.Class, s.SampleType, 5, 4.9, 5.1, 700 + id, heights[i], heights[i] * 10, 10, false)).ToList(),
    };

    [Fact]
    public void Principal_components_separate_two_groups_that_differ()
    {
        var samples = Samples(("A1", "treated"), ("A2", "treated"), ("A3", "treated"), ("B1", "control"), ("B2", "control"), ("B3", "control"));
        var features = new List<AlignmentSpotRow>();
        var rng = new Random(7);
        double Jitter() => 0.95 + rng.NextDouble() * 0.1;
        for (var k = 0; k < 10; k++)
        {
            var level = 2000 * (k + 1);
            // ten features an order of magnitude higher in the treated group
            features.Add(Feature(k, "up " + k, "PC", samples,
                level * 10 * Jitter(), level * 10 * Jitter(), level * 10 * Jitter(),
                level * Jitter(), level * Jitter(), level * Jitter()));
            // ten that only wobble
            features.Add(Feature(100 + k, "flat " + k, "PE", samples,
                level * Jitter(), level * Jitter(), level * Jitter(),
                level * Jitter(), level * Jitter(), level * Jitter()));
        }

        var matrix = DataMatrix.Build(features, samples);
        var pca = Pca.Compute(matrix, 2);

        Assert.Equal(6, pca.Scores.Count);
        Assert.Equal(20, pca.Loadings.Count);
        Assert.True(pca.ExplainedVariance[0] > 50, $"the first component should carry the split, got {pca.ExplainedVariance[0]:F1} %");

        // the two groups must end up on opposite sides of the first component
        var treated = pca.Scores.Where(s => s.Class == "treated").Average(s => s.Components[0]);
        var control = pca.Scores.Where(s => s.Class == "control").Average(s => s.Components[0]);
        Assert.True(Math.Sign(treated) != Math.Sign(control), "the groups did not separate on PC1");

        // and the features that drive it are the ones that actually change
        var strongest = pca.Loadings.OrderByDescending(l => Math.Abs(l.Components[0])).Take(10).ToList();
        Assert.All(strongest, l => Assert.StartsWith("up ", l.Label));
    }

    [Fact]
    public void Clustering_puts_replicates_of_a_group_together()
    {
        var samples = Samples(("A1", "treated"), ("B1", "control"), ("A2", "treated"), ("B2", "control"));
        var features = new List<AlignmentSpotRow>();
        var rng = new Random(11);
        double Jitter() => 0.97 + rng.NextDouble() * 0.06;
        for (var k = 0; k < 24; k++)
        {
            var level = 1000 * (k + 1);
            // half the features favour one group and half the other, so the sample profiles differ
            var high = k % 2 == 0 ? level * 8 : level;
            var low = k % 2 == 0 ? level : level * 8;
            features.Add(Feature(k, "f" + k, "PC", samples, high * Jitter(), low * Jitter(), high * Jitter(), low * Jitter()));
        }

        var result = HierarchicalClustering.ClusterSamples(DataMatrix.Build(features, samples));

        Assert.NotNull(result.Root);
        var order = result.Root!.Leaves().Select(l => l.Group).ToList();
        Assert.Equal(4, order.Count);
        // the two members of a class must be adjacent in the dendrogram order
        Assert.Equal(order[0], order[1]);
        Assert.Equal(order[2], order[3]);
        Assert.NotEqual(order[0], order[2]);
    }

    [Fact]
    public void The_network_links_spectra_that_look_alike_and_leaves_the_others_apart()
    {
        var samples = Samples(("A1", "s"), ("A2", "s"));
        var a = Feature(1, "PC 34:1", "PC", samples, 1000, 1100);
        var b = Feature(2, "PC 36:2", "PC", samples, 900, 950);
        var c = Feature(3, "TG 52:2", "TG", samples, 800, 850);

        IReadOnlyList<SpectrumPeakPoint> Peaks(params (double Mz, double I)[] p) => p.Select(x => new SpectrumPeakPoint(x.Mz, x.I)).ToList();
        var spectra = new Dictionary<int, IReadOnlyList<SpectrumPeakPoint>>
        {
            [1] = Peaks((184.0733, 999), (496.34, 400), (524.37, 300), (86.09, 120)),
            [2] = Peaks((184.0733, 990), (496.34, 380), (524.37, 320), (86.09, 130)),
            [3] = Peaks((577.52, 999), (603.53, 500), (313.27, 250), (95.08, 100)),
        };

        var network = SpectralNetwork.Build(new[] { a, b, c }, spectra, cutoff: 0.7, tolerance: 0.05);

        // the TG fragments differently, so it joins nothing and is left out of the drawing
        Assert.Equal(2, network.Nodes.Count);
        Assert.Equal(3, network.Considered);
        Assert.Equal(1, network.Unconnected);
        Assert.DoesNotContain(network.Nodes, n => n.FeatureId == 3);
        Assert.Single(network.Edges);

        var withSingletons = SpectralNetwork.Build(new[] { a, b, c }, spectra, cutoff: 0.7, tolerance: 0.05, includeUnconnected: true);
        Assert.Equal(3, withSingletons.Nodes.Count);
        Assert.Single(withSingletons.Edges);
        var edge = network.Edges[0];
        Assert.Equal(1, Math.Min(edge.SourceId, edge.TargetId));
        Assert.Equal(2, Math.Max(edge.SourceId, edge.TargetId));
        Assert.True(edge.Similarity > 0.9);
    }

    [Fact]
    public void A_spectrum_is_identical_to_itself_and_unrelated_to_a_different_one()
    {
        IReadOnlyList<SpectrumPeakPoint> Peaks(params (double Mz, double I)[] p) => p.Select(x => new SpectrumPeakPoint(x.Mz, x.I)).ToList();
        var one = Peaks((100.0, 500), (200.0, 999), (300.0, 250));
        var other = Peaks((150.0, 500), (250.0, 999), (350.0, 250));
        Assert.True(SpectralNetwork.ModifiedCosine(one, one, 0.05) > 0.99);
        Assert.True(SpectralNetwork.ModifiedCosine(one, other, 0.05) < 0.05);
    }
}
