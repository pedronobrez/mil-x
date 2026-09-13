namespace OpenDIAL.Pipeline.Statistics;

public enum DistanceKind { Euclidean, Pearson, Spearman, Manhattan }

public enum LinkageKind { Average, Complete, Single, Ward }

/// <summary>A clustered heatmap: the matrix reordered, with the trees on both sides.</summary>
public sealed record HeatmapResult(
    IReadOnlyList<string> RowLabels,
    IReadOnlyList<string> RowGroups,
    IReadOnlyList<int> RowFeatureIds,
    IReadOnlyList<string> ColumnLabels,
    IReadOnlyList<string> ColumnGroups,
    double[,] Values,
    ClusterNode? RowTree,
    ClusterNode? ColumnTree,
    string ValueName,
    // the same cells before the rows were standardised, and what those numbers are. A standardised
    // row says only how a cell compares with the rest of its own row: a feature that is noise
    // everywhere still has a reddest cell, and without the value behind it the picture invites the
    // reader to believe it means abundance.
    double[,]? Unstandardized = null,
    string? UnstandardizedName = null);

/// <summary>One k-means solution: which cluster each injection fell in, and how compact it is.</summary>
public sealed record KMeansResult(int K, IReadOnlyList<int> Assignment, IReadOnlyList<string> Labels, IReadOnlyList<string> Groups, double WithinSumOfSquares);

/// <summary>
/// Hierarchical clustering over any distance and linkage — the general form of the sample
/// dendrogram — and the clustered heatmap and k-means that lean on it.
/// </summary>
public static class Clustering
{
    /// <summary>The distance between two profiles, with a missing value made the profile's mean.</summary>
    public static double Distance(IReadOnlyList<double> a, IReadOnlyList<double> b, DistanceKind kind)
    {
        switch (kind)
        {
            case DistanceKind.Pearson:
            {
                var r = Correlations.Pearson(a, b);
                return double.IsNaN(r) ? 1 : 1 - r;
            }
            case DistanceKind.Spearman:
            {
                var r = Correlations.Spearman(a, b);
                return double.IsNaN(r) ? 1 : 1 - r;
            }
            case DistanceKind.Manhattan:
            {
                double sum = 0;
                for (var i = 0; i < Math.Min(a.Count, b.Count); i++) sum += Math.Abs(a[i] - b[i]);
                return sum;
            }
            default:
            {
                double sum = 0;
                for (var i = 0; i < Math.Min(a.Count, b.Count); i++) sum += (a[i] - b[i]) * (a[i] - b[i]);
                return Math.Sqrt(sum);
            }
        }
    }

    /// <summary>Agglomerative clustering by the Lance–Williams update for the four linkages.</summary>
    public static ClusterNode? Cluster(double[,] distances, IReadOnlyList<string> labels, IReadOnlyList<string> groups, LinkageKind linkage)
    {
        var n = labels.Count;
        if (n == 0) return null;
        if (n == 1) return new ClusterNode(0, labels[0], groups[0]);
        var d = (double[,])distances.Clone();
        if (linkage == LinkageKind.Ward)
        {
            // Ward works on squared distances
            for (var i = 0; i < n; i++) for (var j = 0; j < n; j++) d[i, j] *= d[i, j];
        }
        var active = Enumerable.Range(0, n).ToList();
        var nodes = active.ToDictionary(i => i, i => new ClusterNode(i, labels[i], groups[i]));
        var size = active.ToDictionary(i => i, _ => 1);
        var index = active.ToDictionary(i => i, i => i);   // node id → row of d
        var next = n;
        while (active.Count > 1)
        {
            var bestA = -1;
            var bestB = -1;
            var best = double.MaxValue;
            for (var x = 0; x < active.Count; x++)
            {
                for (var y = x + 1; y < active.Count; y++)
                {
                    var v = d[index[active[x]], index[active[y]]];
                    if (v < best) { best = v; bestA = active[x]; bestB = active[y]; }
                }
            }
            var ia = index[bestA];
            var ib = index[bestB];
            var na = size[bestA];
            var nb = size[bestB];
            foreach (var other in active)
            {
                if (other == bestA || other == bestB) continue;
                var io = index[other];
                var no = size[other];
                var dao = d[ia, io];
                var dbo = d[ib, io];
                var merged = linkage switch
                {
                    LinkageKind.Single => Math.Min(dao, dbo),
                    LinkageKind.Complete => Math.Max(dao, dbo),
                    LinkageKind.Ward => ((na + no) * dao + (nb + no) * dbo - no * best) / (na + nb + no),
                    _ => (na * dao + nb * dbo) / (na + nb),
                };
                d[ia, io] = d[io, ia] = merged;
            }
            var height = linkage == LinkageKind.Ward ? Math.Sqrt(Math.Max(0, best)) : best;
            var node = new ClusterNode(nodes[bestA], nodes[bestB], height);
            var id = next++;
            nodes[id] = node;
            size[id] = na + nb;
            index[id] = ia;
            active.Remove(bestA);
            active.Remove(bestB);
            nodes.Remove(bestA);
            nodes.Remove(bestB);
            active.Add(id);
        }
        return nodes[active[0]];
    }

