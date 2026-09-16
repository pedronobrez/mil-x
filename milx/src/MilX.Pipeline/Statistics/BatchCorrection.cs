using MilX.Pipeline.Results;

namespace MilX.Pipeline.Statistics;

/// <summary>How a feature's response was corrected across the run.</summary>
public sealed record FeatureCorrection(int FeatureId, string Label, double CvBefore, double CvAfter, bool Corrected);

public sealed record BatchCorrectionResult(
    IReadOnlyList<FeatureCorrection> Features,
    double MedianCvBefore,
    double MedianCvAfter,
    int Corrected,
    int Skipped,
    int QualityControls,
    int Batches,
    string Message)
{
    /// <summary>Corrected values, [sample, feature], in the order of the samples given.</summary>
    public double[,] Values { get; init; } = new double[0, 0];
}

/// <summary>
/// Corrects the drift a long sequence puts into every response, against the quality-control
/// injections that were run through it.
///
/// The instrument's response falls as the source fouls and jumps between batches, so the same
/// compound is not the same number at injection 5 and injection 80. The quality controls are the
/// same material every time, so whatever they do across the sequence is the instrument, not the
/// biology: fit that, divide it out, and what is left is comparable. This is the locally weighted
/// signal correction the metabolomics literature calls QC-RLSC.
///
/// It is measured by what it is for: the spread of the quality controls, before and after.
/// </summary>
public static class BatchCorrection
{
    public static BatchCorrectionResult Apply(
        IReadOnlyList<AlignmentSpotRow> features,
        IReadOnlyList<SampleInfo> samples,
        bool useArea = false,
        double span = 0.75,
        int minQualityControls = 3)
    {
        ArgumentNullException.ThrowIfNull(features);
        ArgumentNullException.ThrowIfNull(samples);
        var n = samples.Count;
        var p = features.Count;
        var values = new double[n, p];
        if (n == 0 || p == 0)
        {
            return new BatchCorrectionResult(Array.Empty<FeatureCorrection>(), 0, 0, 0, 0, 0, 0, "Nothing to correct.") { Values = values };
        }

        // the order the injections were run in; when it was never filled in, the file order is it
        var order = samples.Select((s, i) => s.InjectionOrder > 0 ? s.InjectionOrder : i + 1).ToArray();
        var batches = samples.Select(s => s.Batch).ToArray();
        var isQc = samples.Select(s => s.IsQualityControl).ToArray();
        var qcCount = isQc.Count(x => x);
        var batchCount = batches.Distinct().Count();

        for (var j = 0; j < p; j++)
        {
            for (var i = 0; i < n; i++)
            {
                var peak = features[j].SamplePeaks.FirstOrDefault(x => x.FileId == samples[i].FileId);
                var v = peak is null ? double.NaN : useArea ? peak.Area : peak.Height;
                values[i, j] = double.IsNaN(v) ? 0 : v;
            }
        }

        if (qcCount < minQualityControls)
        {
            var untouched = features.Select((f, j) => new FeatureCorrection(f.Id, Label(f), Cv(values, j, isQc), Cv(values, j, isQc), false)).ToList();
            return new BatchCorrectionResult(untouched, Median(untouched.Select(f => f.CvBefore)), Median(untouched.Select(f => f.CvAfter)),
                0, p, qcCount,
                batchCount,
                $"Needs at least {minQualityControls} injections marked QC in the Samples workspace; this batch has {qcCount}.")
            { Values = values };
        }

        var results = new List<FeatureCorrection>(p);
        var corrected = 0;
        var skipped = 0;
        for (var j = 0; j < p; j++)
        {
            var before = Cv(values, j, isQc);
            var didCorrect = CorrectFeature(values, j, order, batches, isQc, span);
            var after = didCorrect ? Cv(values, j, isQc) : before;
            if (didCorrect) corrected++; else skipped++;
            results.Add(new FeatureCorrection(features[j].Id, Label(features[j]), before, after, didCorrect));
        }

        return new BatchCorrectionResult(
            results,
            Median(results.Select(r => r.CvBefore)),
            Median(results.Select(r => r.CvAfter)),
            corrected,
            skipped,
            qcCount,
            batchCount,
            $"{corrected} of {p} feature(s) corrected against {qcCount} quality control(s) across {batchCount} batch(es).")
        { Values = values };
    }

