using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpenDIAL.Desktop.Charts;
using OpenDIAL.Desktop.Controls;
using OpenDIAL.Desktop.ViewModels;
using OpenDIAL.Desktop.Views;
using OpenDIAL.Pipeline.Curation;
using OpenDIAL.Pipeline.Model;
using OpenDIAL.Pipeline.Results;
using OpenDIAL.Pipeline.Statistics;
using Xunit;

namespace OpenDIAL.Desktop.Tests;

/// <summary>
/// The one-factor analysis as the reviewer meets it: a reviewed result with confirmed lipids and
/// their standards, through the preprocessing, into the tests and the plots, and out as pictures.
/// </summary>
public class OneFactorTests
{
    /// <summary>
    /// Twelve injections in two classes, with a curation that confirms sixteen lipids in two classes,
    /// one odd-chain standard in each; half of the PCs differ threefold between the classes.
    /// </summary>
    internal static (StatisticsViewModel Vm, CurationStore Curation, IReadOnlyList<AlignmentSpotRow> Spots) Reviewed(int classes = 2)
    {
        var samples = new List<SampleInfo>();
        var names = new[] { "treated", "control", "vehicle" };
        var per = 12 / classes;
        for (var i = 0; i < 12; i++)
            samples.Add(new SampleInfo(i, names[i / per][0].ToString().ToUpperInvariant() + (i % per + 1), names[i / per], "Sample", InjectionOrder: i + 1));

        var folder = Path.Combine(Path.GetTempPath(), "opendial-onefactor-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var curation = CurationStore.Load(Path.Combine(folder, "AlignmentResult.arf2"));

        var rng = new Random(11);
        var spots = new List<AlignmentSpotRow>();
        string NameOf(int k) => k switch
        {
            0 => "PC 33:1",                       // the odd-chain standard of the PCs
            < 10 => $"PC {32 + 2 * k}:{k % 4}",
            10 => "PE 17:0_17:0",                 // and of the PEs
            < 16 => $"PE {34 + 2 * (k - 10)}:{k % 3}",
            _ => "Unknown",
        };
        for (var k = 0; k < 40; k++)
        {
            var confirmed = k < 16;
            var separates = confirmed && k is > 0 and < 6;   // five PCs carry the difference
            var level = 4000.0 * (k + 1);
            var heights = new double[samples.Count];
            for (var i = 0; i < samples.Count; i++)
            {
                var fold = separates && samples[i].Class == "treated" ? 3.0 : 1.0;
                heights[i] = level * fold * (0.9 + rng.NextDouble() * 0.2);
            }
            var name = NameOf(k);
            var row = new AlignmentSpotRow
            {
                Id = k,
                Name = name,
                Ontology = name.StartsWith("PC") ? "PC" : name.StartsWith("PE") ? "PE" : string.Empty,
                Mz = 700 + k,
                Rt = 5 + k * 0.1,
                AverageHeight = heights.Average(),
                SamplePeaks = samples.Select((s, i) => new AlignedSamplePeak(s.FileId, s.FileName, s.Class, s.SampleType, 5, 4.9, 5.1, 700 + k, heights[i], heights[i] * 10, 20, false)).ToList(),
            };
            spots.Add(row);
            if (confirmed) curation.SetTag(k, PeakSpotTagKind.Confirmed, true);
        }

        var vm = new StatisticsViewModel(new NoDialogs());
        var session = new ResultSession(folder, Array.Empty<CompMs.MsdialCore.DataObj.AnalysisFileBean>(), null, null, null, Array.Empty<ExportedFile>(), null, IonizationMode.LCMS);
        vm.Load(session, spots, samples, curation);
        return (vm, curation, spots);
    }

    private sealed class NoDialogs : Services.IFileDialogService
    {
        public Task<IReadOnlyList<string>> PickFilesAsync(string title, IReadOnlyList<string> patterns, bool allowMultiple, string? startFolder = null)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<string?> PickFolderAsync(string title, string? startFolder = null) => Task.FromResult<string?>(null);
        public Task<string?> SaveFileAsync(string title, string suggestedName, string extension, string? startFolder = null) => Task.FromResult<string?>(null);
    }

    [AvaloniaFact]
    public void A_reviewed_result_opens_as_ratios_to_the_suggested_standards()
    {
        var (vm, _, _) = Reviewed();
        var a = vm.Analysis;

        Assert.Equal(OneFactorViewModel.SourceModes[0], a.SourceMode);
        Assert.Equal(16, a.ConfirmedCount);
        Assert.Contains("PC ÷ PC 33:1", a.StandardsSummary);
        Assert.Contains("PE ÷ PE 17:0_17:0", a.StandardsSummary);

        // the two standards divide themselves out, so fourteen analytes go through
        Assert.NotNull(a.Data);
        Assert.Equal(14, a.Data!.Raw.FeatureCount);
        Assert.Equal(12, a.Data.Raw.SampleCount);
        Assert.Contains("ratio", a.Data.Raw.ValueName, StringComparison.OrdinalIgnoreCase);

        // the before-and-after boxes: one per injection, and twenty features (or all fourteen)
        Assert.Equal(12, a.BeforeBoxes.Count);
        Assert.Equal(12, a.AfterBoxes.Count);
        Assert.Equal(14, a.BeforeFeatureBoxes.Count);
        Assert.All(a.AfterBoxes, b => Assert.Equal(14, b.Values.Count));

        // and the models downstream read the same fourteen
        Assert.Equal(14, vm.Loadings.Count);
        Assert.Equal(12, vm.Scores.Count);
        Assert.Equal(2, a.Classes.Count);
    }

    [AvaloniaFact]
    public void Changing_the_source_reruns_everything_that_reads_it()
    {
        var (vm, _, _) = Reviewed();
        var recomputed = 0;
        vm.Analysis.DataChanged += (_, _) => recomputed++;

        vm.Analysis.SourceMode = OneFactorViewModel.SourceModes[2];   // every feature
        Assert.Equal(1, recomputed);
        Assert.Equal(40, vm.Analysis.Data!.Raw.FeatureCount);
        Assert.Equal(40, vm.Loadings.Count);

        vm.Analysis.SourceMode = OneFactorViewModel.SourceModes[1];   // confirmed, raw
        Assert.Equal(16, vm.Analysis.Data!.Raw.FeatureCount);
        Assert.Equal(2, recomputed);
    }

    [AvaloniaFact]
    public void The_comparison_finds_the_features_that_differ_and_the_volcano_names_them()
    {
        var (vm, _, _) = Reviewed();
        var a = vm.Analysis;
        a.ClassA = "treated";
        a.ClassB = "control";
        a.CompareCommand.Execute(null);

        Assert.Equal(14, a.ComparisonRows.Count);
        var significant = a.ComparisonRows.Where(r => r.IsSignificant(0.05, true)).Select(r => r.FeatureId).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, significant);
        Assert.All(a.ComparisonRows.Where(r => r.IsSignificant(0.05, true)), r => Assert.True(r.FoldChange > 2.5 && r.FoldChange < 3.5, r.Label + " " + r.FoldChange));

        // the volcano colours the same five as up, and the reference lines mark the thresholds
        Assert.Equal(14, a.VolcanoPoints.Count);
        Assert.Equal(5, a.VolcanoPoints.Count(p => p.Group == "up"));
        Assert.Equal(0, a.VolcanoPoints.Count(p => p.Group == "down"));
        Assert.All(a.VolcanoPoints.Where(p => p.Group == "up"), p => Assert.True(p.Labelled));
        Assert.Equal(3, a.VolcanoLines.Count);
        Assert.Contains("5", a.ComparisonCounts);
    }