    /// <summary>
    /// The heatmap: the features given (rows) against the injections (columns), each feature
    /// standardised to zero mean and unit variance across the injections when asked, clustered on
    /// both sides and reordered by the trees.
    /// </summary>
    public static HeatmapResult Heatmap(AnalysisTable table, IReadOnlyList<int> features, DistanceKind distance, LinkageKind linkage, bool standardizeRows, bool clusterColumns)
    {
        var n = table.SampleCount;
        var m = features.Count;
        var values = new double[m, n];
        var plain = new double[m, n];
        for (var r = 0; r < m; r++)
        {
            var column = table.Column(features[r]);
            var present = column.Where(v => !double.IsNaN(v)).ToList();
            var mean = present.Count == 0 ? 0 : present.Average();
            var sd = present.Count > 1 ? Math.Sqrt(present.Sum(v => (v - mean) * (v - mean)) / (present.Count - 1)) : 0;
            for (var c = 0; c < n; c++)
            {
                var v = double.IsNaN(column[c]) ? mean : column[c];
                plain[r, c] = v;
                values[r, c] = standardizeRows ? (sd > 0 ? (v - mean) / sd : 0) : v;
            }
        }
        var rowLabels = features.Select(j => AnalysisTable.LabelOf(table.Features[j])).ToList();
        var rowGroups = features.Select(j => string.IsNullOrWhiteSpace(table.Features[j].Ontology) ? "(no class)" : table.Features[j].Ontology).ToList();
        var rowIds = features.Select(j => table.Features[j].Id).ToList();
        var columnLabels = table.Samples.Select(s => s.FileName).ToList();
        var columnGroups = table.Samples.Select(AnalysisTable.ClassOf).ToList();

        ClusterNode? rowTree = null;
        var rowOrder = Enumerable.Range(0, m).ToList();
        if (m > 1)
        {
            var d = new double[m, m];
            for (var a = 0; a < m; a++)
                for (var b = a + 1; b < m; b++)
                    d[a, b] = d[b, a] = Distance(Row(values, a), Row(values, b), distance);
            rowTree = Cluster(d, rowLabels, rowGroups, linkage);
            rowOrder = rowTree!.Leaves().Select(l => l.Index).ToList();
        }
        ClusterNode? columnTree = null;
        var columnOrder = Enumerable.Range(0, n).ToList();
        if (clusterColumns && n > 1)
        {
            var d = new double[n, n];
            for (var a = 0; a < n; a++)
                for (var b = a + 1; b < n; b++)
                    d[a, b] = d[b, a] = Distance(Column(values, a), Column(values, b), distance);
            columnTree = Cluster(d, columnLabels, columnGroups, linkage);
            columnOrder = columnTree!.Leaves().Select(l => l.Index).ToList();
        }
        var ordered = new double[m, n];
        var orderedPlain = new double[m, n];
        for (var r = 0; r < m; r++)
            for (var c = 0; c < n; c++)
            {
                ordered[r, c] = values[rowOrder[r], columnOrder[c]];
                orderedPlain[r, c] = plain[rowOrder[r], columnOrder[c]];
            }
        return new HeatmapResult(
            rowOrder.Select(r => rowLabels[r]).ToList(),
            rowOrder.Select(r => rowGroups[r]).ToList(),
            rowOrder.Select(r => rowIds[r]).ToList(),
            columnOrder.Select(c => columnLabels[c]).ToList(),
            columnOrder.Select(c => columnGroups[c]).ToList(),
            ordered, rowTree, columnTree,
            standardizeRows ? "z-score of " + table.ValueName : table.ValueName,
            standardizeRows ? orderedPlain : null,
            standardizeRows ? table.ValueName : null);
    }

