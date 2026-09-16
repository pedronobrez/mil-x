namespace MilX.Pipeline.Statistics;

public enum MissingValueMethod
{
    /// <summary>A fifth of the smallest positive value of the feature: a stand-in for the detection limit.</summary>
    FifthOfMinimum,
    /// <summary>Half of the feature's minimum.</summary>
    HalfOfMinimum,
    /// <summary>The feature's minimum.</summary>
    Minimum,
    Mean,
    Median,
    /// <summary>The mean of the k nearest features by correlation over the injections that have both.</summary>
    KNearest,
    /// <summary>Leave the gaps as gaps; the tests then work on what is there.</summary>
    Keep,
}

public enum FilterMethod
{
    None,
    /// <summary>Interquartile range: features that hardly vary are dropped.</summary>
    InterquartileRange,
    StandardDeviation,
    /// <summary>Median absolute deviation.</summary>
    MedianAbsoluteDeviation,
    /// <summary>Relative standard deviation, SD over the mean.</summary>
    RelativeStandardDeviation,
    /// <summary>The mean intensity: the faint features go.</summary>
    MeanIntensity,
    MedianIntensity,
}

public enum SampleNormalization
{
    None,
    /// <summary>Every injection divided by its total.</summary>
    Sum,
    /// <summary>Every injection divided by its median.</summary>
    Median,
    /// <summary>Probabilistic quotient normalisation against the median profile of the batch.</summary>
    ProbabilisticQuotient,
    /// <summary>The quotient normalisation against the median profile of the quality controls.</summary>
    ProbabilisticQuotientToControls,
    /// <summary>Every injection divided by one feature of it: an internal standard.</summary>
    ReferenceFeature,
}

public enum Transformation
{
    None,
    Log10,
    Log2,
    NaturalLog,
    SquareRoot,
    CubeRoot,
}

public sealed record PreprocessingOptions
{
    public MissingValueMethod Missing { get; init; } = MissingValueMethod.FifthOfMinimum;
    /// <summary>Features missing in more than this share of the injections are dropped first.</summary>
    public double MaxMissingFraction { get; init; } = 0.5;
    public FilterMethod Filter { get; init; } = FilterMethod.InterquartileRange;
    /// <summary>The share of features the variance filter removes, from the bottom; NaN follows MetaboAnalyst's rule of thumb by size.</summary>
    public double FilterFraction { get; init; } = double.NaN;
    /// <summary>Drop features whose relative standard deviation over the quality controls is above this per cent; 0 is off.</summary>
    public double MaxControlRsd { get; init; } = 0;
    public SampleNormalization Normalization { get; init; } = SampleNormalization.None;
    /// <summary>The feature id to divide by when <see cref="SampleNormalization.ReferenceFeature"/> is chosen.</summary>
    public int? ReferenceFeatureId { get; init; }
    public Transformation Transform { get; init; } = Transformation.Log10;
    public ValueScaling Scaling { get; init; } = ValueScaling.Auto;
}

/// <summary>What the preprocessing did, step by step, for the page that shows it.</summary>
public sealed record PreprocessingReport(
    int FeaturesIn,
    int DroppedForMissing,
    int Imputed,
    int DroppedByFilter,
    int DroppedByControlRsd,
    int FeaturesOut,
    string Normalization,
    string Transform,
    string Scaling)
{
    public string Summary =>
        $"{FeaturesIn} features in · {DroppedForMissing} dropped for missing values · {Imputed} value(s) imputed · "
        + $"{DroppedByFilter} dropped by the filter{(DroppedByControlRsd > 0 ? $" · {DroppedByControlRsd} dropped by the QC RSD" : string.Empty)} · "
        + $"{FeaturesOut} out · {Normalization} · {Transform} · {Scaling}";
}

public sealed record PreprocessedData(
    AnalysisTable Raw,
    AnalysisTable Filtered,
    AnalysisTable Normalized,
    AnalysisTable Transformed,
    DataMatrix Scaled,
    PreprocessingReport Report);

/// <summary>
/// The MetaboAnalyst sequence, in the order it runs there: missing values, the variance filter,
/// sample normalisation, transformation, scaling. Every stage's table is kept, because the tests
/// read different ones — fold changes want the normalised values on their own scale, the t-test
/// wants them transformed, the models want them scaled.
/// </summary>
public static class Preprocessing
{
    public static PreprocessedData Run(AnalysisTable raw, PreprocessingOptions options)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(options);
        var n = raw.SampleCount;