    [AvaloniaFact]
    public void Choosing_a_feature_on_any_page_opens_it_in_the_ion_table()
    {
        var (vm, _, _) = Reviewed();
        var opened = new List<int>();
        vm.RequestShowFeature = id => opened.Add(id);
        vm.Analysis.CompareCommand.Execute(null);

        // choosing a row draws the feature's boxes; the button opens it
        vm.Analysis.SelectedComparison = vm.Analysis.ComparisonRows.First(r => r.FeatureId == 3);
        Assert.NotEmpty(vm.Analysis.FeatureBoxes);
        Assert.Equal(2, vm.Analysis.FeatureBoxes.Count);   // one box per class
        Assert.Contains("PC", vm.Analysis.FeatureBoxTitle);
        vm.Analysis.OpenSelectedFeatureCommand.Execute(null);
        Assert.Contains(3, opened);

        vm.SelectedLoading = vm.Loadings.First(l => ((PcaLoading)l.Tag!).FeatureId == 4);
        Assert.Contains(4, opened);
    }

    [AvaloniaFact]
    public void The_anova_the_heatmap_the_kmeans_and_the_enrichment_all_run_on_the_dataset()
    {
        var (vm, _, _) = Reviewed(classes: 3);   // treated, control and vehicle, four injections each
        var a = vm.Analysis;
        a.RunAnovaCommand.Execute(null);
        Assert.Equal(14, a.AnovaRows.Count);
        Assert.True(a.AnovaRows.Count(r => r.AdjustedP <= 0.05) >= 5, a.AnovaMessage);
        // and the post-hoc test says which pair differs: the treated against each of the others
        var first = a.AnovaRows.OrderBy(r => r.P).First();
        Assert.Equal(3, first.PostHoc.Count);
        Assert.All(first.PostHoc.Where(d => d.ClassA == "treated" || d.ClassB == "treated"), d => Assert.True(d.P < 0.01));
        a.SelectedAnova = first;
        Assert.Contains("treated", a.PostHocText);
        Assert.Equal(3, a.FeatureBoxes.Count);

        a.HeatmapTopText = "10";
        a.BuildHeatmapCommand.Execute(null);
        Assert.NotNull(a.Heatmap);
        Assert.Equal(10, a.Heatmap!.RowLabels.Count);
        Assert.Equal(12, a.Heatmap.ColumnLabels.Count);
        Assert.NotNull(a.Heatmap.RowTree);

        a.KText = "2";
        a.RunKMeansCommand.Execute(null);
        Assert.Equal(12, a.KMeansPoints.Count);
        // two clusters: the treated alone in one, the two others together in the other
        var treatedCluster = a.KMeansPoints.Where(p => p.Label.StartsWith('T')).Select(p => p.Group).Distinct().ToList();
        Assert.Single(treatedCluster);
        Assert.DoesNotContain(a.KMeansPoints, p => !p.Label.StartsWith('T') && p.Group == treatedCluster[0]);

        a.ClassA = "treated";
        a.ClassB = "control";
        a.CompareCommand.Execute(null);
        a.MinimumSetSizeText = "2";
        a.RunEnrichmentCommand.Execute(null);
        Assert.NotEmpty(a.EnrichmentRows);
        Assert.Contains(a.EnrichmentRows, r => r.Set == "PC" && r.Hits == 5);
        Assert.NotEmpty(a.ClassChangeBars);
        Assert.Contains("PC", a.ChainMapClasses);
        Assert.NotNull(a.ChainMap);
    }

