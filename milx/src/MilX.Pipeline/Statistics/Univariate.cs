using MilX.Pipeline.Results;

namespace MilX.Pipeline.Statistics;

/// <summary>One feature's comparison between two classes.</summary>
public sealed record FeatureComparison(
    int FeatureId,
    string Label,
    string Group,
    double MeanA,
    double MeanB,
    double FoldChange,
    double Log2FoldChange,
    double Statistic,
    double P,
    double AdjustedP)
{
    public double NegativeLog10P => P <= 0 ? 300 : -Math.Log10(P);
    public bool IsSignificant(double alpha, bool adjusted) => (adjusted ? AdjustedP : P) <= alpha;
}

public sealed record TwoGroupResult(
    string ClassA,
    string ClassB,
    IReadOnlyList<FeatureComparison> Features,
    string Test,
    string Message);

/// <summary>One feature's difference across every class.</summary>
public sealed record FeatureAnova(
    int FeatureId,
    string Label,
    string Group,
    double Statistic,
    double P,
    double AdjustedP,
    IReadOnlyList<PairwiseDifference> PostHoc)
{
    public double NegativeLog10P => P <= 0 ? 300 : -Math.Log10(P);
}

public sealed record PairwiseDifference(string ClassA, string ClassB, double Difference, double P);

public sealed record AnovaResult(IReadOnlyList<string> Classes, IReadOnlyList<FeatureAnova> Features, string Test, string Message);

