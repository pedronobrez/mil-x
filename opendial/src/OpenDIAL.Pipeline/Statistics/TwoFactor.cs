using OpenDIAL.Pipeline.Results;

namespace OpenDIAL.Pipeline.Statistics;

/// <summary>One feature under two factors: the F and p of each main effect and of their interaction.</summary>
public sealed record TwoWayFeature(
    int FeatureId,
    string Label,
    string Group,
    double FA, double PA, double AdjustedPA,
    double FB, double PB, double AdjustedPB,
    double FAB, double PAB, double AdjustedPAB,
    IReadOnlyList<double> CellMeans)
{
    /// <summary>The smallest adjusted p over the effects tested, for ranking.</summary>
    public double BestAdjustedP => new[] { AdjustedPA, AdjustedPB, AdjustedPAB }.Where(p => !double.IsNaN(p)).DefaultIfEmpty(double.NaN).Min();
    public string Significant(double alpha) => string.Join(" + ", new[] { (AdjustedPA, "A"), (AdjustedPB, "B"), (AdjustedPAB, "A×B") }.Where(e => !double.IsNaN(e.Item1) && e.Item1 <= alpha).Select(e => e.Item2));
}

public sealed record TwoWayResult(
    string NameA,
    string NameB,
    IReadOnlyList<string> LevelsA,
    IReadOnlyList<string> LevelsB,
    bool Interaction,
    IReadOnlyList<TwoWayFeature> Features,
    string Message);

/// <summary>Where one injection lands on the first two components of an ASCA effect, with its residual added back.</summary>
public sealed record AscaSample(int FileId, string Sample, string LevelA, string LevelB, double Pc1, double Pc2);

/// <summary>One effect of the ASCA decomposition: how much of the variation it holds, whether that is more than chance, and its components.</summary>
public sealed record AscaEffect(string Name, double PercentOfVariation, double PermutationP, IReadOnlyList<double> Explained, IReadOnlyList<AscaSample> Scores, IReadOnlyList<PcaLoading> Loadings)
{
    /// <summary>
    /// A two-level factor makes a rank-one effect: it has one component and no second. The scores
    /// then take the residual's own first component as the vertical axis, so the spread of the
    /// replicates is still seen against the separation.
    /// </summary>
    public bool SecondAxisIsResidual { get; init; }
    public string SecondAxisLabel => SecondAxisIsResidual ? "PC1 of the residual" : "PC2 of the effect";
}

public sealed record AscaResult(IReadOnlyList<AscaEffect> Effects, double ResidualPercent, int Permutations, string Message);

