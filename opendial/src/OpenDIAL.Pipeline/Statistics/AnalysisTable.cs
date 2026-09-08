using OpenDIAL.Pipeline.Results;

namespace OpenDIAL.Pipeline.Statistics;

/// <summary>
/// A feature-by-injection table on its way through the analysis: raw responses or ratios first,
/// then filtered, then normalised. Values are [sample, feature]; NaN is a missing value. Unlike
/// <see cref="DataMatrix"/>, which is centred and scaled for the multivariate models, this keeps the
/// values on their own scale, which is what fold changes and tests need.
/// </summary>
public sealed class AnalysisTable
{
    public AnalysisTable(double[,] values, IReadOnlyList<SampleInfo> samples, IReadOnlyList<AlignmentSpotRow> features, string valueName)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(features);
        if (values.GetLength(0) != samples.Count || values.GetLength(1) != features.Count)
        {
            throw new ArgumentException($"The values are {values.GetLength(0)} by {values.GetLength(1)}, but there are {samples.Count} injections and {features.Count} features.");
        }
        Values = values;
        Samples = samples;
        Features = features;
        ValueName = valueName;
    }

    /// <summary>[sample, feature]; NaN where the value is missing.</summary>
    public double[,] Values { get; }
    public IReadOnlyList<SampleInfo> Samples { get; }
    public IReadOnlyList<AlignmentSpotRow> Features { get; }
    /// <summary>What a value is: "peak height", "area ratio to PC 15:0-18:1(d7)", "log10 ratio", …</summary>
    public string ValueName { get; }
    public int SampleCount => Samples.Count;
    public int FeatureCount => Features.Count;

    /// <summary>The classes in the order they first appear, blank ones named "(none)".</summary>
    public IReadOnlyList<string> Classes => Samples.Select(ClassOf).Distinct().ToList();

    public static string ClassOf(SampleInfo s) => string.IsNullOrWhiteSpace(s.Class) ? "(none)" : s.Class;

    /// <summary>The label a feature is shown with.</summary>
    public static string LabelOf(AlignmentSpotRow f) => f.IsAnnotated ? f.Name : $"m/z {f.Mz:F4} @ {f.Rt:F2}";

    public double[] Column(int feature)
    {
        var column = new double[SampleCount];
        for (var i = 0; i < SampleCount; i++) column[i] = Values[i, feature];
        return column;
    }

    public double[] Row(int sample)
    {
        var row = new double[FeatureCount];
        for (var j = 0; j < FeatureCount; j++) row[j] = Values[sample, j];
        return row;
    }

    /// <summary>A copy with the same shape and new values.</summary>
    public AnalysisTable With(double[,] values, string? valueName = null) => new(values, Samples, Features, valueName ?? ValueName);

    /// <summary>A copy keeping only the features whose index is in <paramref name="keep"/>, in that order.</summary>
    public AnalysisTable Select(IReadOnlyList<int> keep)
    {
        var values = new double[SampleCount, keep.Count];
        for (var jj = 0; jj < keep.Count; jj++)
            for (var i = 0; i < SampleCount; i++)
                values[i, jj] = Values[i, keep[jj]];
        return new AnalysisTable(values, Samples, keep.Select(j => Features[j]).ToList(), ValueName);
    }

    /// <summary>A copy keeping only the injections whose index is in <paramref name="keep"/>.</summary>
    public AnalysisTable SelectSamples(IReadOnlyList<int> keep)
    {
        var values = new double[keep.Count, FeatureCount];
        for (var ii = 0; ii < keep.Count; ii++)
            for (var j = 0; j < FeatureCount; j++)
                values[ii, j] = Values[keep[ii], j];
        return new AnalysisTable(values, keep.Select(i => Samples[i]).ToList(), Features, ValueName);
    }

    /// <summary>The multivariate matrix over these values, already transformed: centred and scaled here, nothing else.</summary>
    public DataMatrix ToDataMatrix(ValueScaling scaling)
    {
        var values = (double[,])Values.Clone();
        var n = SampleCount;
        var p = FeatureCount;
        for (var j = 0; j < p; j++)
        {
            // a missing value is set to the feature's mean so it neither pulls nor pushes
            double sum = 0;
            var count = 0;
            for (var i = 0; i < n; i++) if (!double.IsNaN(values[i, j])) { sum += values[i, j]; count++; }
            var mean = count > 0 ? sum / count : 0;
            for (var i = 0; i < n; i++) values[i, j] = double.IsNaN(values[i, j]) ? 0 : values[i, j] - mean;
            if (scaling == ValueScaling.None) continue;
            double ss = 0;
            for (var i = 0; i < n; i++) ss += values[i, j] * values[i, j];
            var sd = n > 1 ? Math.Sqrt(ss / (n - 1)) : 0;
            if (sd <= 1e-12) continue;
            var divisor = scaling switch { ValueScaling.Auto => sd, ValueScaling.Pareto => Math.Sqrt(sd), _ => 1 };
            for (var i = 0; i < n; i++) values[i, j] /= divisor;
        }
        return DataMatrix.Prepared(values, Samples, Features);
    }
}