/// <summary>
/// The tests that look at one feature at a time — fold change, the t-test and its rank
/// counterpart, the analysis of variance and its — with the correction for testing many of them.
/// </summary>
public static class Univariate
{
    /// <summary>The injections of each class, in table order.</summary>
    public static Dictionary<string, List<int>> GroupIndices(AnalysisTable table)
    {
        var groups = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < table.SampleCount; i++)
        {
            var cls = AnalysisTable.ClassOf(table.Samples[i]);
            if (!groups.TryGetValue(cls, out var list)) groups[cls] = list = new List<int>();
            list.Add(i);
        }
        return groups;
    }

    /// <summary>
    /// Compares two classes feature by feature. The fold change is the ratio of the class means on
    /// the <paramref name="linear"/> table (normalised, not transformed); the test runs on the
    /// <paramref name="transformed"/> one, where a log makes the spread comparable.
    /// </summary>
    public static TwoGroupResult Compare(
        AnalysisTable linear,
        AnalysisTable transformed,
        string classA,
        string classB,
        bool nonParametric = false,
        bool equalVariance = false,
        bool paired = false,
        PAdjustment adjustment = PAdjustment.FalseDiscoveryRate)
    {
        ArgumentNullException.ThrowIfNull(linear);
        ArgumentNullException.ThrowIfNull(transformed);
        var groups = GroupIndices(transformed);
        if (!groups.TryGetValue(classA, out var a) || !groups.TryGetValue(classB, out var b))
        {
            return new TwoGroupResult(classA, classB, Array.Empty<FeatureComparison>(), string.Empty, "Both classes have to exist in the batch.");
        }
        if (a.Count < 2 || b.Count < 2)
        {
            return new TwoGroupResult(classA, classB, Array.Empty<FeatureComparison>(), string.Empty,
                $"A test needs at least two injections in each class; {classA} has {a.Count} and {classB} has {b.Count}.");
        }
        if (paired && a.Count != b.Count)
        {
            return new TwoGroupResult(classA, classB, Array.Empty<FeatureComparison>(), string.Empty, "A paired test needs the same number of injections in both classes, in matching order.");
        }
        var p = transformed.FeatureCount;
        var raw = new double[p];
        var rows = new List<FeatureComparison>(p);
        for (var j = 0; j < p; j++)
        {
            var xa = a.Select(i => transformed.Values[i, j]).Where(v => !double.IsNaN(v)).ToArray();
            var xb = b.Select(i => transformed.Values[i, j]).Where(v => !double.IsNaN(v)).ToArray();
            var la = a.Select(i => linear.Values[i, j]).Where(v => !double.IsNaN(v)).ToArray();
            var lb = b.Select(i => linear.Values[i, j]).Where(v => !double.IsNaN(v)).ToArray();
            var meanA = la.Length == 0 ? double.NaN : la.Average();
            var meanB = lb.Length == 0 ? double.NaN : lb.Average();
            var fold = meanB > 0 && meanA >= 0 ? meanA / meanB : double.NaN;
            double statistic, pValue;
            if (xa.Length < 2 || xb.Length < 2)
            {
                statistic = double.NaN;
                pValue = double.NaN;
            }
            else if (nonParametric)
            {
                (statistic, pValue) = paired ? WilcoxonSignedRank(xa, xb) : MannWhitney(xa, xb);
            }
            else
            {
                (statistic, pValue) = paired ? PairedT(xa, xb) : TTest(xa, xb, equalVariance);
            }
            raw[j] = pValue;
            var feature = transformed.Features[j];
            rows.Add(new FeatureComparison(feature.Id, AnalysisTable.LabelOf(feature), Group(feature), meanA, meanB, fold,
                double.IsNaN(fold) || fold <= 0 ? double.NaN : Math.Log2(fold), statistic, pValue, double.NaN));
        }
        var adjusted = MultipleTesting.Adjust(raw, adjustment);
        for (var j = 0; j < p; j++) rows[j] = rows[j] with { AdjustedP = adjusted[j] };
        var test = paired
            ? (nonParametric ? "Wilcoxon signed-rank test" : "paired t-test")
            : (nonParametric ? "Mann–Whitney U test" : equalVariance ? "t-test, equal variances" : "Welch's t-test");
        var significant = rows.Count(r => r.AdjustedP <= 0.05);
        return new TwoGroupResult(classA, classB, rows, test,
            $"{classA} ({a.Count}) against {classB} ({b.Count}) over {p} features by the {test} · {significant} at {AdjustName(adjustment)} ≤ 0.05.");
    }

    public static string AdjustName(PAdjustment adjustment) => adjustment switch
    {
        PAdjustment.FalseDiscoveryRate => "FDR",
        PAdjustment.Bonferroni => "Bonferroni p",
        PAdjustment.Holm => "Holm p",
        _ => "raw p",
    };

    private static string Group(AlignmentSpotRow f) => string.IsNullOrWhiteSpace(f.Ontology) ? "(no class)" : f.Ontology;

    // ------------------------------------------------------------------ two groups

    /// <summary>Welch's t-test by default; the pooled-variance form when asked.</summary>
    public static (double T, double P) TTest(double[] a, double[] b, bool equalVariance)
    {
        var na = a.Length;
        var nb = b.Length;
        var ma = a.Average();
        var mb = b.Average();
        var va = Variance(a, ma);
        var vb = Variance(b, mb);
        double t, df;
        if (equalVariance)
        {
            var pooled = ((na - 1) * va + (nb - 1) * vb) / (na + nb - 2);
            var se = Math.Sqrt(pooled * (1.0 / na + 1.0 / nb));
            t = se > 0 ? (ma - mb) / se : 0;
            df = na + nb - 2;
        }
        else
        {
            var sa = va / na;
            var sb = vb / nb;
            var se = Math.Sqrt(sa + sb);
            t = se > 0 ? (ma - mb) / se : 0;
            var denominator = (na > 1 ? sa * sa / (na - 1) : 0) + (nb > 1 ? sb * sb / (nb - 1) : 0);
            df = denominator > 0 ? (sa + sb) * (sa + sb) / denominator : na + nb - 2;
        }
        if (t == 0 && va == 0 && vb == 0) return (0, ma == mb ? 1 : 0);
        return (t, Distributions.StudentTwoSided(t, df));
    }

    public static (double T, double P) PairedT(double[] a, double[] b)
    {
        var n = Math.Min(a.Length, b.Length);
        var d = new double[n];
        for (var i = 0; i < n; i++) d[i] = a[i] - b[i];
        var mean = d.Average();
        var sd = Math.Sqrt(Variance(d, mean));
        var t = sd > 0 ? mean / (sd / Math.Sqrt(n)) : 0;
        return (t, sd > 0 ? Distributions.StudentTwoSided(t, n - 1) : (mean == 0 ? 1 : 0));
    }

    /// <summary>
    /// The Mann–Whitney U test. Exact for small samples without ties, by counting the rank sums a
    /// null arrangement can reach; the normal approximation with a tie correction otherwise.
    /// </summary>
    public static (double U, double P) MannWhitney(double[] a, double[] b)
    {
        var na = a.Length;
        var nb = b.Length;
        var all = a.Select(v => (v, 0)).Concat(b.Select(v => (v, 1))).OrderBy(x => x.Item1).ToList();
        var ranks = Ranks(all.Select(x => x.Item1).ToList(), out var hasTies, out var tieTerm);
        double ra = 0;
        for (var i = 0; i < all.Count; i++) if (all[i].Item2 == 0) ra += ranks[i];
        var u = ra - na * (na + 1) / 2.0;
        var uOther = na * nb - u;
        var uMin = Math.Min(u, uOther);
        if (!hasTies && na + nb <= 20)
        {
            return (u, Math.Min(1, 2 * ExactMannWhitney(uMin, na, nb)));
        }
        var n = na + nb;
        var mean = na * nb / 2.0;
        var variance = na * nb / 12.0 * ((n + 1) - tieTerm / (n * (n - 1.0)));
        if (variance <= 0) return (u, 1);
        var z = (Math.Abs(u - mean) - 0.5) / Math.Sqrt(variance);
        return (u, Math.Min(1, 2 * (1 - Distributions.NormalCdf(Math.Max(0, z)))));
    }

    /// <summary>P(U ≤ u) under the null, by dynamic programming over the arrangements.</summary>
    private static double ExactMannWhitney(double u, int na, int nb)
    {
        var maxU = na * nb;
        // counts[i][j][k]: ways to arrange i of A among i+j items with U = k; rolled over i and j
        var table = new double[na + 1, nb + 1, maxU + 1];
        for (var j = 0; j <= nb; j++) table[0, j, 0] = 1;
        for (var i = 1; i <= na; i++)
        {
            table[i, 0, 0] = 1;
            for (var j = 1; j <= nb; j++)
            {
                for (var k = 0; k <= maxU; k++)
                {
                    // the last item is from A (contributes j to U) or from B
                    var fromA = k - j >= 0 ? table[i - 1, j, k - j] : 0;
                    var fromB = table[i, j - 1, k];
                    table[i, j, k] = fromA + fromB;
                }
            }
        }
        double total = 0;
        double below = 0;
        for (var k = 0; k <= maxU; k++)
        {
            total += table[na, nb, k];
            if (k <= u) below += table[na, nb, k];
        }
        return total > 0 ? below / total : 1;
    }

    /// <summary>The Wilcoxon signed-rank test on paired differences, normal approximation.</summary>
    public static (double W, double P) WilcoxonSignedRank(double[] a, double[] b)
    {
        var n = Math.Min(a.Length, b.Length);
        var d = Enumerable.Range(0, n).Select(i => a[i] - b[i]).Where(v => v != 0).ToList();
        if (d.Count == 0) return (0, 1);
        var ranks = Ranks(d.Select(Math.Abs).ToList(), out _, out var tieTerm);
        double wPlus = 0;
        for (var i = 0; i < d.Count; i++) if (d[i] > 0) wPlus += ranks[i];
        var m = d.Count;
        var mean = m * (m + 1) / 4.0;
        var variance = m * (m + 1) * (2 * m + 1) / 24.0 - tieTerm / 48.0;
        if (variance <= 0) return (wPlus, 1);
        var z = (Math.Abs(wPlus - mean) - 0.5) / Math.Sqrt(variance);
        return (wPlus, Math.Min(1, 2 * (1 - Distributions.NormalCdf(Math.Max(0, z)))));
    }

    // ------------------------------------------------------------------ several groups

    /// <summary>
    /// One-way analysis of variance across every class, with Fisher's least significant
    /// difference between each pair as the post-hoc test; or Kruskal–Wallis when asked for ranks.
    /// </summary>
    public static AnovaResult Anova(AnalysisTable transformed, bool nonParametric = false, PAdjustment adjustment = PAdjustment.FalseDiscoveryRate, double postHocAlpha = 0.05)
    {
        ArgumentNullException.ThrowIfNull(transformed);
        var groups = GroupIndices(transformed).Where(g => g.Value.Count >= 2).ToList();
        var classes = groups.Select(g => g.Key).ToList();
        if (classes.Count < 3)
        {
            return new AnovaResult(classes, Array.Empty<FeatureAnova>(), string.Empty,
                classes.Count == 2 ? "Two classes: use the two-group comparison." : "Needs at least three classes with two or more injections each.");
        }
        var p = transformed.FeatureCount;
        var raw = new double[p];
        var rows = new List<FeatureAnova>(p);
        for (var j = 0; j < p; j++)
        {
            var samples = groups.Select(g => g.Value.Select(i => transformed.Values[i, j]).Where(v => !double.IsNaN(v)).ToArray()).ToList();
            double statistic, pValue;
            var postHoc = new List<PairwiseDifference>();
            if (samples.Any(s => s.Length < 2))
            {
                statistic = double.NaN;
                pValue = double.NaN;
            }
            else if (nonParametric)
            {
                (statistic, pValue) = KruskalWallis(samples);
                if (pValue <= postHocAlpha)
                {
                    for (var x = 0; x < samples.Count; x++)
                        for (var y = x + 1; y < samples.Count; y++)
                        {
                            var (_, pair) = MannWhitney(samples[x], samples[y]);
                            postHoc.Add(new PairwiseDifference(classes[x], classes[y], Preprocessing.Median(samples[x]) - Preprocessing.Median(samples[y]), pair));
                        }
                }
            }
            else
            {
                var (f, pf, mse, dfError) = OneWay(samples);
                statistic = f;
                pValue = pf;
                if (pValue <= postHocAlpha)
                {
                    for (var x = 0; x < samples.Count; x++)
                        for (var y = x + 1; y < samples.Count; y++)
                        {
                            var difference = samples[x].Average() - samples[y].Average();
                            var se = Math.Sqrt(mse * (1.0 / samples[x].Length + 1.0 / samples[y].Length));
                            var t = se > 0 ? difference / se : 0;
                            postHoc.Add(new PairwiseDifference(classes[x], classes[y], difference, se > 0 ? Distributions.StudentTwoSided(t, dfError) : (difference == 0 ? 1 : 0)));
                        }
                }
            }
            raw[j] = pValue;
            var feature = transformed.Features[j];
            rows.Add(new FeatureAnova(feature.Id, AnalysisTable.LabelOf(feature), Group(feature), statistic, pValue, double.NaN, postHoc));
        }
        var adjusted = MultipleTesting.Adjust(raw, adjustment);
        for (var j = 0; j < p; j++) rows[j] = rows[j] with { AdjustedP = adjusted[j] };
        var test = nonParametric ? "Kruskal–Wallis test" : "one-way ANOVA with Fisher's LSD";
        var significant = rows.Count(r => r.AdjustedP <= 0.05);
        return new AnovaResult(classes, rows, test,
            $"{classes.Count} classes over {p} features by the {test} · {significant} at {AdjustName(adjustment)} ≤ 0.05.");
    }

    private static (double F, double P, double Mse, double DfError) OneWay(IReadOnlyList<double[]> samples)
    {
        var k = samples.Count;
        var n = samples.Sum(s => s.Length);
        var grand = samples.SelectMany(s => s).Average();
        double between = 0, within = 0;
        foreach (var s in samples)
        {
            var mean = s.Average();
            between += s.Length * (mean - grand) * (mean - grand);
            within += s.Sum(v => (v - mean) * (v - mean));
        }
        var dfBetween = k - 1;
        var dfWithin = n - k;
        var mse = dfWithin > 0 ? within / dfWithin : 0;
        var f = mse > 0 ? (between / dfBetween) / mse : (between > 0 ? double.PositiveInfinity : 0);
        var p = double.IsInfinity(f) ? 0 : Distributions.FisherUpper(f, dfBetween, dfWithin);
        return (f, p, mse, dfWithin);
    }

    public static (double H, double P) KruskalWallis(IReadOnlyList<double[]> samples)
    {
        var all = samples.SelectMany((s, g) => s.Select(v => (v, g))).OrderBy(x => x.v).ToList();
        var ranks = Ranks(all.Select(x => x.v).ToList(), out _, out var tieTerm);
        var n = all.Count;
        var sums = new double[samples.Count];
        for (var i = 0; i < n; i++) sums[all[i].g] += ranks[i];
        double h = 0;
        for (var g = 0; g < samples.Count; g++) h += sums[g] * sums[g] / samples[g].Length;
        h = 12.0 / (n * (n + 1)) * h - 3 * (n + 1);
        var correction = 1 - tieTerm / (Math.Pow(n, 3) - n);
        if (correction > 0) h /= correction;
        return (h, Distributions.ChiSquareUpper(h, samples.Count - 1));
    }

    // ------------------------------------------------------------------ helpers

    private static double Variance(double[] values, double mean) =>
        values.Length < 2 ? 0 : values.Sum(v => (v - mean) * (v - mean)) / (values.Length - 1);

    /// <summary>Mid-ranks of a sorted list, with the tie term Σ(t³ − t) for the corrections.</summary>
    private static double[] Ranks(IReadOnlyList<double> sorted, out bool hasTies, out double tieTerm)
    {
        var ranks = new double[sorted.Count];
        hasTies = false;
        tieTerm = 0;
        var i = 0;
        while (i < sorted.Count)
        {
            var j = i;
            while (j + 1 < sorted.Count && sorted[j + 1] == sorted[i]) j++;
            var rank = (i + j) / 2.0 + 1;
            for (var k = i; k <= j; k++) ranks[k] = rank;
            var t = j - i + 1;
            if (t > 1) { hasTies = true; tieTerm += (double)t * t * t - t; }
            i = j + 1;
        }
        return ranks;
    }
}