/// <summary>
/// Two factors at once — treatment and time, genotype and diet, class and batch. The two-way
/// analysis of variance says, one feature at a time, which factor moved it and whether the two
/// interact; ASCA (ANOVA-simultaneous component analysis) partitions the whole matrix into the
/// part each factor explains and looks at each part with a principal-component model, so the
/// dataset's answer to the design is one picture per factor rather than a thousand p-values.
/// </summary>
public static class TwoFactor
{
    /// <summary>
    /// The two-way ANOVA per feature, on the transformed values, with type-II sums of squares by
    /// model comparison — so unbalanced designs and empty cells are handled the way R's car::Anova
    /// handles them — and the p of each effect adjusted across the features.
    /// </summary>
    public static TwoWayResult Anova(AnalysisTable transformed, IReadOnlyList<string> factorA, IReadOnlyList<string> factorB, string nameA, string nameB,
        bool interaction = true, PAdjustment adjustment = PAdjustment.FalseDiscoveryRate)
    {
        ArgumentNullException.ThrowIfNull(transformed);
        if (factorA.Count != transformed.SampleCount || factorB.Count != transformed.SampleCount) throw new ArgumentException("one level per injection");
        var levelsA = factorA.Where(l => !string.IsNullOrEmpty(l)).Distinct().OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToList();
        var levelsB = factorB.Where(l => !string.IsNullOrEmpty(l)).Distinct().OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToList();
        if (levelsA.Count < 2 || levelsB.Count < 2)
        {
            return new TwoWayResult(nameA, nameB, levelsA, levelsB, interaction, Array.Empty<TwoWayFeature>(),
                $"Each factor needs at least two levels; {nameA} has {levelsA.Count} and {nameB} has {levelsB.Count}.");
        }
        var rows = Enumerable.Range(0, transformed.SampleCount).Where(i => !string.IsNullOrEmpty(factorA[i]) && !string.IsNullOrEmpty(factorB[i])).ToList();
        var cells = new int[levelsA.Count, levelsB.Count];
        foreach (var i in rows) cells[levelsA.IndexOf(factorA[i]), levelsB.IndexOf(factorB[i])]++;
        var filled = 0;
        for (var a = 0; a < levelsA.Count; a++) for (var b = 0; b < levelsB.Count; b++) if (cells[a, b] > 0) filled++;
        var replicated = 0;
        for (var a = 0; a < levelsA.Count; a++) for (var b = 0; b < levelsB.Count; b++) if (cells[a, b] > 1) replicated++;
        if (interaction && replicated == 0)
        {
            // no cell has a replicate: the interaction and the error are the same thing; test the main effects only
            interaction = false;
        }

        var p = transformed.FeatureCount;
        var features = new List<TwoWayFeature>(p);
        var rawA = new double[p];
        var rawB = new double[p];
        var rawAB = new double[p];
        for (var j = 0; j < p; j++)
        {
            var kept = rows.Where(i => !double.IsNaN(transformed.Values[i, j])).ToList();
            var y = kept.Select(i => transformed.Values[i, j]).ToArray();
            var a = kept.Select(i => levelsA.IndexOf(factorA[i])).ToArray();
            var b = kept.Select(i => levelsB.IndexOf(factorB[i])).ToArray();
            var (fa, pa, fb, pb, fab, pab) = TwoWay(y, a, b, levelsA.Count, levelsB.Count, interaction);
            rawA[j] = pa; rawB[j] = pb; rawAB[j] = pab;
            var means = new double[levelsA.Count * levelsB.Count];
            for (var ca = 0; ca < levelsA.Count; ca++)
                for (var cb = 0; cb < levelsB.Count; cb++)
                {
                    var values = kept.Where((_, k) => a[k] == ca && b[k] == cb).Select(i => transformed.Values[i, j]).ToList();
                    means[ca * levelsB.Count + cb] = values.Count == 0 ? double.NaN : values.Average();
                }
            var feature = transformed.Features[j];
            features.Add(new TwoWayFeature(feature.Id, AnalysisTable.LabelOf(feature), string.IsNullOrEmpty(feature.Ontology) ? "unknown" : feature.Ontology,
                fa, pa, double.NaN, fb, pb, double.NaN, fab, pab, double.NaN, means));
        }
        var adjA = MultipleTesting.Adjust(rawA, adjustment);
        var adjB = MultipleTesting.Adjust(rawB, adjustment);
        var adjAB = interaction ? MultipleTesting.Adjust(rawAB, adjustment) : rawAB;
        for (var j = 0; j < p; j++) features[j] = features[j] with { AdjustedPA = adjA[j], AdjustedPB = adjB[j], AdjustedPAB = interaction ? adjAB[j] : double.NaN };
        var sigA = features.Count(f => f.AdjustedPA <= 0.05);
        var sigB = features.Count(f => f.AdjustedPB <= 0.05);
        var sigAB = features.Count(f => f.AdjustedPAB <= 0.05);
        var design = $"{levelsA.Count} × {levelsB.Count} design, {filled} of {levelsA.Count * levelsB.Count} cells filled, {rows.Count} injections";
        var message = $"{design} · at {Univariate.AdjustName(adjustment)} ≤ 0.05: {sigA} feature(s) by {nameA}, {sigB} by {nameB}"
            + (interaction ? $", {sigAB} by the interaction" : replicated == 0 ? " · no cell has a replicate, so the interaction cannot be told from the error and is not tested" : string.Empty);
        return new TwoWayResult(nameA, nameB, levelsA, levelsB, interaction, features, message);
    }

