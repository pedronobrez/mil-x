using OpenDIAL.Pipeline.Results;

namespace OpenDIAL.Pipeline.Statistics;

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
                values[i, j] = transform == ValueTransform.Log10 ? Math.Log10(v) : v;
            }
        }

        // centre, then scale each feature
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
        return new DataMatrix(values, samples, features);
    }
}
