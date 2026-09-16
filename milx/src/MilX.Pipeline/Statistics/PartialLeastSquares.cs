namespace MilX.Pipeline.Statistics;

public sealed record PlsScore(int FileId, string Sample, string Class, double[] Components, string Predicted);

/// <summary>
/// A feature's weight in the model. VIP above one is the usual mark of a feature that matters, and
/// the sign of the first-component weight says which class it goes with.
/// </summary>
public sealed record PlsLoading(int FeatureId, string Label, string Group, double[] Weights, double Vip);

public sealed record PlsResult(
    IReadOnlyList<PlsScore> Scores,
    IReadOnlyList<PlsLoading> Loadings,
    IReadOnlyList<string> Classes,
    IReadOnlyList<double> ExplainedX,
    double R2Y,
    double Q2,
    double PermutationP,
    int Permutations,
    int CorrectlyClassified,
    string Message);

/// <summary>
/// Partial least squares discriminant analysis: the supervised counterpart of the principal
/// components, asking which features separate classes that were declared rather than which ones
/// carry the most variance.
///
/// With a handful of injections it will separate anything, including noise, so the model is only
/// reported alongside the numbers that say whether to believe it: Q squared from leave-one-out
/// cross-validation, and a permutation test that shuffles the labels and asks how often chance does
/// as well. A high R squared with a low Q squared is a model that has memorised its own samples.
/// </summary>
public static class PartialLeastSquares
{
    public static PlsResult Compute(DataMatrix matrix, int components = 2, int permutations = 200, int seed = 20260907)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        var n = matrix.SampleCount;
        var p = matrix.FeatureCount;
        var classes = matrix.Samples.Select(s => string.IsNullOrEmpty(s.Class) ? "(none)" : s.Class).ToArray();
        var distinct = classes.Distinct().OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToArray();

        if (distinct.Length < 2)
        {
            return Empty(distinct, "Needs at least two classes; set them in the Samples workspace.");
        }
        if (n < 4)
        {
            return Empty(distinct, $"Needs at least four injections; this batch has {n}.");
        }
        components = Math.Max(1, Math.Min(components, Math.Min(n - 2, p)));

        var x = Copy(matrix.Values);
        var y = Dummy(classes, distinct);
        var model = Fit(x, y, components);

        var scores = new List<PlsScore>(n);
        for (var i = 0; i < n; i++)
        {
            var predicted = distinct[ArgMax(model.Fitted, i)];
            var values = new double[components];
            for (var k = 0; k < components; k++) values[k] = model.T[i, k];
            var sample = matrix.Samples[i];
            scores.Add(new PlsScore(sample.FileId, sample.FileName, classes[i], values, predicted));
        }
        var correct = scores.Count(s => s.Predicted == s.Class);

        var loadings = new List<PlsLoading>(p);
        var vips = Vip(model, y);
        for (var j = 0; j < p; j++)
        {
            var weights = new double[components];
            for (var k = 0; k < components; k++) weights[k] = model.W[j, k];
            var feature = matrix.Features[j];
            var label = feature.IsAnnotated ? feature.Name : $"m/z {feature.Mz:F4} @ {feature.Rt:F2}";
            loadings.Add(new PlsLoading(feature.Id, label, string.IsNullOrWhiteSpace(feature.Ontology) ? "(no class)" : feature.Ontology, weights, vips[j]));
        }

        var r2y = R2(y, model.Fitted);

        // Everything below leaves the features alone and only reshuffles the labels, so the work
        // that scales with the number of features is done once, here, and never again: each fold is
        // reduced to the products between its injections. With a couple of thousand features and a
        // couple of hundred shuffles that is the difference between a minute and an instant.
        var folds = CrossValidation.BuildFolds(matrix.Values);
        var q2 = CrossValidate(folds, y, components);
        var permutationP = permutations > 0 ? Permute(folds, classes, distinct, components, q2, permutations, seed) : double.NaN;

        var message = $"{n} injections in {distinct.Length} classes · R²Y {r2y:F2} · Q² {q2:F2}"
            + (double.IsNaN(permutationP) ? string.Empty : $" · permutation p {permutationP:F3}")
            + (q2 < 0.4 ? "  — a Q² this low means the separation does not hold up when a sample is left out."
                        : permutationP is > 0.05 ? "  — shuffled labels did about as well, so the separation is not established."
                        : string.Empty);

