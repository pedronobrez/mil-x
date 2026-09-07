using CompMs.MsdialCore.MSDec;
using OpenDIAL.Pipeline.Results;

namespace OpenDIAL.Pipeline.Statistics;

public sealed record NetworkNode(int FeatureId, string Label, string Group, double Rt, double Mz, double Height, bool IsAnnotated);

public sealed record NetworkEdge(int SourceId, int TargetId, double Similarity, double MassDifference);

public sealed record SpectralNetworkResult(IReadOnlyList<NetworkNode> Nodes, IReadOnlyList<NetworkEdge> Edges, int Considered, int Unconnected);

/// <summary>
/// Links features whose product spectra look alike. Members of one lipid class fragment the same
/// way, so they cluster; a feature annotated as one class but sitting inside another class's
/// cluster is a misannotation worth a second look, and an unknown next to a named cluster is a
/// candidate for the same family.
/// </summary>
public static class SpectralNetwork
{
    /// <summary>
    /// The similarity MS-DIAL and GNPS both use for this: a cosine over matched fragments, where a
    /// fragment matches when the two masses agree within the tolerance, weighted by intensity.
    /// </summary>
    public static double ModifiedCosine(IReadOnlyList<SpectrumPeakPoint> a, IReadOnlyList<SpectrumPeakPoint> b, double tolerance)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var maxA = a.Max(p => p.Intensity);
        var maxB = b.Max(p => p.Intensity);
        if (maxA <= 0 || maxB <= 0) return 0;

        double dot = 0, normA = 0, normB = 0;
        var usedB = new bool[b.Count];
        foreach (var pa in a)
        {
            var wa = Math.Sqrt(pa.Intensity / maxA);
            normA += wa * wa;
            var best = -1;
            var bestDiff = tolerance;
            for (var i = 0; i < b.Count; i++)
            {
                if (usedB[i]) continue;
                var diff = Math.Abs(b[i].Mz - pa.Mz);
                if (diff <= bestDiff) { bestDiff = diff; best = i; }
            }
            if (best < 0) continue;
            usedB[best] = true;
            dot += wa * Math.Sqrt(b[best].Intensity / maxB);
        }
        foreach (var pb in b) normB += pb.Intensity / maxB;
        if (normA <= 0 || normB <= 0) return 0;
        return dot * dot / (normA * normB);
    }

    /// <summary>
    /// Builds the network over the given features. Spectra come from the caller because reading them
    /// is the slow part and the caller already has them cached.
    /// </summary>
    public static SpectralNetworkResult Build(
        IReadOnlyList<AlignmentSpotRow> features,
        IReadOnlyDictionary<int, IReadOnlyList<SpectrumPeakPoint>> spectra,
        double cutoff = 0.7,
        double tolerance = 0.05,
        int minMatchedPeaks = 3,
        bool includeUnconnected = false)
    {
        ArgumentNullException.ThrowIfNull(features);
        ArgumentNullException.ThrowIfNull(spectra);
        var usable = features.Where(f => spectra.TryGetValue(f.Id, out var s) && s.Count >= minMatchedPeaks).ToList();
        var nodes = usable
            .Select(f => new NetworkNode(
                f.Id,
                f.IsAnnotated ? f.Name : $"m/z {f.Mz:F4}",
                string.IsNullOrWhiteSpace(f.Ontology) ? (f.IsAnnotated ? "(no class)" : "unknown") : f.Ontology,
                f.Rt, f.Mz, f.AverageHeight, f.IsAnnotated))
            .ToList();

        var edges = new List<NetworkEdge>();
        for (var i = 0; i < usable.Count; i++)
        {
            var a = spectra[usable[i].Id];
            for (var j = i + 1; j < usable.Count; j++)
            {
                var b = spectra[usable[j].Id];
                var similarity = ModifiedCosine(a, b, tolerance);
                if (similarity < cutoff) continue;
                edges.Add(new NetworkEdge(usable[i].Id, usable[j].Id, similarity, Math.Abs(usable[i].Mz - usable[j].Mz)));
            }
        }

        // A feature joined to nothing says nothing in a network view, and with a few thousand of them
        // the layout is all singletons and no clusters. They are counted, not drawn.
        var linked = new HashSet<int>(edges.SelectMany(e => new[] { e.SourceId, e.TargetId }));
        var unconnected = nodes.Count(n => !linked.Contains(n.FeatureId));
        var shown = includeUnconnected ? nodes : nodes.Where(n => linked.Contains(n.FeatureId)).ToList();
        return new SpectralNetworkResult(shown, edges, nodes.Count, unconnected);
    }

    /// <summary>Cytoscape reads these two files directly.</summary>
    public static async Task ExportAsync(string edgePath, string nodePath, SpectralNetworkResult network)
    {
        ArgumentNullException.ThrowIfNull(network);
        var edges = new List<string> { "source\ttarget\tsimilarity\tmass_difference" };
        edges.AddRange(network.Edges.Select(e =>
            $"{e.SourceId}\t{e.TargetId}\t{e.Similarity.ToString("F4", System.Globalization.CultureInfo.InvariantCulture)}\t{e.MassDifference.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)}"));
        await File.WriteAllLinesAsync(edgePath, edges).ConfigureAwait(false);

        var nodes = new List<string> { "id\tlabel\tclass\trt\tmz\theight\tannotated" };
        nodes.AddRange(network.Nodes.Select(n =>
            $"{n.FeatureId}\t{n.Label}\t{n.Group}\t{n.Rt.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)}\t{n.Mz.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)}\t{n.Height.ToString("F0", System.Globalization.CultureInfo.InvariantCulture)}\t{n.IsAnnotated}"));
        await File.WriteAllLinesAsync(nodePath, nodes).ConfigureAwait(false);
    }
}
