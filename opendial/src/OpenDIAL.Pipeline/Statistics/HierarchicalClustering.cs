namespace OpenDIAL.Pipeline.Statistics;

/// <summary>A node of the dendrogram: a leaf when <see cref="Left"/> is null.</summary>
public sealed class ClusterNode
{
    public ClusterNode(int index, string label, string group) { Index = index; Label = label; Group = group; }
    public ClusterNode(ClusterNode left, ClusterNode right, double height)
    {
        Left = left;
        Right = right;
        Height = height;
        Index = -1;
        Label = string.Empty;
        Group = string.Empty;
    }

    public int Index { get; }
    public string Label { get; }
    public string Group { get; }
    public ClusterNode? Left { get; }
    public ClusterNode? Right { get; }
    public double Height { get; }
    public bool IsLeaf => Left is null;

    /// <summary>Leaves left to right, which is the order the dendrogram is drawn in.</summary>
    public IEnumerable<ClusterNode> Leaves()
    {
        if (IsLeaf) { yield return this; yield break; }
        foreach (var leaf in Left!.Leaves()) yield return leaf;
        foreach (var leaf in Right!.Leaves()) yield return leaf;
    }
}

public sealed record ClusteringResult(ClusterNode? Root, double[,] Distances, IReadOnlyList<string> Labels, IReadOnlyList<string> Groups);

/// <summary>
/// Groups the injections by how alike their abundance profiles are, with average linkage over one
/// minus the Pearson correlation. Replicates of one class should sit together and the blanks apart;
/// an injection that lands with the wrong group is the first sign of a mix-up or a bad run.
/// </summary>
public static class HierarchicalClustering
{
    public static ClusteringResult ClusterSamples(DataMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        var n = matrix.SampleCount;
        var labels = matrix.Samples.Select(s => s.FileName).ToList();
        var groups = matrix.Samples.Select(s => string.IsNullOrEmpty(s.Class) ? "(none)" : s.Class).ToList();
        var distances = new double[n, n];
        for (var a = 0; a < n; a++)
        {
            for (var b = a + 1; b < n; b++)
            {
                var d = 1.0 - Correlation(matrix, a, b);
                distances[a, b] = distances[b, a] = d;
            }
        }
        if (n == 0) return new ClusteringResult(null, distances, labels, groups);
        if (n == 1) return new ClusteringResult(new ClusterNode(0, labels[0], groups[0]), distances, labels, groups);

        var active = Enumerable.Range(0, n).ToList();
        var nodes = active.ToDictionary(i => i, i => new ClusterNode(i, labels[i], groups[i]));
        var members = active.ToDictionary(i => i, i => new List<int> { i });
        var next = n;

        while (active.Count > 1)
        {
            var bestA = active[0];
            var bestB = active[1];
            var best = double.MaxValue;
            foreach (var a in active)
            {
                foreach (var b in active)
                {
                    if (b <= a) continue;
                    var d = Average(distances, members[a], members[b]);
                    if (d < best) { best = d; bestA = a; bestB = b; }
                }
            }
            var merged = new ClusterNode(nodes[bestA], nodes[bestB], best);
            var id = next++;
            nodes[id] = merged;
            members[id] = members[bestA].Concat(members[bestB]).ToList();
            active.Remove(bestA);
            active.Remove(bestB);
            nodes.Remove(bestA);
            nodes.Remove(bestB);
            active.Add(id);
        }
        return new ClusteringResult(nodes[active[0]], distances, labels, groups);
    }

    private static double Average(double[,] distances, List<int> left, List<int> right)
    {
        double sum = 0;
        foreach (var a in left)
        {
            foreach (var b in right) sum += distances[a, b];
        }
        return sum / (left.Count * right.Count);
    }

    /// <summary>Pearson correlation of two rows; the matrix is already centred per feature.</summary>
    private static double Correlation(DataMatrix matrix, int a, int b)
    {
        var p = matrix.FeatureCount;
        if (p == 0) return 1;
        double sa = 0, sb = 0;
        for (var j = 0; j < p; j++) { sa += matrix.Values[a, j]; sb += matrix.Values[b, j]; }
        var ma = sa / p;
        var mb = sb / p;
        double cov = 0, va = 0, vb = 0;
        for (var j = 0; j < p; j++)
        {
            var da = matrix.Values[a, j] - ma;
            var db = matrix.Values[b, j] - mb;
            cov += da * db;
            va += da * da;
            vb += db * db;
        }
        if (va <= 1e-18 || vb <= 1e-18) return 0;
        return cov / Math.Sqrt(va * vb);
    }
}