        return new PlsResult(scores, loadings, distinct, model.ExplainedX, r2y, q2, permutationP, permutations, correct, message);

        PlsResult Empty(IReadOnlyList<string> known, string reason) => new(
            Array.Empty<PlsScore>(), Array.Empty<PlsLoading>(), known, Array.Empty<double>(),
            double.NaN, double.NaN, double.NaN, 0, 0, reason);
    }

    private sealed class Model
    {
        public double[,] T = new double[0, 0];      // sample scores
        public double[,] W = new double[0, 0];      // feature weights
        public double[,] Q = new double[0, 0];      // response loadings
        public double[,] Fitted = new double[0, 0];
        public double[] ExplainedX = Array.Empty<double>();
    }

    /// <summary>NIPALS, the standard iteration; the matrix arrives already centred and scaled.</summary>
    private static Model Fit(double[,] x, double[,] y, int components)
    {
        var n = x.GetLength(0);
        var p = x.GetLength(1);
        var m = y.GetLength(1);
        var t = new double[n, components];
        var w = new double[p, components];
        var pLoad = new double[p, components];
        var q = new double[m, components];
        var explained = new double[components];
        var totalSs = SumSquares(x);

        var e = Copy(x);
        var f = Copy(y);

        for (var k = 0; k < components; k++)
        {
            // start from the response column with the most variance left
            var u = new double[n];
            var best = 0;
            var bestSs = -1.0;
            for (var c = 0; c < m; c++)
            {
                double ss = 0;
                for (var i = 0; i < n; i++) ss += f[i, c] * f[i, c];
                if (ss > bestSs) { bestSs = ss; best = c; }
            }
            for (var i = 0; i < n; i++) u[i] = f[i, best];

            var wk = new double[p];
            var tk = new double[n];
            var qk = new double[m];
            for (var iteration = 0; iteration < 200; iteration++)
            {
                // w = E' u, normalised
                for (var j = 0; j < p; j++)
                {
                    double s = 0;
                    for (var i = 0; i < n; i++) s += e[i, j] * u[i];
                    wk[j] = s;
                }
                var norm = Math.Sqrt(wk.Sum(v => v * v));
                if (norm < 1e-12) break;
                for (var j = 0; j < p; j++) wk[j] /= norm;

                // t = E w
                for (var i = 0; i < n; i++)
                {
                    double s = 0;
                    for (var j = 0; j < p; j++) s += e[i, j] * wk[j];
                    tk[i] = s;
                }
                var tt = tk.Sum(v => v * v);
                if (tt < 1e-12) break;

                // q = F' t / t't
                for (var c = 0; c < m; c++)
                {
                    double s = 0;
                    for (var i = 0; i < n; i++) s += f[i, c] * tk[i];
                    qk[c] = s / tt;
                }
                var qq = qk.Sum(v => v * v);
                if (qq < 1e-18) break;

                // u = F q / q'q
                var previous = (double[])u.Clone();
                for (var i = 0; i < n; i++)
                {
                    double s = 0;
                    for (var c = 0; c < m; c++) s += f[i, c] * qk[c];
                    u[i] = s / qq;
                }
                double delta = 0;
                for (var i = 0; i < n; i++) delta += Math.Abs(u[i] - previous[i]);
                if (delta < 1e-10) break;
            }

            var ttFinal = tk.Sum(v => v * v);
            if (ttFinal < 1e-12) break;

            // deflate
            var pk = new double[p];
            for (var j = 0; j < p; j++)
            {
                double s = 0;
                for (var i = 0; i < n; i++) s += e[i, j] * tk[i];
                pk[j] = s / ttFinal;
            }
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < p; j++) e[i, j] -= tk[i] * pk[j];
                for (var c = 0; c < m; c++) f[i, c] -= tk[i] * qk[c];
            }
            for (var i = 0; i < n; i++) t[i, k] = tk[i];
            for (var j = 0; j < p; j++) { w[j, k] = wk[j]; pLoad[j, k] = pk[j]; }
            for (var c = 0; c < m; c++) q[c, k] = qk[c];
            explained[k] = totalSs <= 0 ? 0 : 100.0 * ttFinal * pk.Sum(v => v * v) / totalSs;
        }

        // fitted responses: T Q'
        var fitted = new double[n, m];
        for (var i = 0; i < n; i++)
        {
            for (var c = 0; c < m; c++)
            {
                double s = 0;
                for (var k = 0; k < components; k++) s += t[i, k] * q[c, k];
                fitted[i, c] = s;
            }
        }
        return new Model { T = t, W = w, Q = q, Fitted = fitted, ExplainedX = explained };
    }

    /// <summary>Variable importance in projection, the usual ranking of which features carry the model.</summary>
    private static double[] Vip(Model model, double[,] y)
    {
        var p = model.W.GetLength(0);
        var components = model.W.GetLength(1);
        var n = model.T.GetLength(0);
        var m = y.GetLength(1);
        var ssy = new double[components];
        double total = 0;
        for (var k = 0; k < components; k++)
        {
            double tt = 0;
            for (var i = 0; i < n; i++) tt += model.T[i, k] * model.T[i, k];
            double qq = 0;
            for (var c = 0; c < m; c++) qq += model.Q[c, k] * model.Q[c, k];
            ssy[k] = tt * qq;
            total += ssy[k];
        }
        var vip = new double[p];
        if (total <= 0) return vip;
        for (var j = 0; j < p; j++)
        {
            double sum = 0;
            for (var k = 0; k < components; k++)
            {
                double norm = 0;
                for (var jj = 0; jj < p; jj++) norm += model.W[jj, k] * model.W[jj, k];
                if (norm <= 0) continue;
                sum += ssy[k] * model.W[j, k] * model.W[j, k] / norm;
            }
            vip[j] = Math.Sqrt(p * sum / total);
        }
        return vip;
    }

    /// <summary>Leave one injection out, refit, predict it: the honest measure of a small model.</summary>
    /// <summary>Leave-one-out Q squared over prepared folds.</summary>
    private static double CrossValidate(CrossValidation.Fold[] folds, double[,] y, int components)
    {
        var n = folds.Length;
        var m = y.GetLength(1);
        if (n < 4) return double.NaN;
        double press = 0, tss = 0;
        var means = new double[m];
        for (var c = 0; c < m; c++)
        {
            for (var i = 0; i < n; i++) means[c] += y[i, c];
            means[c] /= n;
        }
        for (var left = 0; left < n; left++)
        {
            var trainY = new double[n - 1, m];
            var r = 0;
            for (var i = 0; i < n; i++)
            {
                if (i == left) continue;
                for (var c = 0; c < m; c++) trainY[r, c] = y[i, c];
                r++;
            }
            var predicted = FitAndPredict(folds[left], trainY, Math.Min(components, n - 3), m);
            for (var c = 0; c < m; c++)
            {
                press += Math.Pow(y[left, c] - predicted[c], 2);
                tss += Math.Pow(y[left, c] - means[c], 2);
            }
        }
        return tss <= 0 ? double.NaN : 1 - press / tss;
    }

    /// <summary>
    /// The same NIPALS iteration as <see cref="Fit"/>, written so that nothing carries the length of
    /// a feature vector.
    ///
    /// Every quantity the fit needs lives in the space the injections span: each weight is a
    /// combination of the training rows, so it can be named by its n coefficients rather than by its
    /// p entries, and the products between weights, scores and loadings all come back to the Gram
    /// matrix that was worked out once. The arithmetic is the same and the answer is the same — a
    /// test holds it to the plain version — but the cost stops depending on how many features there
    /// are, which is what makes a permutation test finish on a real result.
    /// </summary>
    private static double[] FitAndPredict(CrossValidation.Fold fold, double[,] y, int components, int m)
    {
        var g = fold.Gram;
        var n = g.GetLength(0);
        var result = new double[m];
        if (n < 2 || components < 1) return result;

        var f = Copy(y);
        var rotation = CrossValidation.Identity(n);            // R: the deflation so far

        var omegas = new List<double[]>(components);           // weights, as combinations of rows
        var gOmegas = new List<double[]>(components);
        var pis = new List<double[]>(components);              // loadings, likewise
        var qs = new List<double[]>(components);

        for (var k = 0; k < components; k++)
        {
            var u = new double[n];
            var best = 0;
            var bestSs = -1.0;
            for (var c = 0; c < m; c++)
            {
                double ss = 0;
                for (var i = 0; i < n; i++) ss += f[i, c] * f[i, c];
                if (ss > bestSs) { bestSs = ss; best = c; }
            }
            for (var i = 0; i < n; i++) u[i] = f[i, best];

            double[]? omega = null, gOmega = null, t = null, q = null;
            var tt = 0.0;
            for (var iteration = 0; iteration < 200; iteration++)
            {
                // omega ∝ R'u, scaled so the weight it stands for has unit length
                var raw = CrossValidation.TransposeTimes(rotation, u);
                var graw = CrossValidation.Times(g, raw);
                var norm = CrossValidation.Dot(raw, graw);
                if (norm < 1e-18) break;
                var scale = 1.0 / Math.Sqrt(norm);
                omega = new double[n];
                gOmega = new double[n];
                for (var i = 0; i < n; i++) { omega[i] = raw[i] * scale; gOmega[i] = graw[i] * scale; }

                t = CrossValidation.Times(rotation, gOmega);
                tt = CrossValidation.Dot(t, t);
                if (tt < 1e-18) { t = null; break; }

                q = new double[m];
                for (var c = 0; c < m; c++)
                {
                    double s = 0;
                    for (var i = 0; i < n; i++) s += f[i, c] * t[i];
                    q[c] = s / tt;
                }
                var qq = CrossValidation.Dot(q, q);
                if (qq < 1e-18) { t = null; break; }

                var previous = (double[])u.Clone();
                for (var i = 0; i < n; i++)
                {
                    double s = 0;
                    for (var c = 0; c < m; c++) s += f[i, c] * q[c];
                    u[i] = s / qq;
                }
                double delta = 0;
                for (var i = 0; i < n; i++) delta += Math.Abs(u[i] - previous[i]);
                if (delta < 1e-12) break;
            }
            if (t is null || omega is null || gOmega is null || q is null) break;

            var pi = CrossValidation.TransposeTimes(rotation, t);
            for (var i = 0; i < n; i++) pi[i] /= tt;

            for (var i = 0; i < n; i++)
                for (var c = 0; c < m; c++)
                    f[i, c] -= t[i] * q[c];

            CrossValidation.Deflate(rotation, t, tt);

            omegas.Add(omega);
            gOmegas.Add(gOmega);
            pis.Add(pi);
            qs.Add(q);
        }

        // the held-out injection, projected through the components in turn
        var scores = new double[omegas.Count];
        for (var k = 0; k < omegas.Count; k++)
        {
            var s = CrossValidation.Dot(fold.Cross, omegas[k]);
            for (var j = 0; j < k; j++) s -= scores[j] * CrossValidation.Dot(pis[j], gOmegas[k]);
            scores[k] = s;
            for (var c = 0; c < m; c++) result[c] += s * qs[k][c];
        }
        return result;
    }

    /// <summary>
    /// The same leave-one-out Q squared worked out the obvious way, carrying every feature through
    /// each fold. Kept only so a test can hold <see cref="FitAndPredict"/> to it: it is too slow to
    /// use on a real result, which is the whole reason the other one exists.
    /// </summary>
    internal static double CrossValidatePlainly(double[,] x, double[,] y, int components)
    {
        var n = x.GetLength(0);
        var p = x.GetLength(1);
        var m = y.GetLength(1);
        if (n < 4) return double.NaN;
        double press = 0, tss = 0;
        var means = new double[m];
        for (var c = 0; c < m; c++)
        {
            for (var i = 0; i < n; i++) means[c] += y[i, c];
            means[c] /= n;
        }
        for (var left = 0; left < n; left++)
        {
            var trainX = new double[n - 1, p];
            var trainY = new double[n - 1, m];
            var r = 0;
            for (var i = 0; i < n; i++)
            {
                if (i == left) continue;
                for (var j = 0; j < p; j++) trainX[r, j] = x[i, j];
                for (var c = 0; c < m; c++) trainY[r, c] = y[i, c];
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
            var model = Fit(trainX, trainY, Math.Min(components, n - 3));
            var predicted = Predict(model, trainX, held, m);
            for (var c = 0; c < m; c++)
            {
                press += Math.Pow(y[left, c] - predicted[c], 2);
                tss += Math.Pow(y[left, c] - means[c], 2);
            }
        }
        return tss <= 0 ? double.NaN : 1 - press / tss;
    }

    /// <summary>The fast path on its own, so a test can compare the two.</summary>
    internal static double CrossValidateQuickly(double[,] x, double[,] y, int components)
        => CrossValidate(CrossValidation.BuildFolds(x), y, components);

    /// <summary>The class labels as a centred dummy response, exposed for the same test.</summary>
    internal static double[,] DummyFor(string[] classes, string[] distinct) => Dummy(classes, distinct);

    /// <summary>Projects one held-out row through the fitted model.</summary>
    private static double[] Predict(Model model, double[,] trainX, double[] row, int m)
    {
        var p = trainX.GetLength(1);
        var components = model.W.GetLength(1);
        var e = (double[])row.Clone();
        var result = new double[m];
        var n = trainX.GetLength(0);
        for (var k = 0; k < components; k++)
        {
            double tk = 0;
            for (var j = 0; j < p; j++) tk += e[j] * model.W[j, k];
            // deflate with the training loading of this component
            double tt = 0;
            for (var i = 0; i < n; i++) tt += model.T[i, k] * model.T[i, k];
            if (tt < 1e-12) break;
            var pk = new double[p];
            for (var j = 0; j < p; j++)
            {
                double s = 0;
                for (var i = 0; i < n; i++) s += trainX[i, j] * model.T[i, k];
                pk[j] = s / tt;
            }
            for (var j = 0; j < p; j++) e[j] -= tk * pk[j];
            for (var c = 0; c < m; c++) result[c] += tk * model.Q[c, k];
        }
        return result;
    }

    /// <summary>Shuffles the labels and refits: how often does chance reach this Q squared?</summary>
    private static double Permute(CrossValidation.Fold[] folds, string[] classes, string[] distinct, int components, double q2, int permutations, int seed)
    {
        if (double.IsNaN(q2)) return double.NaN;
        var rng = new Random(seed);
        var atLeastAsGood = 0;
        var shuffled = (string[])classes.Clone();
        for (var iteration = 0; iteration < permutations; iteration++)
        {
            for (var i = shuffled.Length - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
            }
            var q = CrossValidate(folds, Dummy(shuffled, distinct), components);
            if (!double.IsNaN(q) && q >= q2) atLeastAsGood++;
        }
        return (atLeastAsGood + 1.0) / (permutations + 1.0);
    }

    private static double[,] Dummy(string[] classes, string[] distinct)
    {
        var n = classes.Length;
        var m = distinct.Length;
        var y = new double[n, m];
        for (var i = 0; i < n; i++)
        {
            var index = Array.IndexOf(distinct, classes[i]);
            if (index >= 0) y[i, index] = 1;
        }
        // centre, as the predictors already are
        for (var c = 0; c < m; c++)
        {
            double mean = 0;
            for (var i = 0; i < n; i++) mean += y[i, c];
            mean /= n;
            for (var i = 0; i < n; i++) y[i, c] -= mean;
        }
        return y;
    }

    private static double R2(double[,] y, double[,] fitted)
    {
        var n = y.GetLength(0);
        var m = y.GetLength(1);
        double residual = 0, total = 0;
        for (var c = 0; c < m; c++)
        {
            double mean = 0;
            for (var i = 0; i < n; i++) mean += y[i, c];
            mean /= n;
            for (var i = 0; i < n; i++)
            {
                residual += Math.Pow(y[i, c] - fitted[i, c], 2);
                total += Math.Pow(y[i, c] - mean, 2);
            }
        }
        return total <= 0 ? double.NaN : 1 - residual / total;
    }

    private static int ArgMax(double[,] values, int row)
    {
        var best = 0;
        for (var c = 1; c < values.GetLength(1); c++)
        {
            if (values[row, c] > values[row, best]) best = c;
        }
        return best;
    }

    private static double[,] Copy(double[,] source)
    {
        var copy = new double[source.GetLength(0), source.GetLength(1)];
        Array.Copy(source, copy, source.Length);
        return copy;
    }

    private static double SumSquares(double[,] values)
    {
        double sum = 0;
        foreach (var v in values) sum += v * v;
        return sum;
    }
}
