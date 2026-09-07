namespace OpenDIAL.Pipeline.Statistics;

/// <summary>One sample placed on the principal components.</summary>
public sealed record PcaScore(int FileId, string Sample, string Class, double[] Components);

/// <summary>One feature's contribution to the components.</summary>
public sealed record PcaLoading(int FeatureId, string Label, string Group, double[] Components);

public sealed record PcaResult(
    IReadOnlyList<PcaScore> Scores,
    IReadOnlyList<PcaLoading> Loadings,
    IReadOnlyList<double> ExplainedVariance,
    int ComponentCount);

/// <summary>
/// Principal components of the alignment matrix.
///
/// A metabolomics matrix has far more features than samples, so the components are taken from the
/// sample-by-sample Gram matrix rather than the feature covariance: with eight injections that is
/// an 8x8 eigenproblem instead of a 1600x1600 one, and it gives exactly the same components.
/// </summary>
public static class Pca
{
    public static PcaResult Compute(DataMatrix matrix, int components = 3)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        var n = matrix.SampleCount;
        var p = matrix.FeatureCount;
        components = Math.Max(1, Math.Min(components, Math.Max(1, Math.Min(n - 1, p))));
        if (n < 2 || p < 1)
        {
            return new PcaResult(Array.Empty<PcaScore>(), Array.Empty<PcaLoading>(), Array.Empty<double>(), 0);
        }

        // Gram matrix of the samples
        var gram = new double[n, n];
        for (var a = 0; a < n; a++)
        {
            for (var b = a; b < n; b++)
            {
                double sum = 0;
                for (var j = 0; j < p; j++) sum += matrix.Values[a, j] * matrix.Values[b, j];
                gram[a, b] = gram[b, a] = sum;
            }
        }

        var (eigenvalues, eigenvectors) = Jacobi(gram);
        var order = Enumerable.Range(0, n).OrderByDescending(i => eigenvalues[i]).ToArray();
        var total = eigenvalues.Sum(v => Math.Max(0, v));

        var scores = new List<PcaScore>(n);
        for (var i = 0; i < n; i++)
        {
            var values = new double[components];
            for (var k = 0; k < components; k++)
            {
                var idx = order[k];
                values[k] = eigenvectors[i, idx] * Math.Sqrt(Math.Max(0, eigenvalues[idx]));
            }
            var sample = matrix.Samples[i];
            scores.Add(new PcaScore(sample.FileId, sample.FileName, string.IsNullOrEmpty(sample.Class) ? "(none)" : sample.Class, values));
        }

        // loadings: X^T u / sqrt(lambda)
        var loadings = new List<PcaLoading>(p);
        for (var j = 0; j < p; j++)
        {
            var values = new double[components];
            for (var k = 0; k < components; k++)
            {
                var idx = order[k];
                var lambda = Math.Max(1e-12, eigenvalues[idx]);
                double sum = 0;
                for (var i = 0; i < n; i++) sum += matrix.Values[i, j] * eigenvectors[i, idx];
                values[k] = sum / Math.Sqrt(lambda);
            }
            var feature = matrix.Features[j];
            var label = feature.IsAnnotated ? feature.Name : $"m/z {feature.Mz:F4} @ {feature.Rt:F2}";
            loadings.Add(new PcaLoading(feature.Id, label, string.IsNullOrWhiteSpace(feature.Ontology) ? "(no class)" : feature.Ontology, values));
        }

        var explained = order.Take(components).Select(i => total <= 0 ? 0 : 100.0 * Math.Max(0, eigenvalues[i]) / total).ToList();
        return new PcaResult(scores, loadings, explained, components);
    }

    /// <summary>Jacobi eigenvalue iteration for a small symmetric matrix; exact enough and dependency free.</summary>
    private static (double[] Values, double[,] Vectors) Jacobi(double[,] input)
    {
        var n = input.GetLength(0);
        var a = (double[,])input.Clone();
        var v = new double[n, n];
        for (var i = 0; i < n; i++) v[i, i] = 1;

        for (var sweep = 0; sweep < 100; sweep++)
        {
            double off = 0;
            for (var i = 0; i < n; i++)
                for (var j = i + 1; j < n; j++)
                    off += a[i, j] * a[i, j];
            if (off < 1e-18) break;

            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    if (Math.Abs(a[i, j]) < 1e-18) continue;
                    var theta = (a[j, j] - a[i, i]) / (2 * a[i, j]);
                    var t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                    if (theta == 0) t = 1;
                    var c = 1 / Math.Sqrt(t * t + 1);
                    var s = t * c;
                    for (var k = 0; k < n; k++)
                    {
                        var aik = a[i, k];
                        var ajk = a[j, k];
                        a[i, k] = c * aik - s * ajk;
                        a[j, k] = s * aik + c * ajk;
                    }
                    for (var k = 0; k < n; k++)
                    {
                        var aki = a[k, i];
                        var akj = a[k, j];
                        a[k, i] = c * aki - s * akj;
                        a[k, j] = s * aki + c * akj;
                        var vki = v[k, i];
                        var vkj = v[k, j];
                        v[k, i] = c * vki - s * vkj;
                        v[k, j] = s * vki + c * vkj;
                    }
                }
            }
        }
        var values = new double[n];
        for (var i = 0; i < n; i++) values[i] = a[i, i];
        return (values, v);
    }
}
