namespace OpenDIAL.Pipeline.Statistics;

/// <summary>One injection in the orthogonal model: where it falls along the separation, and beside it.</summary>
public sealed record OplsScore(int FileId, string Sample, string Class, double Predictive, double Orthogonal, string Assigned);

/// <summary>
/// A feature's place in the S-plot. <see cref="Covariance"/> says how much of the separation it
/// carries and <see cref="Correlation"/> how reliably it carries it; the features worth believing
/// sit in the corners, far out on both.
/// </summary>
public sealed record OplsLoading(int FeatureId, string Label, string Group, double Covariance, double Correlation, double Weight, double Vip)
{
    /// <summary>The class this feature is higher in, filled in by the caller from the score signs.</summary>
    public string Side { get; init; } = string.Empty;
}

public sealed record OplsResult(
    IReadOnlyList<OplsScore> Scores,
    IReadOnlyList<OplsLoading> Loadings,
    IReadOnlyList<string> Classes,
    int OrthogonalComponents,
    double PredictiveVarianceX,
    double OrthogonalVarianceX,
    double R2Y,
    double Q2,
    double PermutationP,
    int Permutations,
    int CorrectlyClassified,
    string Message);

/// <summary>
/// Orthogonal partial least squares discriminant analysis.
///
/// A plain discriminant model spreads the separation between the classes across every component it
/// fits, mixed in with variation that has nothing to do with them — the run order, the extraction,
/// the animal. OPLS asks the same question and answers it in a tidier shape: it strips out, one
/// component at a time, the variation in the features that is orthogonal to the class membership,
/// and what is left is a single predictive component that holds the whole separation. The score
/// plot then reads directly: left to right is the difference between the classes, up and down is
/// everything else that was in the way.
///
/// It fits no better than the plain model. It is the same information rotated, so the honest
/// numbers matter just as much and are reported alongside: leave-one-out Q squared, and a
/// permutation test that shuffles the labels and asks how often chance does as well. The rotation
/// makes a model easier to read; it does not make a weak one true.
///
/// Two classes only, because the predictive part is one direction and a direction has two ends.
///
/// Trygg and Wold, Journal of Chemometrics 16 (2002) 119-128.
/// </summary>
public static class OrthogonalProjection
{
    public static OplsResult Compute(DataMatrix matrix, int orthogonalComponents = 1, int permutations = 200, int seed = 20260907)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        var n = matrix.SampleCount;
        var p = matrix.FeatureCount;
        var classes = matrix.Samples.Select(s => string.IsNullOrEmpty(s.Class) ? "(none)" : s.Class).ToArray();
        var distinct = classes.Distinct().OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToArray();

        if (distinct.Length < 2)
        {
            return Empty(distinct, "Needs two classes; set them in the Samples workspace.");
        }
        if (distinct.Length > 2)
        {
            return Empty(distinct, $"The orthogonal model separates two classes, and this batch has {distinct.Length}. Use the plain discriminant model, or narrow the comparison.");
        }
        if (n < 4)
        {
            return Empty(distinct, $"Needs at least four injections; this batch has {n}.");
        }
        if (p < 2)
        {
            return Empty(distinct, "Needs more than one feature.");
        }

        // one column, centred: minus one for the first class and plus one for the second
        var y = Response(classes, distinct);
        orthogonalComponents = Math.Clamp(orthogonalComponents, 0, Math.Max(0, Math.Min(n - 3, 5)));

        var model = Fit(matrix.Values, y, orthogonalComponents);
        if (model is null)
        {
            return Empty(distinct, "The features carry nothing that lines up with these classes.");
        }

        var scores = new List<OplsScore>(n);
        var correct = 0;
        for (var i = 0; i < n; i++)
        {
            var fitted = model.Predictive[i] * model.Regression;
            var assigned = distinct[fitted >= 0 ? 1 : 0];
            if (string.Equals(assigned, classes[i], StringComparison.OrdinalIgnoreCase)) correct++;
            scores.Add(new OplsScore(
                matrix.Samples[i].FileId,
                matrix.Samples[i].FileName,
                classes[i],
                model.Predictive[i],
                model.Orthogonal.Count > 0 ? model.Orthogonal[0][i] : 0,
                assigned));
        }

        // the class each end of the predictive component belongs to, read off the injections
        var meanByClass = scores.GroupBy(s => s.Class).ToDictionary(g => g.Key, g => g.Average(s => s.Predictive));
        var ordered = distinct.OrderBy(c => meanByClass.TryGetValue(c, out var m) ? m : 0).ToArray();
        var negative = ordered[0];
        var positive = ordered[^1];

