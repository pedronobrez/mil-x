using CompMs.MsdialCore.DataObj;
using OpenDIAL.Pipeline.Curation;
using OpenDIAL.Pipeline.Results;
using OpenDIAL.Pipeline.Statistics;
using Xunit;
using Xunit.Abstractions;

namespace OpenDIAL.Pipeline.Tests;

/// <summary>
/// The whole one-factor analysis over a real result that carries a real review: the alignment
/// MS-DIAL produced on Windows, with the features its reviewer confirmed. Runs only when
/// OPENDIAL_TEST_REVIEWED_ALIGNMENT points at that .arf2, and only reads it.
/// </summary>
public class OneFactorRealDataTests
{
    private readonly ITestOutputHelper _output;
    public OneFactorRealDataTests(ITestOutputHelper output) => _output = output;

    private static string? Source =>
        Environment.GetEnvironmentVariable("OPENDIAL_TEST_REVIEWED_ALIGNMENT") is { Length: > 0 } p && File.Exists(p) ? p : null;

    [Fact]
    public void Confirmed_analytes_as_ratios_to_their_standards_go_through_the_whole_analysis()
    {
        var source = Source;
        if (source is null)
        {
            Console.WriteLine("skipped: set OPENDIAL_TEST_REVIEWED_ALIGNMENT to a reviewed alignment .arf2");
            return;
        }
        var beanName = Path.GetFileName(source);
        if (beanName.EndsWith(".arf2", StringComparison.OrdinalIgnoreCase)) beanName = beanName[..^1];
        var bean = new AlignmentFileBean { FilePath = Path.Combine(Path.GetDirectoryName(source)!, beanName), FileName = Path.GetFileNameWithoutExtension(source) };
        var container = AlignmentResultContainer.Load(bean);
        var table = ResultLoader.LoadAlignmentTableAsync(bean, Array.Empty<AnalysisFileBean>(), container).GetAwaiter().GetResult();
        var store = CurationStore.Load(bean.FilePath);
        var confirmed = table.Spots.Where(s => store.HasTag(s.Id, PeakSpotTagKind.Confirmed)).ToList();
        _output.WriteLine($"{table.Spots.Count} features, {confirmed.Count} confirmed, {table.Samples.Count} injections");
        Assert.True(confirmed.Count >= 20, "the reviewed alignment should carry confirmed features");

        // the classes as the Windows project declared them are not in the container alone; take
        // them from the injection names, the way the samples workspace would have them
        var samples = table.Spots[0].SamplePeaks.Select((p, i) => new SampleInfo(p.FileId, p.FileName,
            p.FileName.Contains("BK", StringComparison.OrdinalIgnoreCase) ? "blank"
            : p.FileName.Contains("Eq", StringComparison.OrdinalIgnoreCase) || p.FileName.Contains("Mix", StringComparison.OrdinalIgnoreCase) ? "standard" : "liver",
            p.FileName.Contains("Eq", StringComparison.OrdinalIgnoreCase) ? "QC" : "Sample", i + 1)).ToList();

        var classes = RelativeAbundance.ClassesWithConfirmed(confirmed);
        _output.WriteLine("classes with confirmed analytes: " + string.Join(", ", classes.Select(c => $"{c.Class} ({c.Confirmed})")));
        var assignments = RelativeAbundance.Suggest(confirmed);
        foreach (var a in assignments)
        {
            var standard = a.StandardFeatureId is null ? "—" : confirmed.First(f => f.Id == a.StandardFeatureId).Name;
            _output.WriteLine($"  {a.Class}: {standard}");
        }
        var (ratios, report) = RelativeAbundance.Build(confirmed, samples, assignments, useArea: true);
        _output.WriteLine(report.Message);
        Assert.True(ratios.FeatureCount > 10);

        var data = Preprocessing.Run(ratios, new PreprocessingOptions { Filter = FilterMethod.None, MaxMissingFraction = 0.5, Transform = Transformation.Log10 });
        _output.WriteLine(data.Report.Summary);

        var comparison = Univariate.Compare(data.Normalized, data.Transformed, "liver", "blank");
        _output.WriteLine(comparison.Message);
        foreach (var row in comparison.Features.OrderBy(r => r.P).Take(8))
        {
            _output.WriteLine($"  {row.Label,-32} FC {row.FoldChange,10:F1}  p {row.P:E2}  FDR {row.AdjustedP:E2}");
        }
        Assert.True(comparison.Features.Count(r => r.AdjustedP <= 0.05) > 0, "liver against blank should show something");

        var significant = comparison.Features.Where(r => r.AdjustedP <= 0.05 && Math.Abs(r.Log2FoldChange) >= 1).Select(r => r.FeatureId).ToHashSet();
        var enrichment = Enrichment.OverRepresentation(data.Transformed.Features, significant);
        _output.WriteLine(enrichment.Message);
        foreach (var row in enrichment.Rows.Take(6))
        {
            _output.WriteLine($"  {row.Set,-22} {row.Kind,-12} {row.Hits}/{row.SetSize} expected {row.Expected:F1} ratio {row.EnrichmentRatio:F1} p {row.P:E2}");
        }
        var changes = Enrichment.ClassChanges(comparison.Features, data.Transformed.Features);
        foreach (var c in changes) _output.WriteLine($"  {c.Class,-10} {c.Members} members, mean log2 FC {c.MeanLog2FoldChange:F2}, {c.Up} up {c.Down} down, p {c.P:E2}");

        var forest = RandomForest.Compute(data.Scaled, trees: 300);
        _output.WriteLine(forest.Message);
        _output.WriteLine("  top by importance: " + string.Join(", ", forest.Importance.Take(5).Select(i => $"{i.Label} ({i.MeanDecreaseAccuracy:F3})")));

        var top = comparison.Features.OrderBy(r => r.P).Take(25).Select(r => data.Transformed.Features.ToList().FindIndex(f => f.Id == r.FeatureId)).Where(j => j >= 0).ToList();
        var heatmap = Clustering.Heatmap(data.Transformed, top, DistanceKind.Pearson, LinkageKind.Average, true, true);
        _output.WriteLine("heatmap column order: " + string.Join(", ", heatmap.ColumnLabels.Select((l, i) => $"{l} [{heatmap.ColumnGroups[i]}]")));
        var kmeans = Clustering.KMeans(data.Scaled, 2);
        _output.WriteLine("k-means: " + string.Join(", ", kmeans.Labels.Select((l, i) => $"{l}→{kmeans.Assignment[i]}")));

        var pca = Pca.Compute(data.Scaled, 3);
        _output.WriteLine($"PCA explained: {string.Join(", ", pca.ExplainedVariance.Select(v => v.ToString("F1")))}");
        var pls = PartialLeastSquares.Compute(data.Scaled, 2, 100);
        _output.WriteLine($"PLS-DA: R2Y {pls.R2Y:F2} Q2 {pls.Q2:F2} p {pls.PermutationP:F3}");

        // the pathway analysis on the ratios: liver against blank, at every level
        foreach (var level in new[] { Statistics.Pathways.PathwayLevel.Class, Statistics.Pathways.PathwayLevel.Species, Statistics.Pathways.PathwayLevel.FattyAcid })
        {
            var pathways = Statistics.Pathways.LipidPathways.Compute(data.Normalized, "liver", "blank", level);
            _output.WriteLine($"pathways ({level}): {pathways.Message}");
            _output.WriteLine("  nodes: " + string.Join(", ", pathways.Nodes.Take(12).Select(n => $"{n.Name} ({n.Members})")));
            foreach (var r in pathways.Reactions.Where(r => r.Tested).OrderByDescending(r => r.AbsZ).Take(8))
                _output.WriteLine($"  {r.Label,-34} log2 {r.Log2Change,6:F2}  p {r.P:E2}  Z {r.Z,6:F2}  {r.Status,-10} {r.GeneText}");
            foreach (var p in pathways.Pathways.Take(5))
                _output.WriteLine($"  path {p.Chain,-40} Z {p.Z,6:F2} {p.Status}");
            if (level == Statistics.Pathways.PathwayLevel.Class)
            {
                // PC, PE, LPC, SM and CAR go through as ratios; Cer and LPE had only their standard confirmed, which divides itself out
                Assert.True(pathways.Tested.Count >= 3, "PE → PC, PC → LPC and LPC → PC are testable");
                Assert.Contains(pathways.Reactions, r => r.Id == "PEMT" && r.Tested);
                Assert.Contains(pathways.Reactions, r => r.Id == "PLA2-PC" && r.Tested);
                Assert.Contains(pathways.Predicted, p => p.Missing == "PS");
                Assert.Contains(pathways.Predicted, p => p.Reaction == "Cer → SM" && p.Missing == "Cer");
            }
        }
    }
}
