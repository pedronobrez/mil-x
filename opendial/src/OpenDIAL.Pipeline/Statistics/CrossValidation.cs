namespace OpenDIAL.Pipeline.Statistics;

/// <summary>
/// The arithmetic the two multivariate models share: leave-one-out folds prepared once, and the
/// handful of vector operations they are written in.
///
/// A fold carries no features. Every weight, score and loading a fit produces is a combination of
/// the training injections, so all of them can be named by their n coefficients rather than by
/// their p entries, and every product between them comes back to the matrix of products between
/// the injections. That matrix is worked out once here. What it buys is a permutation test that
/// finishes: the labels change on every shuffle, the features never do.
/// </summary>
internal static class CrossValidation
{
    /// <summary>
    /// One leave-one-out fold, reduced to what a fit actually needs from the features: the products
    /// between the training injections, and between them and the one left out. Both are worked out
    /// from the training rows alone, scaling included.
    /// </summary>
    internal sealed class Fold
    {
        public double[,] Gram = new double[0, 0];        // training injections against each other
        public double[] Cross = Array.Empty<double>();   // and against the one left out
    }

    /// <summary>Builds every fold. This is the only place the feature count is paid for.</summary>
    internal static Fold[] BuildFolds(double[,] x)
    {
        var n = x.GetLength(0);
        var p = x.GetLength(1);
        var folds = new Fold[n];
        for (var left = 0; left < n; left++)
        {
            var trainX = new double[n - 1, p];
            var r = 0;
            for (var i = 0; i < n; i++)
            {
                if (i == left) continue;
                for (var j = 0; j < p; j++) trainX[r, j] = x[i, j];
                r++;
            }

            // Centre and scale from the training rows alone. Doing it once over the whole matrix
            // lets the held-out injection influence its own prediction, and a model fitted to noise
            // then comes back with a respectable Q squared — the very thing this is here to catch.
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

            var gram = new double[n - 1, n - 1];
            var cross = new double[n - 1];
            for (var a = 0; a < n - 1; a++)
            {
                for (var b = a; b < n - 1; b++)
                {
                    double s = 0;
                    for (var j = 0; j < p; j++) s += trainX[a, j] * trainX[b, j];
                    gram[a, b] = s;
                    gram[b, a] = s;
                }
                double c = 0;
                for (var j = 0; j < p; j++) c += trainX[a, j] * held[j];
                cross[a] = c;
            }
            folds[left] = new Fold { Gram = gram, Cross = cross };
        }
        return folds;
    }


    internal static double[] Times(double[,] a, double[] v)
    {
        var n = a.GetLength(0);
        var result = new double[n];
        for (var i = 0; i < n; i++)
        {
            double s = 0;
            for (var j = 0; j < v.Length; j++) s += a[i, j] * v[j];
            result[i] = s;
        }
        return result;
    }

    internal static double[] TransposeTimes(double[,] a, double[] v)
    {
        var n = a.GetLength(1);
        var result = new double[n];
        for (var j = 0; j < n; j++)
        {
            double s = 0;
            for (var i = 0; i < v.Length; i++) s += a[i, j] * v[i];
            result[j] = s;
        }
        return result;
    }

    internal static double Dot(double[] a, double[] b)
    {
        double s = 0;
        for (var i = 0; i < a.Length; i++) s += a[i] * b[i];
        return s;
    }

    /// <summary>The identity, the starting point for a deflation that has not happened yet.</summary>
    internal static double[,] Identity(int n)
    {
        var m = new double[n, n];
        for (var i = 0; i < n; i++) m[i, i] = 1;
        return m;
    }

    /// <summary>Applies one more deflation: R becomes (I − t t'/t't) R.</summary>
    internal static void Deflate(double[,] rotation, double[] t, double tt)
    {
        var n = rotation.GetLength(0);
        var row = TransposeTimes(rotation, t);
        for (var i = 0; i < n; i++)
        {
            var factor = t[i] / tt;
            for (var j = 0; j < n; j++) rotation[i, j] -= factor * row[j];
        }
    }
}