        var loadings = new List<OplsLoading>(p);
        for (var j = 0; j < p; j++)
        {
            loadings.Add(new OplsLoading(
                matrix.Features[j].Id,
                Label(matrix.Features[j]),
                matrix.Features[j].Ontology ?? string.Empty,
                model.Covariance[j],
                model.Correlation[j],
                model.Weights[j],
                model.Vip[j])
            {
                Side = model.Covariance[j] < 0 ? negative : positive,
            });
        }

        var r2y = R2(y, model.Predictive, model.Regression);
        var folds = CrossValidation.BuildFolds(matrix.Values);
        var q2 = CrossValidate(folds, y, orthogonalComponents);
        var permutationP = permutations > 0 ? Permute(folds, y, orthogonalComponents, q2, permutations, seed) : double.NaN;

        var message = $"{n} injections, {distinct.Length} classes, {orthogonalComponents} orthogonal component(s) · R²Y {r2y:F2} · Q² {q2:F2}"
            + (double.IsNaN(permutationP) ? string.Empty : $" · permutation p {permutationP:F3}");

        return new OplsResult(
            scores, loadings, distinct, orthogonalComponents,
            model.PredictiveVariance, model.OrthogonalVariance,
            r2y, q2, permutationP, permutations, correct, message);

        OplsResult Empty(IReadOnlyList<string> known, string reason) => new(
            Array.Empty<OplsScore>(), Array.Empty<OplsLoading>(), known, 0, 0, 0,
            double.NaN, double.NaN, double.NaN, 0, 0, reason);
    }

    private static string Label(Results.AlignmentSpotRow row) =>
        string.IsNullOrWhiteSpace(row.Name) || row.Name.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)
            ? $"m/z {row.Mz:F4} @ {row.Rt:F2}"
            : row.Name;

    /// <summary>Minus one and plus one, in the alphabetical order of the class names, then centred.</summary>
    private static double[] Response(string[] classes, string[] distinct)
    {
        var y = new double[classes.Length];
        for (var i = 0; i < classes.Length; i++)
        {
            y[i] = string.Equals(classes[i], distinct[0], StringComparison.OrdinalIgnoreCase) ? -1 : 1;
        }
        var mean = y.Average();
        for (var i = 0; i < y.Length; i++) y[i] -= mean;
        return y;
    }

    private sealed class Model
    {
        public double[] Predictive = Array.Empty<double>();          // t, one per injection
        public List<double[]> Orthogonal = new();                    // t_ortho, one list per component
        public List<double[]> OrthogonalWeights = new();             // w_ortho, in the features
        public List<double[]> OrthogonalLoadings = new();            // p_ortho, likewise
        public double[] Weights = Array.Empty<double>();             // w, one per feature
        public double[] Covariance = Array.Empty<double>();          // the S-plot, across
        public double[] Correlation = Array.Empty<double>();         // and up
        public double[] Vip = Array.Empty<double>();
        public double Regression;                                    // y ≈ b t
        public double PredictiveVariance;                            // per cent of X on the predictive part
        public double OrthogonalVariance;                            // and on everything stripped out
    }

    /// <summary>
    /// The orthogonal signal correction, done plainly on the whole matrix. This runs once, for the
    /// model that is reported, so it can afford to carry every feature; the cross-validation and the
    /// permutation test use the folded form below instead.
    /// </summary>
    private static Model? Fit(double[,] x, double[] y, int orthogonalComponents)
    {
        var n = x.GetLength(0);
        var p = x.GetLength(1);
        var e = Copy(x);
        var totalSs = SumSquares(e);
        if (totalSs <= 0) return null;

        var model = new Model();
        var orthogonalSs = 0.0;

        var w = new double[p];
        var t = new double[n];
        for (var k = 0; k <= orthogonalComponents; k++)
        {
            // w ∝ E'y: the direction in the features that lines up with the class membership
            for (var j = 0; j < p; j++)
            {
                double s = 0;
                for (var i = 0; i < n; i++) s += e[i, j] * y[i];
                w[j] = s;
            }
            var norm = Math.Sqrt(w.Sum(v => v * v));
            if (norm < 1e-12) return null;
            for (var j = 0; j < p; j++) w[j] /= norm;

            for (var i = 0; i < n; i++)
            {
                double s = 0;
                for (var j = 0; j < p; j++) s += e[i, j] * w[j];
                t[i] = s;
            }
            var tt = t.Sum(v => v * v);
            if (tt < 1e-12) return null;
            if (k == orthogonalComponents) break;   // the last pass keeps the predictive part

            // the loading of that score, and the part of it that has nothing to do with the classes
            var loading = new double[p];
            for (var j = 0; j < p; j++)
            {
                double s = 0;
                for (var i = 0; i < n; i++) s += e[i, j] * t[i];
                loading[j] = s / tt;
            }
            double projection = 0;
            for (var j = 0; j < p; j++) projection += w[j] * loading[j];
            var wOrtho = new double[p];
            for (var j = 0; j < p; j++) wOrtho[j] = loading[j] - projection * w[j];
            var orthoNorm = Math.Sqrt(wOrtho.Sum(v => v * v));
            if (orthoNorm < 1e-12) break;           // nothing orthogonal left to strip
            for (var j = 0; j < p; j++) wOrtho[j] /= orthoNorm;

            var tOrtho = new double[n];
            for (var i = 0; i < n; i++)
            {
                double s = 0;
                for (var j = 0; j < p; j++) s += e[i, j] * wOrtho[j];
                tOrtho[i] = s;
            }
            var ttOrtho = tOrtho.Sum(v => v * v);
            if (ttOrtho < 1e-12) break;

            var pOrtho = new double[p];
            for (var j = 0; j < p; j++)
            {
                double s = 0;
                for (var i = 0; i < n; i++) s += e[i, j] * tOrtho[i];
                pOrtho[j] = s / ttOrtho;
            }
            for (var i = 0; i < n; i++)
                for (var j = 0; j < p; j++)
                    e[i, j] -= tOrtho[i] * pOrtho[j];

            orthogonalSs += ttOrtho * pOrtho.Sum(v => v * v);
            model.Orthogonal.Add(tOrtho);
            model.OrthogonalWeights.Add(wOrtho);
            model.OrthogonalLoadings.Add(pOrtho);
        }

        var ttFinal = t.Sum(v => v * v);
        var predictiveLoading = new double[p];
        for (var j = 0; j < p; j++)
        {
            double s = 0;
            for (var i = 0; i < n; i++) s += e[i, j] * t[i];
            predictiveLoading[j] = s / ttFinal;
        }

        model.Predictive = (double[])t.Clone();
        model.Weights = (double[])w.Clone();
        model.Regression = Dot(y, t) / ttFinal;
        model.PredictiveVariance = 100.0 * ttFinal * predictiveLoading.Sum(v => v * v) / totalSs;
        model.OrthogonalVariance = 100.0 * orthogonalSs / totalSs;

        // The S-plot, straight off the deflated matrix: how much of the separation each feature
        // carries, and how reliably. Magnitude without reliability is one loud injection.
        model.Covariance = new double[p];
        model.Correlation = new double[p];
        model.Vip = new double[p];
        var tSd = Math.Sqrt(ttFinal / Math.Max(1, n - 1));
        for (var j = 0; j < p; j++)
        {
            double cov = 0, ss = 0;
            for (var i = 0; i < n; i++)
            {
                cov += e[i, j] * t[i];
                ss += e[i, j] * e[i, j];
            }
            model.Covariance[j] = cov / Math.Max(1, n - 1);
            var featureSd = Math.Sqrt(ss / Math.Max(1, n - 1));
            model.Correlation[j] = featureSd <= 1e-12 || tSd <= 1e-12 ? 0 : model.Covariance[j] / (featureSd * tSd);
        }

        // With one predictive component VIP reduces to the weight, scaled so the mean square is one
        var meanSquare = model.Weights.Sum(v => v * v) / p;
        var scale = meanSquare <= 1e-18 ? 0 : 1.0 / Math.Sqrt(meanSquare);
        for (var j = 0; j < p; j++) model.Vip[j] = Math.Abs(model.Weights[j]) * scale;

        return model;
    }

    /// <summary>Leave-one-out Q squared, over folds that carry no features.</summary>
    private static double CrossValidate(CrossValidation.Fold[] folds, double[] y, int orthogonalComponents)
    {
        var n = folds.Length;
        if (n < 4) return double.NaN;
        var mean = y.Average();
        double press = 0, tss = 0;
        for (var left = 0; left < n; left++)
        {
            var trainY = new double[n - 1];
            var r = 0;
            for (var i = 0; i < n; i++)
            {
                if (i == left) continue;
                trainY[r++] = y[i];
            }
            // centre the response on the training injections too
            var trainMean = trainY.Average();
            for (var i = 0; i < n - 1; i++) trainY[i] -= trainMean;

            var predicted = FitAndPredict(folds[left], trainY, Math.Min(orthogonalComponents, n - 3)) + trainMean;
            press += Math.Pow(y[left] - predicted, 2);
            tss += Math.Pow(y[left] - mean, 2);
        }
        return tss <= 0 ? double.NaN : 1 - press / tss;
    }

    /// <summary>
    /// The same orthogonal correction as <see cref="Fit"/>, written so that nothing carries the
    /// length of a feature vector.
    ///
    /// Each weight is a combination of the training rows, so it is named by its n coefficients
    /// instead of its p entries, and every product between weights, scores and loadings comes back
    /// to the matrix of products between the injections that the fold already holds. A test holds
    /// this to the plain version.
    /// </summary>
    private static double FitAndPredict(CrossValidation.Fold fold, double[] y, int orthogonalComponents)
    {
        var g = fold.Gram;
        var n = g.GetLength(0);
        if (n < 2) return 0;

        var rotation = CrossValidation.Identity(n);
        var orthoOmegas = new List<double[]>();
        var orthoGOmegas = new List<double[]>();
        var orthoPis = new List<double[]>();

        double[] omega = Array.Empty<double>(), gOmega = Array.Empty<double>(), t = Array.Empty<double>();
        var tt = 0.0;

        for (var k = 0; k <= orthogonalComponents; k++)
        {
            // omega ∝ R'y, scaled so the weight it stands for has unit length
            var raw = CrossValidation.TransposeTimes(rotation, y);
            var graw = CrossValidation.Times(g, raw);
            var norm = CrossValidation.Dot(raw, graw);
            if (norm < 1e-18) return 0;
            var scale = 1.0 / Math.Sqrt(norm);
            omega = new double[n];
            gOmega = new double[n];
            for (var i = 0; i < n; i++) { omega[i] = raw[i] * scale; gOmega[i] = graw[i] * scale; }

            t = CrossValidation.Times(rotation, gOmega);
            tt = CrossValidation.Dot(t, t);
            if (tt < 1e-18) return 0;
            if (k == orthogonalComponents) break;

            // the loading of that score, then the part of it orthogonal to the class direction
            var pi = CrossValidation.TransposeTimes(rotation, t);
            for (var i = 0; i < n; i++) pi[i] /= tt;
            var projection = CrossValidation.Dot(omega, CrossValidation.Times(g, pi));
            var omegaOrtho = new double[n];
            for (var i = 0; i < n; i++) omegaOrtho[i] = pi[i] - projection * omega[i];

            var gOmegaOrtho = CrossValidation.Times(g, omegaOrtho);
            var orthoNorm = CrossValidation.Dot(omegaOrtho, gOmegaOrtho);
            if (orthoNorm < 1e-18) break;
            var orthoScale = 1.0 / Math.Sqrt(orthoNorm);
            for (var i = 0; i < n; i++) { omegaOrtho[i] *= orthoScale; gOmegaOrtho[i] *= orthoScale; }

            var tOrtho = CrossValidation.Times(rotation, gOmegaOrtho);
            var ttOrtho = CrossValidation.Dot(tOrtho, tOrtho);
            if (ttOrtho < 1e-18) break;

            var piOrtho = CrossValidation.TransposeTimes(rotation, tOrtho);
            for (var i = 0; i < n; i++) piOrtho[i] /= ttOrtho;

            CrossValidation.Deflate(rotation, tOrtho, ttOrtho);
            orthoOmegas.Add(omegaOrtho);
            orthoGOmegas.Add(gOmegaOrtho);
            orthoPis.Add(piOrtho);
        }

        // the held-out injection, stripped of the same orthogonal parts, then projected
        var orthoScores = new double[orthoOmegas.Count];
        for (var k = 0; k < orthoOmegas.Count; k++)
        {
            var s = CrossValidation.Dot(fold.Cross, orthoOmegas[k]);
            for (var j = 0; j < k; j++) s -= orthoScores[j] * CrossValidation.Dot(orthoPis[j], orthoGOmegas[k]);
            orthoScores[k] = s;
        }
        var predictive = CrossValidation.Dot(fold.Cross, omega);
        for (var k = 0; k < orthoOmegas.Count; k++) predictive -= orthoScores[k] * CrossValidation.Dot(orthoPis[k], gOmega);

        var regression = CrossValidation.Dot(y, t) / tt;
        return predictive * regression;
    }

    private static double Permute(CrossValidation.Fold[] folds, double[] y, int orthogonalComponents, double q2, int permutations, int seed)
    {
        if (double.IsNaN(q2)) return double.NaN;
        var rng = new Random(seed);
        var atLeastAsGood = 0;
        var shuffled = (double[])y.Clone();
        for (var iteration = 0; iteration < permutations; iteration++)
        {
            for (var i = shuffled.Length - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            var q = CrossValidate(folds, shuffled, orthogonalComponents);
            if (!double.IsNaN(q) && q >= q2) atLeastAsGood++;
        }
        return (atLeastAsGood + 1.0) / (permutations + 1.0);
    }

    private static double R2(double[] y, double[] t, double regression)
    {
        double press = 0, tss = 0;
        var mean = y.Average();
        for (var i = 0; i < y.Length; i++)
        {
            press += Math.Pow(y[i] - t[i] * regression, 2);
            tss += Math.Pow(y[i] - mean, 2);
        }
        return tss <= 0 ? double.NaN : 1 - press / tss;
    }

    private static double Dot(double[] a, double[] b)
    {
        double s = 0;
        for (var i = 0; i < a.Length; i++) s += a[i] * b[i];
        return s;
    }

    private static double[,] Copy(double[,] source)
    {
        var n = source.GetLength(0);
        var p = source.GetLength(1);
        var copy = new double[n, p];
        Array.Copy(source, copy, source.Length);
        return copy;
    }

    private static double SumSquares(double[,] values)
    {
        double s = 0;
        foreach (var v in values) s += v * v;
        return s;
    }

    /// <summary>The folded fit on its own, so a test can hold it to the plain one.</summary>
    internal static double PredictQuickly(double[,] x, double[] y, int left, int orthogonalComponents)
    {
        var folds = CrossValidation.BuildFolds(x);
        var trainY = new List<double>();
        for (var i = 0; i < y.Length; i++) if (i != left) trainY.Add(y[i]);
        var mean = trainY.Average();
        var centred = trainY.Select(v => v - mean).ToArray();
        return FitAndPredict(folds[left], centred, orthogonalComponents) + mean;
    }

    /// <summary>The same prediction carrying every feature, for that test.</summary>
    internal static double PredictPlainly(double[,] x, double[] y, int left, int orthogonalComponents)
    {
        var n = x.GetLength(0);
        var p = x.GetLength(1);
        var trainX = new double[n - 1, p];
        var trainY = new List<double>();
        var r = 0;
        for (var i = 0; i < n; i++)
        {
            if (i == left) continue;
            for (var j = 0; j < p; j++) trainX[r, j] = x[i, j];
            trainY.Add(y[i]);
            r++;
        }
        var held = new double[p];
        for (var j = 0; j < p; j++) held[j] = x[left, j];
        for (var j = 0; j < p; j++)
        {
            double mean = 0;
            for (var i = 0; i < n - 1; i++) mean += trainX[i, j];
            mean /= n - 1;
            double ss = 0;
            for (var i = 0; i < n - 1; i++)
            {
                trainX[i, j] -= mean;
                ss += trainX[i, j] * trainX[i, j];
            }
            held[j] -= mean;
            var sd = n > 2 ? Math.Sqrt(ss / (n - 2)) : 0;
            if (sd <= 1e-12) continue;
            for (var i = 0; i < n - 1; i++) trainX[i, j] /= sd;
            held[j] /= sd;
        }

        var responseMean = trainY.Average();
        var centred = trainY.Select(v => v - responseMean).ToArray();
        var model = Fit(trainX, centred, orthogonalComponents);
        if (model is null) return responseMean;

        // strip the same orthogonal parts off the held-out row, then project what is left
        var remaining = (double[])held.Clone();
        for (var k = 0; k < model.OrthogonalWeights.Count; k++)
        {
            var wOrtho = model.OrthogonalWeights[k];
            var pOrtho = model.OrthogonalLoadings[k];
            double score = 0;
            for (var j = 0; j < p; j++) score += remaining[j] * wOrtho[j];
            for (var j = 0; j < p; j++) remaining[j] -= score * pOrtho[j];
        }
        double predictive = 0;
        for (var j = 0; j < p; j++) predictive += remaining[j] * model.Weights[j];
        return predictive * model.Regression + responseMean;
    }
}
