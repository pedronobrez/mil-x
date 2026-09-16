using MilX.Pipeline.Results;

namespace MilX.Pipeline.Statistics;

public enum ValueTransform { None, Log10 }

public enum ValueScaling
{
    /// <summary>Centre only: the loud features dominate, which is what you want for abundance work.</summary>
    None,
    /// <summary>Unit variance, so every feature counts the same however abundant it is.</summary>
    Auto,
    /// <summary>Divide by the square root of the standard deviation: between the two, the usual choice in metabolomics.</summary>
    Pareto,
}

/// <summary>
/// The alignment result as a numeric table, samples down the rows and features across the columns,
/// ready for the multivariate views. Missing and non-positive values become the smallest positive
/// value of their feature, which is what a log transform needs and what most pipelines do.
/// </summary>
public sealed class DataMatrix
{
    private DataMatrix(double[,] values, IReadOnlyList<SampleInfo> samples, IReadOnlyList<AlignmentSpotRow> features)
    {
        Values = values;
        Samples = samples;
        Features = features;
    }

    /// <summary>[sample, feature], already transformed, centred and scaled.</summary>
    public double[,] Values { get; }
    public IReadOnlyList<SampleInfo> Samples { get; }
    public IReadOnlyList<AlignmentSpotRow> Features { get; }
    public int SampleCount => Samples.Count;
    public int FeatureCount => Features.Count;

    public static DataMatrix Build(
        IReadOnlyList<AlignmentSpotRow> features,
        IReadOnlyList<SampleInfo> samples,
        bool useArea = false,
        ValueTransform transform = ValueTransform.Log10,
        ValueScaling scaling = ValueScaling.Auto)
    {
        ArgumentNullException.ThrowIfNull(features);
        ArgumentNullException.ThrowIfNull(samples);
        var n = samples.Count;
        var p = features.Count;
        var values = new double[n, p];
        if (n == 0 || p == 0) return new DataMatrix(values, samples, features);

        for (var j = 0; j < p; j++)
        {
            var feature = features[j];
            var column = new double[n];
            var smallest = double.MaxValue;
            for (var i = 0; i < n; i++)
            {
                var peak = feature.SamplePeaks.FirstOrDefault(x => x.FileId == samples[i].FileId);
                var v = peak is null ? double.NaN : useArea ? peak.Area : peak.Height;
                column[i] = v;
                if (!double.IsNaN(v) && v > 0 && v < smallest) smallest = v;
            }
            if (smallest == double.MaxValue) smallest = 1;
            for (var i = 0; i < n; i++)
            {
                var v = column[i];
                if (double.IsNaN(v) || v <= 0) v = smallest;
                values[i, j] = v;
            }
        }

        Prepare(values, transform, scaling);
        return new DataMatrix(values, samples, features);
    }

    /// <summary>
    /// Builds the matrix from responses that have already been pulled out and worked on — the
    /// drift-corrected values, above all — instead of reading them off the features again. The
    /// array is [sample, feature] in the order of the lists given, and is not modified.
    /// </summary>
    public static DataMatrix From(
        double[,] raw,
        IReadOnlyList<SampleInfo> samples,
        IReadOnlyList<AlignmentSpotRow> features,
        ValueTransform transform = ValueTransform.Log10,
        ValueScaling scaling = ValueScaling.Auto)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(features);
        var n = samples.Count;
        var p = features.Count;
        if (raw.GetLength(0) != n || raw.GetLength(1) != p)
        {
            throw new ArgumentException($"The values are {raw.GetLength(0)} by {raw.GetLength(1)}, but there are {n} injections and {p} features.", nameof(raw));
        }

        var values = new double[n, p];
        for (var j = 0; j < p; j++)
        {
            // a feature that is zero everywhere has no floor of its own to stand on
            var smallest = double.MaxValue;
            for (var i = 0; i < n; i++)
            {
                var v = raw[i, j];
                if (!double.IsNaN(v) && v > 0 && v < smallest) smallest = v;
            }
            if (smallest == double.MaxValue) smallest = 1;
            for (var i = 0; i < n; i++)
            {
                var v = raw[i, j];
                values[i, j] = double.IsNaN(v) || v <= 0 ? smallest : v;
            }
        }

        Prepare(values, transform, scaling);
        return new DataMatrix(values, samples, features);
    }

    /// <summary>A matrix whose values were already transformed, centred and scaled by the caller.</summary>
    public static DataMatrix Prepared(double[,] values, IReadOnlyList<SampleInfo> samples, IReadOnlyList<AlignmentSpotRow> features)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.GetLength(0) != samples.Count || values.GetLength(1) != features.Count)
        {
            throw new ArgumentException($"The values are {values.GetLength(0)} by {values.GetLength(1)}, but there are {samples.Count} injections and {features.Count} features.", nameof(values));
        }
        return new DataMatrix(values, samples, features);
    }

    /// <summary>Transforms in place, then centres and scales every feature.</summary>
    private static void Prepare(double[,] values, ValueTransform transform, ValueScaling scaling)
    {
        var n = values.GetLength(0);
        var p = values.GetLength(1);
        if (transform == ValueTransform.Log10)
        {
            for (var j = 0; j < p; j++)
                for (var i = 0; i < n; i++)
                    values[i, j] = Math.Log10(values[i, j]);
        }

        for (var j = 0; j < p; j++)
        {
            double sum = 0;
            for (var i = 0; i < n; i++) sum += values[i, j];
            var mean = sum / n;
            double ss = 0;
            for (var i = 0; i < n; i++)
            {
                values[i, j] -= mean;
                ss += values[i, j] * values[i, j];
            }
            if (scaling == ValueScaling.None) continue;
            var sd = n > 1 ? Math.Sqrt(ss / (n - 1)) : 0;
            if (sd <= 1e-12) continue;
            var divisor = scaling == ValueScaling.Auto ? sd : Math.Sqrt(sd);
            for (var i = 0; i < n; i++) values[i, j] /= divisor;
        }
    }
}