    [AvaloniaFact]
    public void The_correlations_and_the_pattern_search_rank_the_features()
    {
        var (vm, _, _) = Reviewed();
        var a = vm.Analysis;
        a.TopCountText = "8";
        a.CorrelateCommand.Execute(null);
        Assert.NotNull(a.CorrelationHeatmap);
        Assert.Equal(8, a.CorrelationHeatmap!.RowLabels.Count);
        Assert.True(a.CorrelationHeatmap.Symmetric);

        a.PatternKind = OneFactorViewModel.PatternKinds[1];
        a.PatternClassOrder = "control, treated";
        a.SearchPatternCommand.Execute(null);
        Assert.Equal(14, a.PatternRows.Count);
        // the five that rise with the treatment correlate best with the class order
        var best = a.PatternRows.OrderByDescending(r => r.Correlation).Take(5).Select(r => r.FeatureId).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, best);
    }

    [AvaloniaFact]
    public async Task The_forest_leans_on_the_features_that_differ()
    {
        var (vm, _, _) = Reviewed();
        var a = vm.Analysis;
        a.TreesText = "200";
        await a.RunForestCommand.ExecuteAsync(null);
        Assert.Equal(14, a.ImportanceRows.Count);
        var top = a.ImportanceRows.Take(5).Select(r => r.FeatureId).OrderBy(x => x).ToList();
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, top);
        Assert.NotEmpty(a.Confusion);
    }

    [AvaloniaFact]
    public void The_standards_are_kept_in_the_review_and_come_back_on_reload()
    {
        var (vm, curation, spots) = Reviewed();
        var a = vm.Analysis;
        // the dialog is stood in for: the PEs are set to raw, the PCs keep their standard
        a.ShowStandardsDialog = (_, current) => Task.FromResult<IReadOnlyList<StandardAssignment>?>(
            current.Select(x => x.Class == "PE" ? x with { StandardFeatureId = null } : x).ToList());
        a.ChooseStandardsCommand.Execute(null);

        Assert.False(curation.InternalStandards.ContainsKey("PE"));
        Assert.Equal(0, curation.InternalStandards["PC"]);
        Assert.Contains("raw: PE", a.StandardsSummary);
        Assert.Equal(15, a.Data!.Raw.FeatureCount);   // only the PC standard divides itself out now

        curation.Save();
        var again = CurationStore.Load(Path.Combine(Path.GetDirectoryName(curation.SidecarPath)!, "AlignmentResult.arf2"));
        Assert.Equal(curation.InternalStandards, again.InternalStandards);

        // the reload handshake: the review changing marks the analysis stale until the shell reloads it
        a.ReviewIsNewer = true;
        var asked = false;
        a.ReloadRequested += (_, _) => asked = true;
        a.ReloadFromReviewCommand.Execute(null);
        Assert.True(asked);
        Assert.False(a.ReviewIsNewer);
    }

    [AvaloniaFact]
    public void The_standards_dialog_lists_every_class_with_its_suggestion_first()
    {
        var (_, _, spots) = Reviewed();
        var confirmed = spots.Take(16).ToList();
        var window = new StandardsWindow(confirmed, RelativeAbundance.Suggest(confirmed));
        window.Show();

        var rows = window.GetVisualDescendants().OfType<ItemsControl>().First(c => c.Name == "Rows").Items.OfType<StandardRow>().ToList();
        Assert.Equal(2, rows.Count);
        Assert.Equal("PC", rows[0].Class);
        Assert.Equal(10, rows[0].Confirmed);
        Assert.Equal(0, rows[0].Selected.FeatureId);
        Assert.Contains("odd chain", rows[0].Selected.Text);
        Assert.Equal("none — keep the raw area", rows[0].Choices[0].Text);
        // the class's own candidates come before the other class's
        Assert.All(rows[0].Choices.Skip(1).Take(10), c => Assert.StartsWith("PC", c.Text));

        var use = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "UseButton");
        use.Focus();
        window.KeyPress(Avalonia.Input.Key.Enter, Avalonia.Input.RawInputModifiers.None);
        Assert.NotNull(window.Result);
        Assert.Equal(new int?[] { 0, 10 }, window.Result!.Select(r => r.StandardFeatureId).ToArray());
    }

    [AvaloniaFact]
    public async Task Every_chart_writes_itself_as_svg_and_png()
    {
        var (vm, _, _) = Reviewed();
        vm.Analysis.CompareCommand.Execute(null);
        vm.Analysis.BuildHeatmapCommand.Execute(null);
        vm.Analysis.RunEnrichmentCommand.Execute(null);
        var view = new StatisticsView { DataContext = vm };
        var window = new Window { Content = view, Width = 1500, Height = 900 };
        window.Show();
        var tabs = view.GetVisualDescendants().OfType<TabControl>().First(t => t.Name == "StatsTabs");
        var folder = Path.Combine(Path.GetTempPath(), "opendial-charts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);

        var written = 0;
        foreach (var page in new[] { "Volcano plot", "Principal components", "Heatmap", "Lipid enrichment", "Dendrogram", "Normalisation check" })
        {
            StatisticsWorkspaceTests.SelectPage(tabs, page);
            window.UpdateLayout();
            foreach (var frame in view.GetVisualDescendants().OfType<ChartFrame>())
            {
                var chart = frame.Chart;
                Assert.NotNull(chart);
                frame.ExportPathOverride = Path.Combine(folder, $"{written}.svg");
                var svgPath = await frame.ExportAsync("svg", 1);
                Assert.NotNull(svgPath);
                var svg = File.ReadAllText(svgPath!);
                Assert.StartsWith("<svg", svg);
                Assert.Contains("</svg>", svg);
                Assert.True(svg.Length > 500, page + ": " + svg.Length);

                frame.ExportPathOverride = Path.Combine(folder, $"{written}.png");
                var pngPath = await frame.ExportAsync("png", 2);
                Assert.NotNull(pngPath);
                var png = File.ReadAllBytes(pngPath!);
                Assert.Equal(0x89, png[0]);
                Assert.Equal((byte)'P', png[1]);
                written++;
            }
        }
        Assert.True(written >= 10, written.ToString());

        // the volcano's SVG carries its text as text and its lines as paths, not as pixels
        StatisticsWorkspaceTests.SelectPage(tabs, "Volcano plot");
        window.UpdateLayout();
        var volcano = view.GetVisualDescendants().OfType<ScatterChart>().First(c => c.Items == vm.Analysis.VolcanoPoints);
        var text = ChartExport.ToSvg(volcano);
        Assert.Contains("<text", text);
        Assert.Contains("log2 fold change", text);
        Assert.Contains("<ellipse", text);
        Assert.DoesNotContain("<image", text);
        window.Close();
    }

    [AvaloniaFact]
    public void The_pathways_score_the_reactions_between_the_two_classes()
    {
        var (vm, _, _) = Reviewed();
        var a = vm.Analysis;
        a.ClassA = "treated";
        a.ClassB = "control";
        var p = vm.Pathways;
        Assert.Equal("Not computed yet.", p.Message);

        p.ComputeCommand.Execute(null);
        Assert.NotNull(p.Result);
        // PC and PE are confirmed, so PE → PC is testable at the class level; PS, LPC and the rest are not measured
        var pemt = Assert.Single(p.Reactions, r => r.Id == "PEMT");
        Assert.True(pemt.Tested);
        Assert.Contains(p.Predicted, r => r.Missing == "PS");
        Assert.Contains(p.Predicted, r => r.Missing == "LPC");
        Assert.NotEmpty(p.Pathways);
        Assert.Contains("class level", p.Message);
        Assert.Same(pemt, p.SelectedReaction);
        Assert.Equal(2, p.ReactionBoxes.Count);
        Assert.Contains("PEMT", p.ReactionDetail);

        // the species level follows the compositions: PE 36:2 and PC 36:2 are both in the fixture
        p.Level = PathwaysViewModel.Levels[1];
        p.ComputeCommand.Execute(null);
        Assert.Contains(p.Reactions, r => r.Reactant == "PE 36:2" && r.Product == "PC 36:2");
        Assert.Contains("species level", p.Message);

        // choosing a pathway lights its chain and selects its first reaction
        var chain = p.Pathways.First();
        p.SelectedPathway = chain;
        Assert.Equal(chain.Nodes, p.HighlightedChain);
        Assert.Same(chain.Reactions[0], p.SelectedReaction);

        // the paired option goes through to the engine and is said in the message
        p.Paired = true;
        p.ComputeCommand.Execute(null);
        Assert.Contains("paired", p.Message);
        p.Paired = false;

        // a new dataset clears the result, so it never describes numbers the pages no longer show
        a.SourceMode = OneFactorViewModel.SourceModes[2];
        Assert.Null(p.Result);
        Assert.Equal("Not computed yet.", p.Message);
    }

    [AvaloniaFact]
    public void The_pathway_graph_draws_and_exports()
    {
        var (vm, _, _) = Reviewed();
        vm.Analysis.ClassA = "treated";
        vm.Analysis.ClassB = "control";
        vm.Pathways.ComputeCommand.Execute(null);
        var graph = new PathwayGraph { Result = vm.Pathways.Result, Width = 700, Height = 500 };
        var window = new Window { Content = graph, Width = 720, Height = 520 };
        window.Show();
        window.UpdateLayout();
        var svg = ChartExport.ToSvg(graph);
        Assert.Contains("<line", svg);
        Assert.Contains("PC", svg);
        Assert.Contains("faster in treated", svg);
        window.Close();
    }
}
