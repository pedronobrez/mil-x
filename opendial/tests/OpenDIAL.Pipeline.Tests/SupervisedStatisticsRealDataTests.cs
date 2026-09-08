using CompMs.MsdialCore.DataObj;
using OpenDIAL.Pipeline.Results;
using OpenDIAL.Pipeline.Statistics;
using Xunit;
using Xunit.Abstractions;

namespace OpenDIAL.Pipeline.Tests;

/// <summary>
/// The supervised model and the drift correction against a real alignment rather than a fixture.
/// Runs only when OPENDIAL_TEST_ALIGNMENT points at an alignment .arf2, and only reads it.
///
/// The real result carries no class design and no controls, so the labels here are made up. That is
/// the point of the first check: made-up labels on real data must not survive cross-validation. The
/// second asks the correction to tighten the spread over injections that stand in as controls.
/// </summary>
public class SupervisedStatisticsRealDataTests
{
    private readonly ITestOutputHelper _output;
    public SupervisedStatisticsRealDataTests(ITestOutputHelper output) => _output = output;

    private static string? Source =>
        Environment.GetEnvironmentVariable("OPENDIAL_TEST_ALIGNMENT") is { Length: > 0 } p && File.Exists(p) ? p : null;

    [Fact]
    public void A_real_alignment_refuses_labels_that_mean_nothing_and_takes_a_drift_correction()
    {
        var source = Source;
        if (source is null)
        {
            Console.WriteLine("skipped: set OPENDIAL_TEST_ALIGNMENT to an alignment .arf2");
            return;
        }

        var beanName = Path.GetFileName(source);
        if (beanName.EndsWith(".arf2", StringComparison.OrdinalIgnoreCase)) beanName = beanName[..^1];
        var bean = new AlignmentFileBean
        {
            FilePath = Path.Combine(Path.GetDirectoryName(source)!, beanName),
            FileName = Path.GetFileNameWithoutExtension(source),
        };
        var container = AlignmentResultContainer.Load(bean);
        Assert.NotNull(container);

        var table = ResultLoader.LoadAlignmentTableAsync(bean, Array.Empty<AnalysisFileBean>(), container).GetAwaiter().GetResult();
        var features = table.Spots.Where(s => s.SamplePeaks.Count(p => p.Height > 0) == table.Spots[0].SamplePeaks.Count).ToList();
        var names = table.Spots[0].SamplePeaks.Select(p => (p.FileId, p.FileName)).OrderBy(x => x.FileId).ToList();
        _output.WriteLine($"{table.Spots.Count} features, {features.Count} present in every injection, {names.Count} injections");
        foreach (var (id, name) in names) _output.WriteLine($"  {id}: {name}");
        if (names.Count < 6 || features.Count < 50)
        {
            _output.WriteLine("too small for a supervised model; nothing asserted");
            return;
        }

        // The design that is really there: the liver injections against the blanks and the
        // equilibration. This one should hold up, and it is what says the model works on real data.
        var real = names
            .Select((x, i) => new SampleInfo(
                x.FileId,
                x.FileName,
                x.FileName.Contains("BK", StringComparison.OrdinalIgnoreCase)
                || x.FileName.Contains("Eq", StringComparison.OrdinalIgnoreCase)
                || x.FileName.Contains("Mix", StringComparison.OrdinalIgnoreCase) ? "blank" : "liver",
                "Sample",
                i + 1))
            .ToList();
        var design = PartialLeastSquares.Compute(DataMatrix.Build(features, real), 2, 200);
        _output.WriteLine($"[liver against blank] R2Y {design.R2Y:F2}, Q2 {design.Q2:F2}, p {design.PermutationP:F3}, {design.CorrectlyClassified}/{real.Count} classified back");
        _output.WriteLine("  top by VIP: " + string.Join(", ", design.Loadings.OrderByDescending(l => l.Vip).Take(5).Select(l => $"{l.Label} ({l.Vip:F2})")));
        Assert.True(design.Q2 >= 0.4, $"a real difference should survive cross-validation; Q2 {design.Q2:F2}");
        Assert.True(design.PermutationP <= 0.05, $"and beat shuffled labels; p {design.PermutationP:F3}");

        // the same design through the orthogonal rotation: it should agree, and say so more tidily
        var orthogonal = OrthogonalProjection.Compute(DataMatrix.Build(features, real), 1, 200);
        _output.WriteLine($"[orthogonal] R2Y {orthogonal.R2Y:F2}, Q2 {orthogonal.Q2:F2}, p {orthogonal.PermutationP:F3}, "
            + $"{orthogonal.PredictiveVarianceX:F1} % of X predictive against {orthogonal.OrthogonalVarianceX:F1} % stripped out, "
            + $"{orthogonal.CorrectlyClassified}/{real.Count} classified back");
        _output.WriteLine("  corners of the S-plot: " + string.Join(", ", orthogonal.Loadings
            .Where(l => Math.Abs(l.Correlation) > 0.9)
            .OrderByDescending(l => Math.Abs(l.Covariance))
            .Take(5)
            .Select(l => $"{l.Label} (cov {l.Covariance:F2}, r {l.Correlation:F2}, {l.Side})")));
        Assert.True(orthogonal.Q2 >= 0.4, $"the orthogonal model should hold up too; Q2 {orthogonal.Q2:F2}");
        Assert.Equal(real.Count, orthogonal.CorrectlyClassified);

        // Labels with nothing behind them: the first replicate of everything against the second,
        // which puts liver, blank and mix on both sides and so describes no chemistry at all.
        var invented = names
            .Select((x, i) => new SampleInfo(x.FileId, x.FileName, i % 2 == 0 ? "left" : "right", "Sample", i + 1))
            .ToList();
        var noise = PartialLeastSquares.Compute(DataMatrix.Build(features, invented), 2, 200);
        _output.WriteLine($"[invented labels] R2Y {noise.R2Y:F2}, Q2 {noise.Q2:F2}, p {noise.PermutationP:F3}, {noise.CorrectlyClassified}/{invented.Count} classified back");
        Assert.True(noise.R2Y > 0.5, "the fit itself will always separate them");
        Assert.False(noise.Q2 >= 0.4 && noise.PermutationP <= 0.05,
            $"invented labels on real data must not pass both checks; Q2 {noise.Q2:F2}, p {noise.PermutationP:F3}");

        // every third injection stands in as a control, over two batches
        var standIns = names
            .Select((x, i) => new SampleInfo(x.FileId, x.FileName, "pool", i % 3 == 0 ? "QC" : "Sample", i + 1, i < names.Count / 2 ? 1 : 2))
            .ToList();
        var corrected = BatchCorrection.Apply(features, standIns);
        _output.WriteLine($"[correction] {corrected.Message}");
        _output.WriteLine($"[correction] median CV over the stand-in controls {corrected.MedianCvBefore:F1} % before, {corrected.MedianCvAfter:F1} % after");
        Assert.True(corrected.Corrected > 0, "with controls in both batches every feature should be correctable");
        Assert.True(corrected.MedianCvAfter < corrected.MedianCvBefore,
            $"the spread should tighten: {corrected.MedianCvBefore:F1} % before, {corrected.MedianCvAfter:F1} % after");
    }
}