    private static string Label(AlignmentSpotRow f) =>
        f.IsAnnotated ? f.Name : $"m/z {f.Mz:F4} @ {f.Rt:F2}";

    /// <summary>
    /// Divides one feature by the smooth curve its quality controls trace across the sequence, batch
    /// by batch, rescaled so the corrected values keep the size of the originals.
    /// </summary>
    private static bool CorrectFeature(double[,] values, int j, int[] order, int[] batches, bool[] isQc, double span)
    {
        var n = order.Length;
        var overallQcMean = 0.0;
        var overallQcCount = 0;
        for (var i = 0; i < n; i++)
        {
            if (!isQc[i] || values[i, j] <= 0) continue;
            overallQcMean += values[i, j];
            overallQcCount++;
        }
        if (overallQcCount < 3) return false;
        overallQcMean /= overallQcCount;
        if (overallQcMean <= 0) return false;

        var any = false;
        foreach (var batch in batches.Distinct())
        {
            var members = Enumerable.Range(0, n).Where(i => batches[i] == batch).OrderBy(i => order[i]).ToList();
            var qcIndices = members.Where(i => isQc[i] && values[i, j] > 0).ToList();
            if (qcIndices.Count == 0) continue;

            if (qcIndices.Count < 3)
            {
                // Not enough controls in this batch to trace a trend, but enough to say what the
                // batch as a whole was running at: level it to the others with one factor.
                var level = qcIndices.Average(i => values[i, j]);
                if (level <= 0) continue;
                foreach (var i in members) values[i, j] = values[i, j] / level * overallQcMean;
                any = true;
                continue;
            }

            var qcX = qcIndices.Select(i => (double)order[i]).ToArray();
            var qcY = qcIndices.Select(i => values[i, j]).ToArray();
            foreach (var i in members)
            {
                var fitted = Loess(qcX, qcY, order[i], span);
                if (fitted <= 0) continue;
                values[i, j] = values[i, j] / fitted * overallQcMean;
            }
            any = true;
        }
        return any;
    }

    /// <summary>
    /// Locally weighted linear fit at one point: the nearest fraction of the quality controls, each
    /// weighted by how close it is. With few of them it degrades to their mean, which is the right
    /// answer when there is not enough evidence for a trend.
    /// </summary>
    internal static double Loess(double[] x, double[] y, double at, double span)
    {
        var n = x.Length;
        if (n == 0) return 0;
        if (n < 3) return y.Average();
        var take = Math.Max(3, (int)Math.Ceiling(Math.Clamp(span, 0.1, 1.0) * n));
        take = Math.Min(take, n);

        var neighbours = Enumerable.Range(0, n).OrderBy(i => Math.Abs(x[i] - at)).Take(take).ToArray();
        var maxDistance = neighbours.Max(i => Math.Abs(x[i] - at));
        if (maxDistance <= 0) return neighbours.Average(i => y[i]);

        double sw = 0, swx = 0, swy = 0, swxx = 0, swxy = 0;
        foreach (var i in neighbours)
        {
            var u = Math.Abs(x[i] - at) / maxDistance;
            var w = Math.Pow(1 - u * u * u, 3);   // tricube
            if (w <= 0) continue;
            sw += w;
            swx += w * x[i];
            swy += w * y[i];
            swxx += w * x[i] * x[i];
            swxy += w * x[i] * y[i];
        }
        if (sw <= 0) return y.Average();
        var denominator = sw * swxx - swx * swx;
        if (Math.Abs(denominator) < 1e-12) return swy / sw;
        var slope = (sw * swxy - swx * swy) / denominator;
        var intercept = (swy - slope * swx) / sw;
        return intercept + slope * at;
    }

    /// <summary>Coefficient of variation of the quality controls, in percent: the number this is judged by.</summary>
    private static double Cv(double[,] values, int j, bool[] isQc)
    {
        var samples = new List<double>();
        for (var i = 0; i < isQc.Length; i++)
        {
            if (isQc[i] && values[i, j] > 0) samples.Add(values[i, j]);
        }
        if (samples.Count < 2) return double.NaN;
        var mean = samples.Average();
        if (mean <= 0) return double.NaN;
        var sd = Math.Sqrt(samples.Sum(v => (v - mean) * (v - mean)) / (samples.Count - 1));
        return 100.0 * sd / mean;
    }

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToList();
        return sorted.Count == 0 ? double.NaN : sorted[sorted.Count / 2];
    }
}
