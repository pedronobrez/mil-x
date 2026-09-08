namespace OpenDIAL.Pipeline.Statistics;

public enum CorrelationKind { Pearson, Spearman, Kendall }

/// <summary>A square correlation matrix over named items, in the order given.</summary>
public sealed record CorrelationMatrix(IReadOnlyList<string> Labels, IReadOnlyList<string> Groups, IReadOnlyList<int> FeatureIds, double[,] Values, CorrelationKind Kind);

/// <summary>One feature's correlation with a pattern or another feature.</summary>
public sealed record PatternMatch(int FeatureId, string Label, string Group, double Correlation, double P);

/// <summary>
/// Correlation between features, between injections, and between a feature and a pattern — the
/// heatmap and the pattern search of MetaboAnalyst.
/// </summary>
public static class Correlations
{
    public static double Pearson(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        var n = Math.Min(x.Count, y.Count);
        if (n < 2) return double.NaN;
        double mx = 0, my = 0;
        for (var i = 0; i < n; i++) { mx += x[i]; my += y[i]; }
        mx /= n; my /= n;
        double sxy = 0, sxx = 0, syy = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = x[i] - mx;
            var dy = y[i] - my;
            sxy += dx * dy; sxx += dx * dx; syy += dy * dy;
        }
        return sxx <= 0 || syy <= 0 ? double.NaN : sxy / Math.Sqrt(sxx * syy);
    }

    public static double Spearman(IReadOnlyList<double> x, IReadOnlyList<double> y) => Pearson(Rank(x), Rank(y));

    public static double Kendall(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        var n = Math.Min(x.Count, y.Count);
        if (n < 2) return double.NaN;
        double concordant = 0, discordant = 0, tiesX = 0, tiesY = 0;
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                var sx = Math.Sign(x[i] - x[j]);
                var sy = Math.Sign(y[i] - y[j]);
                if (sx == 0 && sy == 0) continue;
                if (sx == 0) { tiesX++; continue; }
                if (sy == 0) { tiesY++; continue; }
                if (sx == sy) concordant++; else discordant++;
            }
        }
        var denominator = Math.Sqrt((concordant + discordant + tiesX) * (concordant + discordant + tiesY));
        return denominator <= 0 ? double.NaN : (concordant - discordant) / denominator;
    }

    public static double Correlate(IReadOnlyList<double> x, IReadOnlyList<double> y, CorrelationKind kind) => kind switch
    {
        CorrelationKind.Spearman => Spearman(x, y),
        CorrelationKind.Kendall => Kendall(x, y),
        _ => Pearson(x, y),
    };

    /// <summary>The two-sided p-value of a correlation over n pairs, by the t transformation.</summary>
    public static double PValue(double r, int n)
    {
        if (double.IsNaN(r) || n < 3) return double.NaN;
        if (Math.Abs(r) >= 1) return 0;
        var t = r * Math.Sqrt((n - 2) / (1 - r * r));
        return Distributions.StudentTwoSided(t, n - 2);
    }

    /// <summary>Mid-ranks.</summary>
    public static double[] Rank(IReadOnlyList<double> values)
    {
        var order = Enumerable.Range(0, values.Count).OrderBy(i => values[i]).ToList();
        var ranks = new double[values.Count];
        var i = 0;
        while (i < order.Count)
        {
            var j = i;
            while (j + 1 < order.Count && values[order[j + 1]] == values[order[i]]) j++;
            var rank = (i + j) / 2.0 + 1;
            for (var k = i; k <= j; k++) ranks[order[k]] = rank;
            i = j + 1;
        }
        return ranks;
    }

    /// <summary>Correlations between the features given (by index), over the injections.</summary>
    public static CorrelationMatrix BetweenFeatures(AnalysisTable table, IReadOnlyList<int> features, CorrelationKind kind)
    {
        var columns = features.Select(j => Complete(table.Column(j))).ToList();
        var m = features.Count;
        var values = new double[m, m];
        for (var a = 0; a < m; a++)
        {
            values[a, a] = 1;
            for (var b = a + 1; b < m; b++)
            {
                var r = Correlate(columns[a], columns[b], kind);
                values[a, b] = values[b, a] = double.IsNaN(r) ? 0 : r;
            }
        }
        return new CorrelationMatrix(
            features.Select(j => AnalysisTable.LabelOf(table.Features[j])).ToList(),
            features.Select(j => string.IsNullOrWhiteSpace(table.Features[j].Ontology) ? "(no class)" : table.Features[j].Ontology).ToList(),
            features.Select(j => table.Features[j].Id).ToList(),
            values, kind);
    }

    /// <summary>Correlations between the injections, over the features.</summary>
    public static CorrelationMatrix BetweenSamples(AnalysisTable table, CorrelationKind kind)
    {
        var n = table.SampleCount;
        var rows = Enumerable.Range(0, n).Select(i => Complete(table.Row(i))).ToList();
        var values = new double[n, n];
        for (var a = 0; a < n; a++)
        {
            values[a, a] = 1;
            for (var b = a + 1; b < n; b++)
            {
                var r = Correlate(rows[a], rows[b], kind);
                values[a, b] = values[b, a] = double.IsNaN(r) ? 0 : r;
            }
        }
        return new CorrelationMatrix(
            table.Samples.Select(s => s.FileName).ToList(),
            table.Samples.Select(AnalysisTable.ClassOf).ToList(),
            table.Samples.Select(s => s.FileId).ToList(),
            values, kind);
    }

    /// <summary>
    /// Every feature's correlation with a pattern over the injections — a feature's own profile, or
    /// a number per class such as 1, 2, 3 for a trend — best first.
    /// </summary>
    public static IReadOnlyList<PatternMatch> PatternSearch(AnalysisTable table, IReadOnlyList<double> pattern, CorrelationKind kind, int? excludeFeatureId = null)
    {
        var results = new List<PatternMatch>(table.FeatureCount);
        for (var j = 0; j < table.FeatureCount; j++)
        {
            var feature = table.Features[j];
            if (feature.Id == excludeFeatureId) continue;
            var column = table.Column(j);
            var xs = new List<double>();
            var ys = new List<double>();
            for (var i = 0; i < table.SampleCount && i < pattern.Count; i++)
            {
                if (double.IsNaN(column[i]) || double.IsNaN(pattern[i])) continue;
                xs.Add(column[i]);
                ys.Add(pattern[i]);
            }
            var r = Correlate(xs, ys, kind);
            if (double.IsNaN(r)) continue;
            results.Add(new PatternMatch(feature.Id, AnalysisTable.LabelOf(feature), string.IsNullOrWhiteSpace(feature.Ontology) ? "(no class)" : feature.Ontology, r, PValue(r, xs.Count)));
        }
        return results.OrderByDescending(m => Math.Abs(m.Correlation)).ToList();
    }

    /// <summary>The pattern a class order defines: each injection takes the number of its class.</summary>
    public static double[] ClassPattern(AnalysisTable table, IReadOnlyList<string> orderedClasses)
    {
        var pattern = new double[table.SampleCount];
        for (var i = 0; i < table.SampleCount; i++)
        {
            var index = orderedClasses.ToList().FindIndex(c => string.Equals(c, AnalysisTable.ClassOf(table.Samples[i]), StringComparison.Ordinal));
            pattern[i] = index < 0 ? double.NaN : index + 1;
        }
        return pattern;
    }

    /// <summary>A missing value made the feature's mean, so it neither pulls nor pushes a correlation.</summary>
    private static double[] Complete(double[] values)
    {
        var present = values.Where(v => !double.IsNaN(v)).ToList();
        var mean = present.Count == 0 ? 0 : present.Average();
        return values.Select(v => double.IsNaN(v) ? mean : v).ToArray();
    }
}