    /// <summary>Type-II sums of squares by comparing nested least-squares fits.</summary>
    private static (double FA, double PA, double FB, double PB, double FAB, double PAB) TwoWay(double[] y, int[] a, int[] b, int na, int nb, bool interaction)
    {
        var n = y.Length;
        if (n < 3) return (double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        var (rssA, rankA) = Ols.Fit(Design(a, b, na, nb, withA: true, withB: false, withAB: false), y);
        var (rssB, rankB) = Ols.Fit(Design(a, b, na, nb, withA: false, withB: true, withAB: false), y);
        var (rssAB, rankAB) = Ols.Fit(Design(a, b, na, nb, withA: true, withB: true, withAB: false), y);
        double rssFull = rssAB, rankFull = rankAB;
        if (interaction) (rssFull, rankFull) = Ols.Fit(Design(a, b, na, nb, withA: true, withB: true, withAB: true), y);
        var dfResidual = n - rankFull;
        if (dfResidual <= 0) return (double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        var mse = rssFull / dfResidual;
        (double F, double P) Effect(double rssReduced, double rankReduced, double rssMore, double rankMore)
        {
            var df = rankMore - rankReduced;
            var ss = rssReduced - rssMore;
            if (df <= 0) return (double.NaN, double.NaN);
            if (mse <= 0) return (ss > 1e-12 ? double.PositiveInfinity : 0, ss > 1e-12 ? 0 : 1);
            var f = Math.Max(0, ss / df) / mse;
            return (f, Distributions.FisherUpper(f, df, dfResidual));
        }
        var (fa, pa) = Effect(rssB, rankB, rssAB, rankAB);   // A given B
        var (fb, pb) = Effect(rssA, rankA, rssAB, rankAB);   // B given A
        var (fab, pab) = interaction ? Effect(rssAB, rankAB, rssFull, rankFull) : (double.NaN, double.NaN);
        return (fa, pa, fb, pb, fab, pab);
    }

    /// <summary>The design matrix with treatment coding: an intercept, then the dummies asked for.</summary>
    private static double[][] Design(int[] a, int[] b, int na, int nb, bool withA, bool withB, bool withAB)
    {
        var columns = 1 + (withA ? na - 1 : 0) + (withB ? nb - 1 : 0) + (withAB ? (na - 1) * (nb - 1) : 0);
        var x = new double[a.Length][];
        for (var i = 0; i < a.Length; i++)
        {
            var row = new double[columns];
            var c = 0;
            row[c++] = 1;
            if (withA) for (var k = 1; k < na; k++) row[c++] = a[i] == k ? 1 : 0;
            if (withB) for (var k = 1; k < nb; k++) row[c++] = b[i] == k ? 1 : 0;
            if (withAB) for (var ka = 1; ka < na; ka++) for (var kb = 1; kb < nb; kb++) row[c++] = a[i] == ka && b[i] == kb ? 1 : 0;
            x[i] = row;
        }
        return x;
    }

    /// <summary>
    /// ASCA: the matrix is split into the part each factor explains — the cell means of that factor,
    /// centred — and the residual; each part gets a principal-component model, and a permutation
    /// test says whether a part holds more variation than shuffled labels would give it.
    /// </summary>
    public static AscaResult Asca(DataMatrix scaled, IReadOnlyList<string> factorA, IReadOnlyList<string> factorB, string nameA, string nameB,
        bool interaction = true, int permutations = 200, int seed = 7)
    {
        ArgumentNullException.ThrowIfNull(scaled);
        var n = scaled.SampleCount;
        var p = scaled.FeatureCount;
        if (factorA.Count != n || factorB.Count != n) throw new ArgumentException("one level per injection");
        var levelsA = factorA.Where(l => !string.IsNullOrEmpty(l)).Distinct().OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToList();
        var levelsB = factorB.Where(l => !string.IsNullOrEmpty(l)).Distinct().OrderBy(l => l, StringComparer.OrdinalIgnoreCase).ToList();
        if (levelsA.Count < 2 || levelsB.Count < 2 || n < 4 || p < 2)
        {
            return new AscaResult(Array.Empty<AscaEffect>(), double.NaN, permutations, $"Each factor needs at least two levels and the design at least four injections; {nameA} has {levelsA.Count} level(s), {nameB} {levelsB.Count}.");
        }
        var a = Enumerable.Range(0, n).Select(i => levelsA.IndexOf(factorA[i])).ToArray();
        var b = Enumerable.Range(0, n).Select(i => levelsB.IndexOf(factorB[i])).ToArray();
        // centre the matrix, fill the gaps with the feature mean
        var x = new double[n, p];
        for (var j = 0; j < p; j++)
        {
            var mean = Enumerable.Range(0, n).Select(i => scaled.Values[i, j]).Where(v => !double.IsNaN(v)).DefaultIfEmpty(0).Average();
            for (var i = 0; i < n; i++) x[i, j] = double.IsNaN(scaled.Values[i, j]) ? 0 : scaled.Values[i, j] - mean;
        }
        var total = SumOfSquares(x);
        var partsA = EffectMatrix(x, a, levelsA.Count);
        var partsB = EffectMatrix(x, b, levelsB.Count);
        double[,]? partsAB = null;
        if (interaction)
        {
            var cell = new int[n];
            for (var i = 0; i < n; i++) cell[i] = a[i] * levelsB.Count + b[i];
            var cells = EffectMatrix(x, cell, levelsA.Count * levelsB.Count);
            partsAB = new double[n, p];
            for (var i = 0; i < n; i++) for (var j = 0; j < p; j++) partsAB[i, j] = cells[i, j] - partsA[i, j] - partsB[i, j];
        }
        var residual = new double[n, p];
        for (var i = 0; i < n; i++) for (var j = 0; j < p; j++) residual[i, j] = x[i, j] - partsA[i, j] - partsB[i, j] - (partsAB?[i, j] ?? 0);

        var rng = new Random(seed);
        var residualPca = Pca.Compute(DataMatrix.Prepared(residual, scaled.Samples, scaled.Features), 1);
        var effects = new List<AscaEffect>
        {
            Model(nameA, partsA, residual, residualPca, total, a, levelsA.Count, scaled, factorA, factorB, permutations, rng),
            Model(nameB, partsB, residual, residualPca, total, b, levelsB.Count, scaled, factorA, factorB, permutations, rng),
        };
        if (partsAB is not null)
        {
            // the interaction's permutation shuffles the cell labels with the main effects taken out
            var cell = new int[n];
            for (var i = 0; i < n; i++) cell[i] = a[i] * levelsB.Count + b[i];
            var main = new double[n, p];
            for (var i = 0; i < n; i++) for (var j = 0; j < p; j++) main[i, j] = x[i, j] - partsA[i, j] - partsB[i, j];
            var observed = SumOfSquares(partsAB);
            var beat = 0;
            for (var k = 0; k < permutations; k++)
            {
                var shuffled = Shuffle(cell, rng);
                if (SumOfSquares(EffectMatrix(main, shuffled, levelsA.Count * levelsB.Count)) >= observed - 1e-12) beat++;
            }
            var pca = Pca.Compute(DataMatrix.Prepared(partsAB, scaled.Samples, scaled.Features), 2);
            var (scores, residualAxis) = Project(partsAB, residual, pca, residualPca, scaled, factorA, factorB);
            effects.Add(new AscaEffect($"{nameA} × {nameB}", 100 * observed / total, (beat + 1.0) / (permutations + 1.0), pca.ExplainedVariance, scores, pca.Loadings) { SecondAxisIsResidual = residualAxis });
        }
        var residualPercent = 100 * SumOfSquares(residual) / total;
        var message = string.Join(" · ", effects.Select(e => $"{e.Name} {e.PercentOfVariation:F1} % (p {e.PermutationP:0.000})")) + $" · residual {residualPercent:F1} % · {permutations} permutations";
        return new AscaResult(effects, residualPercent, permutations, message);
    }

    private static AscaEffect Model(string name, double[,] part, double[,] residual, PcaResult residualPca, double total, int[] labels, int levels, DataMatrix scaled,
        IReadOnlyList<string> factorA, IReadOnlyList<string> factorB, int permutations, Random rng)
    {
        var n = part.GetLength(0);
        var p = part.GetLength(1);
        var observed = SumOfSquares(part);
        // what the effect explains once the other effects are gone: the labels are shuffled over the
        // matrix with everything but this effect and the residual removed
        var own = new double[n, p];
        for (var i = 0; i < n; i++) for (var j = 0; j < p; j++) own[i, j] = part[i, j] + residual[i, j];
        var beat = 0;
        for (var k = 0; k < permutations; k++)
        {
            var shuffled = Shuffle(labels, rng);
            if (SumOfSquares(EffectMatrix(own, shuffled, levels)) >= observed - 1e-12) beat++;
        }
        var pca = Pca.Compute(DataMatrix.Prepared(part, scaled.Samples, scaled.Features), 2);
        var (scores, residualAxis) = Project(part, residual, pca, residualPca, scaled, factorA, factorB);
        return new AscaEffect(name, 100 * observed / total, (beat + 1.0) / (permutations + 1.0), pca.ExplainedVariance, scores, pca.Loadings) { SecondAxisIsResidual = residualAxis };
    }

    /// <summary>
    /// The effect plus the residual, projected onto the effect's components: the usual ASCA scores
    /// plot, with the spread of the replicates shown. A rank-one effect has no second component,
    /// and the residual's first stands in for it.
    /// </summary>
    private static (IReadOnlyList<AscaSample> Scores, bool ResidualAxis) Project(double[,] part, double[,] residual, PcaResult pca, PcaResult residualPca, DataMatrix scaled, IReadOnlyList<string> factorA, IReadOnlyList<string> factorB)
    {
        var n = part.GetLength(0);
        var p = part.GetLength(1);
        var loadings = pca.Loadings.Select(l => l.Components).ToList();
        var residualAxis = pca.ExplainedVariance.Count < 2 || pca.ExplainedVariance[1] < 1e-6;
        var second = residualAxis ? residualPca.Loadings.Select(l => l.Components[0]).ToList() : loadings.Select(l => l.Length > 1 ? l[1] : 0).ToList();
        var result = new List<AscaSample>(n);
        for (var i = 0; i < n; i++)
        {
            double pc1 = 0, pc2 = 0;
            for (var j = 0; j < p; j++)
            {
                var v = part[i, j] + residual[i, j];
                if (j < loadings.Count) pc1 += v * loadings[j][0];
                if (j < second.Count) pc2 += (residualAxis ? residual[i, j] : v) * second[j];
            }
            var s = scaled.Samples[i];
            result.Add(new AscaSample(s.FileId, s.FileName, factorA[i], factorB[i], pc1, pc2));
        }
        return (result, residualAxis);
    }

    private static double[,] EffectMatrix(double[,] x, int[] labels, int levels)
    {
        var n = x.GetLength(0);
        var p = x.GetLength(1);
        var sums = new double[levels, p];
        var counts = new int[levels];
        for (var i = 0; i < n; i++)
        {
            if (labels[i] < 0) continue;
            counts[labels[i]]++;
            for (var j = 0; j < p; j++) sums[labels[i], j] += x[i, j];
        }
        var m = new double[n, p];
        for (var i = 0; i < n; i++)
        {
            if (labels[i] < 0 || counts[labels[i]] == 0) continue;
            for (var j = 0; j < p; j++) m[i, j] = sums[labels[i], j] / counts[labels[i]];
        }
        return m;
    }

    private static double SumOfSquares(double[,] m)
    {
        double s = 0;
        foreach (var v in m) s += v * v;
        return s;
    }

    private static int[] Shuffle(int[] labels, Random rng)
    {
        var copy = (int[])labels.Clone();
        for (var i = copy.Length - 1; i > 0; i--)
        {
            var k = rng.Next(i + 1);
            (copy[i], copy[k]) = (copy[k], copy[i]);
        }
        return copy;
    }
}

/// <summary>Ordinary least squares by the normal equations with pivoting, small enough for a design matrix and exact enough for an F.</summary>
internal static class Ols
{
    /// <summary>The residual sum of squares of the least-squares fit of y on X, and the rank of X.</summary>
    public static (double Rss, int Rank) Fit(double[][] x, double[] y)
    {
        var n = y.Length;
        if (n == 0) return (0, 0);
        var k = x[0].Length;
        // X'X and X'y
        var ata = new double[k, k + 1];
        for (var i = 0; i < n; i++)
        {
            var row = x[i];
            for (var r = 0; r < k; r++)
            {
                if (row[r] == 0) continue;
                for (var c = 0; c < k; c++) ata[r, c] += row[r] * row[c];
                ata[r, k] += row[r] * y[i];
            }
        }
        // Gauss–Jordan with full column pivoting; a pivot under the tolerance is a dependent column
        var scale = 0.0;
        for (var r = 0; r < k; r++) scale = Math.Max(scale, Math.Abs(ata[r, r]));
        var tolerance = Math.Max(1e-10, scale * 1e-9);
        var used = new bool[k];
        var rank = 0;
        var beta = new double[k];
        for (var step = 0; step < k; step++)
        {
            var pivot = -1;
            var best = tolerance;
            for (var r = 0; r < k; r++)
            {
                if (used[r]) continue;
                if (Math.Abs(ata[r, r]) > best) { best = Math.Abs(ata[r, r]); pivot = r; }
            }
            if (pivot < 0) break;
            used[pivot] = true;
            rank++;
            var inv = 1.0 / ata[pivot, pivot];
            for (var c = 0; c <= k; c++) ata[pivot, c] *= inv;
            for (var r = 0; r < k; r++)
            {
                if (r == pivot) continue;
                var factor = ata[r, pivot];
                if (factor == 0) continue;
                for (var c = 0; c <= k; c++) ata[r, c] -= factor * ata[pivot, c];
            }
        }
        for (var r = 0; r < k; r++) beta[r] = used[r] ? ata[r, k] : 0;
        double rss = 0;
        for (var i = 0; i < n; i++)
        {
            double fit = 0;
            for (var c = 0; c < k; c++) fit += x[i][c] * beta[c];
            rss += (y[i] - fit) * (y[i] - fit);
        }
        return (rss, rank);
    }
}