    /// <summary>
    /// K-means over the injections on the scaled matrix, k-means++ seeded, the best of several
    /// restarts by within-cluster sum of squares.
    /// </summary>
    public static KMeansResult KMeans(DataMatrix matrix, int k, int restarts = 20, int seed = 20260907)
    {
        var n = matrix.SampleCount;
        var p = matrix.FeatureCount;
        k = Math.Clamp(k, 1, Math.Max(1, n));
        var rows = Enumerable.Range(0, n).Select(i => Row(matrix.Values, i)).ToList();
        var rng = new Random(seed);
        int[]? bestAssignment = null;
        var bestWss = double.MaxValue;
        for (var restart = 0; restart < restarts; restart++)
        {
            var centres = new List<double[]> { (double[])rows[rng.Next(n)].Clone() };
            while (centres.Count < k)
            {
                var weights = rows.Select(r => centres.Min(c => Squared(r, c))).ToArray();
                var total = weights.Sum();
                var pick = rng.NextDouble() * total;
                var chosen = 0;
                for (var i = 0; i < n; i++) { pick -= weights[i]; if (pick <= 0) { chosen = i; break; } }
                centres.Add((double[])rows[chosen].Clone());
            }
            var assignment = new int[n];
            for (var iteration = 0; iteration < 100; iteration++)
            {
                var changed = false;
                for (var i = 0; i < n; i++)
                {
                    var nearest = 0;
                    var nearestD = double.MaxValue;
                    for (var c = 0; c < k; c++)
                    {
                        var dist = Squared(rows[i], centres[c]);
                        if (dist < nearestD) { nearestD = dist; nearest = c; }
                    }
                    if (assignment[i] != nearest) { assignment[i] = nearest; changed = true; }
                }
                for (var c = 0; c < k; c++)
                {
                    var members = Enumerable.Range(0, n).Where(i => assignment[i] == c).ToList();
                    if (members.Count == 0) continue;
                    for (var j = 0; j < p; j++) centres[c][j] = members.Average(i => rows[i][j]);
                }
                if (!changed) break;
            }
            var wss = Enumerable.Range(0, n).Sum(i => Squared(rows[i], centres[assignment[i]]));
            if (wss < bestWss) { bestWss = wss; bestAssignment = assignment; }
        }
        return new KMeansResult(k, bestAssignment ?? new int[n], matrix.Samples.Select(s => s.FileName).ToList(),
            matrix.Samples.Select(AnalysisTable.ClassOf).ToList(), bestWss);
    }

    private static double Squared(double[] a, double[] b)
    {
        double s = 0;
        for (var i = 0; i < a.Length; i++) s += (a[i] - b[i]) * (a[i] - b[i]);
        return s;
    }

    private static double[] Row(double[,] m, int r)
    {
        var row = new double[m.GetLength(1)];
        for (var c = 0; c < row.Length; c++) row[c] = m[r, c];
        return row;
    }

    private static double[] Column(double[,] m, int c)
    {
        var column = new double[m.GetLength(0)];
        for (var r = 0; r < column.Length; r++) column[r] = m[r, c];
        return column;
    }
}