        // 1. features missing too often go; the rest are imputed
        var keep = new List<int>();
        for (var j = 0; j < raw.FeatureCount; j++)
        {
            var missing = 0;
            for (var i = 0; i < n; i++) if (double.IsNaN(raw.Values[i, j])) missing++;
            if (n == 0 || (double)missing / n <= options.MaxMissingFraction) keep.Add(j);
        }
        var droppedForMissing = raw.FeatureCount - keep.Count;
        var table = raw.Select(keep);
        var (imputedTable, imputed) = Impute(table, options.Missing);

        // 2. the variance filter, then the QC RSD filter
        var (filtered, droppedByFilter) = Filter(imputedTable, options.Filter, options.FilterFraction);
        var droppedByRsd = 0;
        if (options.MaxControlRsd > 0)
        {
            (filtered, droppedByRsd) = FilterByControlRsd(filtered, options.MaxControlRsd);
        }

        // 3. sample normalisation, 4. transformation, 5. scaling
        var normalized = Normalize(filtered, options.Normalization, options.ReferenceFeatureId);
        var transformed = Transform(normalized, options.Transform);
        var scaled = transformed.ToDataMatrix(options.Scaling);

        var report = new PreprocessingReport(raw.FeatureCount, droppedForMissing, imputed, droppedByFilter, droppedByRsd, filtered.FeatureCount,
            Describe(options.Normalization), Describe(options.Transform), Describe(options.Scaling));
        return new PreprocessedData(raw, filtered, normalized, transformed, scaled, report);
    }

    public static string Describe(SampleNormalization n) => n switch
    {
        SampleNormalization.None => "no sample normalisation",
        SampleNormalization.Sum => "normalised by sum",
        SampleNormalization.Median => "normalised by median",
        SampleNormalization.ProbabilisticQuotient => "probabilistic quotient normalisation",
        SampleNormalization.ProbabilisticQuotientToControls => "quotient normalisation to the controls",
        SampleNormalization.ReferenceFeature => "normalised to a reference feature",
        _ => n.ToString(),
    };

    public static string Describe(Transformation t) => t switch
    {
        Transformation.None => "no transformation",
        Transformation.Log10 => "log10",
        Transformation.Log2 => "log2",
        Transformation.NaturalLog => "natural log",
        Transformation.SquareRoot => "square root",
        Transformation.CubeRoot => "cube root",
        _ => t.ToString(),
    };

    public static string Describe(ValueScaling s) => s switch
    {
        ValueScaling.None => "mean centring",
        ValueScaling.Auto => "auto scaling",
        ValueScaling.Pareto => "Pareto scaling",
        _ => s.ToString(),
    };

    // ------------------------------------------------------------------ missing values

    public static (AnalysisTable Table, int Imputed) Impute(AnalysisTable table, MissingValueMethod method)
    {
        var n = table.SampleCount;
        var p = table.FeatureCount;
        var values = (double[,])table.Values.Clone();
        var imputed = 0;
        if (method == MissingValueMethod.Keep) return (table, 0);
        for (var j = 0; j < p; j++)
        {
            var present = new List<double>();
            for (var i = 0; i < n; i++) if (!double.IsNaN(values[i, j])) present.Add(values[i, j]);
            if (present.Count == n) continue;
            double fill;
            if (present.Count == 0)
            {
                fill = 0;
            }
            else
            {
                var positive = present.Where(v => v > 0).DefaultIfEmpty(present.Min()).Min();
                fill = method switch
                {
                    MissingValueMethod.FifthOfMinimum => positive / 5,
                    MissingValueMethod.HalfOfMinimum => positive / 2,
                    MissingValueMethod.Minimum => present.Min(),
                    MissingValueMethod.Mean => present.Average(),
                    MissingValueMethod.Median => Median(present),
                    MissingValueMethod.KNearest => double.NaN,   // per cell, below
                    _ => positive / 5,
                };
            }
            for (var i = 0; i < n; i++)
            {
                if (!double.IsNaN(values[i, j])) continue;
                values[i, j] = method == MissingValueMethod.KNearest ? KNearestFill(table, i, j, 5, fill) : fill;
                imputed++;
            }
        }
        return (table.With(values), imputed);
    }

    /// <summary>The mean, in this injection, of the k features whose profiles over the other injections agree best with this one's.</summary>
    private static double KNearestFill(AnalysisTable table, int sample, int feature, int k, double fallback)
    {
        var n = table.SampleCount;
        var target = table.Column(feature);
        var candidates = new List<(double Correlation, double Value)>();
        for (var other = 0; other < table.FeatureCount; other++)
        {
            if (other == feature) continue;
            var value = table.Values[sample, other];
            if (double.IsNaN(value)) continue;
            var column = table.Column(other);
            var xs = new List<double>();
            var ys = new List<double>();
            for (var i = 0; i < n; i++)
            {
                if (i == sample || double.IsNaN(target[i]) || double.IsNaN(column[i])) continue;
                xs.Add(target[i]);
                ys.Add(column[i]);
            }
            if (xs.Count < 3) continue;
            var r = Correlations.Pearson(xs, ys);
            if (!double.IsNaN(r)) candidates.Add((r, value));
        }
        if (candidates.Count == 0) return double.IsNaN(fallback) ? 0 : fallback;
        var nearest = candidates.OrderByDescending(c => c.Correlation).Take(k).ToList();
        // the neighbours are on their own scale; bring their value across by the ratio of means
        double sum = 0;
        var count = 0;
        foreach (var (_, value) in nearest) { sum += value; count++; }
        var mean = sum / count;
        var targetMean = target.Where(v => !double.IsNaN(v)).DefaultIfEmpty(mean).Average();
        var neighbourMean = nearest.Select(c => c.Value).Average();
        return neighbourMean <= 0 ? mean : mean * targetMean / neighbourMean;
    }

    // ------------------------------------------------------------------ filters

    /// <summary>
    /// MetaboAnalyst's rule of thumb for how many features the filter removes when no share is
    /// given: none below 250 features, 5 % up to 500, 10 % up to 1000, 25 % beyond.
    /// </summary>
    public static double DefaultFilterFraction(int features) => features switch
    {
        < 250 => 0,
        < 500 => 0.05,
        < 1000 => 0.10,
        _ => 0.25,
    };

    public static (AnalysisTable Table, int Dropped) Filter(AnalysisTable table, FilterMethod method, double fraction)
    {
        if (method == FilterMethod.None || table.FeatureCount == 0) return (table, 0);
        if (double.IsNaN(fraction)) fraction = DefaultFilterFraction(table.FeatureCount);
        var remove = (int)Math.Floor(table.FeatureCount * Math.Clamp(fraction, 0, 0.95));
        if (remove <= 0) return (table, 0);
        var scores = new double[table.FeatureCount];
        for (var j = 0; j < table.FeatureCount; j++)
        {
            var column = table.Column(j).Where(v => !double.IsNaN(v)).OrderBy(v => v).ToList();
            scores[j] = column.Count == 0 ? 0 : method switch
            {
                FilterMethod.InterquartileRange => Quantile(column, 0.75) - Quantile(column, 0.25),
                FilterMethod.StandardDeviation => StandardDeviation(column),
                FilterMethod.MedianAbsoluteDeviation => MedianAbsoluteDeviation(column),
                FilterMethod.RelativeStandardDeviation => column.Average() > 0 ? StandardDeviation(column) / column.Average() : 0,
                FilterMethod.MeanIntensity => column.Average(),
                FilterMethod.MedianIntensity => Median(column),
                _ => 0,
            };
        }
        var keep = Enumerable.Range(0, table.FeatureCount).OrderByDescending(j => scores[j]).Take(table.FeatureCount - remove).OrderBy(j => j).ToList();
        return (table.Select(keep), remove);
    }

    /// <summary>Drops the features whose relative standard deviation across the QC injections exceeds the limit.</summary>
    public static (AnalysisTable Table, int Dropped) FilterByControlRsd(AnalysisTable table, double maxPercent)
    {
        var controls = Enumerable.Range(0, table.SampleCount).Where(i => table.Samples[i].IsQualityControl).ToList();
        if (controls.Count < 2) return (table, 0);
        var keep = new List<int>();
        for (var j = 0; j < table.FeatureCount; j++)
        {
            var values = controls.Select(i => table.Values[i, j]).Where(v => !double.IsNaN(v)).ToList();
            if (values.Count < 2) { keep.Add(j); continue; }
            var mean = values.Average();
            var rsd = mean > 0 ? 100 * StandardDeviation(values) / mean : double.PositiveInfinity;
            if (rsd <= maxPercent) keep.Add(j);
        }
        return (table.Select(keep), table.FeatureCount - keep.Count);
    }

    // ------------------------------------------------------------------ normalisation

    public static AnalysisTable Normalize(AnalysisTable table, SampleNormalization method, int? referenceFeatureId)
    {
        if (method == SampleNormalization.None) return table;
        var n = table.SampleCount;
        var p = table.FeatureCount;
        var values = (double[,])table.Values.Clone();
        var factors = new double[n];
        switch (method)
        {
            case SampleNormalization.Sum:
                for (var i = 0; i < n; i++) factors[i] = Row(values, i).Where(v => !double.IsNaN(v)).Sum();
                break;
            case SampleNormalization.Median:
                for (var i = 0; i < n; i++) factors[i] = Median(Row(values, i).Where(v => !double.IsNaN(v)).ToList());
                break;
            case SampleNormalization.ReferenceFeature:
            {
                var j = referenceFeatureId is null ? -1 : table.Features.ToList().FindIndex(f => f.Id == referenceFeatureId);
                if (j < 0) return table;
                for (var i = 0; i < n; i++) factors[i] = values[i, j];
                break;
            }
            case SampleNormalization.ProbabilisticQuotient:
            case SampleNormalization.ProbabilisticQuotientToControls:
            {
                // the reference profile: the median of every feature over the batch (or the controls);
                // each injection is divided by the median of its quotients against it
                var reference = new double[p];
                var rows = method == SampleNormalization.ProbabilisticQuotientToControls
                    ? Enumerable.Range(0, n).Where(i => table.Samples[i].IsQualityControl).ToList()
                    : Enumerable.Range(0, n).ToList();
                if (rows.Count == 0) rows = Enumerable.Range(0, n).ToList();
                // integral normalisation first, as the method prescribes
                var sums = new double[n];
                for (var i = 0; i < n; i++) sums[i] = Row(values, i).Where(v => !double.IsNaN(v)).Sum();
                var meanSum = sums.Where(s => s > 0).DefaultIfEmpty(1).Average();
                for (var i = 0; i < n; i++)
                    for (var j = 0; j < p; j++)
                        if (sums[i] > 0) values[i, j] = values[i, j] / sums[i] * meanSum;
                for (var j = 0; j < p; j++) reference[j] = Median(rows.Select(i => values[i, j]).Where(v => !double.IsNaN(v)).ToList());
                for (var i = 0; i < n; i++)
                {
                    var quotients = new List<double>();
                    for (var j = 0; j < p; j++) if (!double.IsNaN(values[i, j]) && reference[j] > 0) quotients.Add(values[i, j] / reference[j]);
                    factors[i] = quotients.Count == 0 ? 1 : Median(quotients);
                }
                break;
            }
        }
        // rescale so the numbers keep their size: divide by the factor, multiply by the mean factor
        var meanFactor = factors.Where(f => f > 0 && !double.IsNaN(f)).DefaultIfEmpty(1).Average();
        for (var i = 0; i < n; i++)
        {
            if (double.IsNaN(factors[i]) || factors[i] <= 0) continue;
            for (var j = 0; j < p; j++) values[i, j] = values[i, j] / factors[i] * meanFactor;
        }
        return table.With(values, table.ValueName + ", " + Describe(method));
    }

    // ------------------------------------------------------------------ transformation

    public static AnalysisTable Transform(AnalysisTable table, Transformation transform)
    {
        if (transform == Transformation.None) return table;
        var n = table.SampleCount;
        var p = table.FeatureCount;
        var values = (double[,])table.Values.Clone();
        for (var j = 0; j < p; j++)
        {
            // a log needs a floor: the smallest positive value of the feature over ten, as MetaboAnalyst does
            var positive = double.MaxValue;
            for (var i = 0; i < n; i++) if (!double.IsNaN(values[i, j]) && values[i, j] > 0 && values[i, j] < positive) positive = values[i, j];
            var floor = positive == double.MaxValue ? 1 : positive / 10;
            for (var i = 0; i < n; i++)
            {
                var v = values[i, j];
                if (double.IsNaN(v)) continue;
                values[i, j] = transform switch
                {
                    Transformation.Log10 => Math.Log10(Math.Max(v, floor)),
                    Transformation.Log2 => Math.Log2(Math.Max(v, floor)),
                    Transformation.NaturalLog => Math.Log(Math.Max(v, floor)),
                    Transformation.SquareRoot => Math.Sqrt(Math.Max(v, 0)),
                    Transformation.CubeRoot => Math.Cbrt(v),
                    _ => v,
                };
            }
        }
        return table.With(values, Describe(transform) + " of " + table.ValueName);
    }

    // ------------------------------------------------------------------ helpers

    private static IEnumerable<double> Row(double[,] values, int i)
    {
        for (var j = 0; j < values.GetLength(1); j++) yield return values[i, j];
    }

    public static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return double.NaN;
        var sorted = values.OrderBy(v => v).ToList();
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }

    /// <summary>The quantile of an already sorted list, by linear interpolation.</summary>
    public static double Quantile(IReadOnlyList<double> sorted, double q)
    {
        if (sorted.Count == 0) return double.NaN;
        var position = (sorted.Count - 1) * q;
        var low = (int)Math.Floor(position);
        var high = Math.Min(sorted.Count - 1, low + 1);
        return sorted[low] + (sorted[high] - sorted[low]) * (position - low);
    }

    public static double StandardDeviation(IReadOnlyList<double> values)
    {
        if (values.Count < 2) return 0;
        var mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (values.Count - 1));
    }

    private static double MedianAbsoluteDeviation(IReadOnlyList<double> values)
    {
        var median = Median(values);
        return Median(values.Select(v => Math.Abs(v - median)).ToList());
    }
}
