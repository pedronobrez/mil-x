using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpenDIAL.Desktop.Controls;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Desktop.ViewModels;
using OpenDIAL.Desktop.Views;
using OpenDIAL.Pipeline.Model;
using OpenDIAL.Pipeline.Results;
using OpenDIAL.Pipeline.Statistics;
using Xunit;

namespace OpenDIAL.Desktop.Tests;

public class StatisticsWorkspaceTests
{
    private sealed class NoDialogs : IFileDialogService
    {
        public Task<IReadOnlyList<string>> PickFilesAsync(string title, IReadOnlyList<string> patterns, bool allowMultiple, string? startFolder = null)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<string?> PickFolderAsync(string title, string? startFolder = null) => Task.FromResult<string?>(null);
        public Task<string?> SaveFileAsync(string title, string suggestedName, string extension, string? startFolder = null) => Task.FromResult<string?>(null);
    }

    /// <summary>Selects a page of the statistics workspace by its header; the order is not part of the contract.</summary>
    internal static void SelectPage(TabControl tabs, string header)
    {
        var item = tabs.Items.OfType<TabItem>().FirstOrDefault(t => (t.Header as string) == header);
        Assert.NotNull(item);
        tabs.SelectedItem = item;
        // the page's content is built on the next layout pass; make it now
        (TopLevel.GetTopLevel(tabs) as Window)?.UpdateLayout();
    }

    internal static (StatisticsViewModel Vm, int Features) Loaded()
    {
        var samples = new[]
        {
            new SampleInfo(0, "A1", "treated", "Sample"),
            new SampleInfo(1, "A2", "treated", "Sample"),
            new SampleInfo(2, "B1", "control", "Sample"),
            new SampleInfo(3, "B2", "control", "Sample"),
        };
        var rng = new Random(3);
        var features = new List<AlignmentSpotRow>();
        for (var k = 0; k < 30; k++)
        {
            var level = 1000.0 * (k + 1);
            var high = k % 2 == 0 ? level * 8 : level;
            var low = k % 2 == 0 ? level : level * 8;
            double J() => 0.97 + rng.NextDouble() * 0.06;
            var heights = new[] { high * J(), high * J(), low * J(), low * J() };
            features.Add(new AlignmentSpotRow
            {
                Id = k,
                Name = k % 3 == 0 ? "PC " + k : "Unknown",
                Ontology = k % 3 == 0 ? "PC" : string.Empty,
                Mz = 700 + k,
                Rt = 5 + k * 0.1,
                AverageHeight = heights.Average(),
                SamplePeaks = samples.Select((s, i) => new AlignedSamplePeak(s.FileId, s.FileName, s.Class, s.SampleType, 5, 4.9, 5.1, 700 + k, heights[i], heights[i] * 10, 20, false)).ToList(),
            });
        }
        var vm = new StatisticsViewModel(new NoDialogs());
        var session = new ResultSession(Path.GetTempPath(), Array.Empty<CompMs.MsdialCore.DataObj.AnalysisFileBean>(), null, null, null, Array.Empty<ExportedFile>(), null, IonizationMode.LCMS);
        vm.Load(session, features, samples);
        return (vm, features.Count);
    }

    [AvaloniaFact]
    public void Loading_a_result_computes_the_components_and_the_clustering()
    {
        var (vm, features) = Loaded();

        Assert.True(vm.HasResults);
        Assert.Equal(4, vm.Scores.Count);
        Assert.Equal(features, vm.Loadings.Count);
        Assert.NotEmpty(vm.Variance);
        Assert.True(vm.Variance[0].Percent > 40);
        Assert.NotNull(vm.ClusterRoot);
        Assert.Equal(4, vm.ClusterRoot!.Leaves().Count());

        // the two classes must land on opposite sides of the first component
        var treated = vm.Scores.Where(s => s.Group == "treated").Average(s => s.X);
        var control = vm.Scores.Where(s => s.Group == "control").Average(s => s.X);
        Assert.True(Math.Sign(treated) != Math.Sign(control));
    }

    [AvaloniaFact]
    public void Changing_the_options_recomputes()
    {
        var (vm, _) = Loaded();
        var before = vm.Variance[0].Percent;

        vm.Analysis.AnnotatedOnly = true;
        Assert.Equal(10, vm.Loadings.Count);   // one feature in three is annotated

        vm.Analysis.Scaling = "Mean centre only";
        Assert.NotEqual(before, vm.Variance[0].Percent);
    }

    [AvaloniaFact]
    public void The_statistics_view_binds_and_shows_every_reading()
    {
        var (vm, _) = Loaded();
        var view = new StatisticsView { DataContext = vm };
        var window = new Window { Content = view, Width = 1400, Height = 900 };
        window.Show();

        var tabs = view.GetVisualDescendants().OfType<TabControl>().FirstOrDefault(t => t.Name == "StatsTabs");
        Assert.NotNull(tabs);
        // eighteen pages under six headings, and the workspace opens on a page, not a heading
        Assert.Equal(19, tabs!.Items.OfType<TabItem>().Count(t => !t.Classes.Contains("section")));
        Assert.Equal(6, tabs.Items.OfType<TabItem>().Count(t => t.Classes.Contains("section")));
        Assert.Equal("Data processing", (tabs.SelectedItem as TabItem)?.Header);

        SelectPage(tabs, "Principal components");
        var scatters = view.GetVisualDescendants().OfType<ScatterChart>().ToList();
        Assert.True(scatters.Count >= 2, "scores and loadings");
        Assert.Contains(scatters, s => s.Items == vm.Scores);
        Assert.Contains(scatters, s => s.Items == vm.Loadings);
        Assert.Contains(view.GetVisualDescendants().OfType<BarChart>(), b => b.Items == vm.Scree);

        // drift correction: the same feature drawn before and after
        SelectPage(tabs, "Drift correction");
        var drift = view.GetVisualDescendants().OfType<ScatterChart>().ToList();
        Assert.True(drift.Count >= 2, "before and after");

        // the discriminant model
        SelectPage(tabs, "Discriminant");
        Assert.Contains(view.GetVisualDescendants().OfType<DataGrid>(), g => g.Columns.Any(c => (c.Header as string) == "VIP"));

        // the orthogonal rotation
        SelectPage(tabs, "Orthogonal");
        Assert.Contains(view.GetVisualDescendants().OfType<DataGrid>(), g => g.Columns.Any(c => (c.Header as string) == "Covariance"));

        SelectPage(tabs, "Dendrogram");
        var dendrogram = view.GetVisualDescendants().OfType<Dendrogram>().FirstOrDefault();
        Assert.NotNull(dendrogram);
        Assert.Same(vm.ClusterRoot, dendrogram!.Root);

        SelectPage(tabs, "Molecular network");
        Assert.NotNull(view.GetVisualDescendants().OfType<NetworkGraph>().FirstOrDefault());

        // the one-factor pages bind to the analysis: every one shows its chart and its table
        SelectPage(tabs, "Volcano plot");
        Assert.Contains(view.GetVisualDescendants().OfType<ScatterChart>(), c => c.Items == vm.Analysis.VolcanoPoints);
        SelectPage(tabs, "Heatmap");
        Assert.NotNull(view.GetVisualDescendants().OfType<HeatmapChart>().FirstOrDefault());
        SelectPage(tabs, "Lipid enrichment");
        Assert.True(view.GetVisualDescendants().OfType<RankChart>().Count() >= 2, "enrichment and class changes");
        SelectPage(tabs, "Normalisation check");
        Assert.Equal(4, view.GetVisualDescendants().OfType<BoxPlotChart>().Count());

        // every chart on every page sits in a frame that can write it out
        foreach (var page in tabs.Items.OfType<TabItem>().Where(t => !t.Classes.Contains("section")))
        {
            SelectPage(tabs, (string)page.Header!);
            foreach (var frame in view.GetVisualDescendants().OfType<ChartFrame>())
                Assert.True(frame.Chart is not null, $"{page.Header}: {frame.HeadingText} has no chart");
        }
        window.Close();
    }

    [AvaloniaFact]
    public void Choosing_a_network_node_asks_the_shell_to_open_that_feature()
    {
        var (vm, _) = Loaded();
        var opened = -1;
        vm.RequestShowFeature = id => opened = id;
        vm.SelectedNetworkFeature = 17;
        Assert.Equal(17, opened);
    }

    /// <summary>
    /// A sequence that drifts: the response falls away as the run goes on and steps down at the
    /// second batch, with a quality control every third injection to measure it against.
    /// </summary>
    internal static (StatisticsViewModel Vm, IReadOnlyList<SampleInfo> Samples) Drifting()
    {
        var samples = new List<SampleInfo>();
        for (var i = 0; i < 18; i++)
        {
            var qc = i % 3 == 0;
            samples.Add(new SampleInfo(
                i,
                qc ? $"QC{i:00}" : $"S{i:00}",
                qc ? "QC" : i % 2 == 0 ? "treated" : "control",
                qc ? "QC" : "Sample",
                InjectionOrder: i + 1,
                Batch: i < 9 ? 1 : 2));
        }

        var rng = new Random(11);
        var features = new List<AlignmentSpotRow>();
        for (var k = 0; k < 20; k++)
        {
            var level = 5000.0 * (k + 1);
            var heights = new double[samples.Count];
            for (var i = 0; i < samples.Count; i++)
            {
                var decay = 1.0 - 0.03 * i;                  // the source fouls across the run
                var step = samples[i].Batch == 2 ? 0.6 : 1;  // and the second batch sits lower
                heights[i] = level * decay * step * (0.99 + rng.NextDouble() * 0.02);
            }
            features.Add(new AlignmentSpotRow
            {
                Id = k,
                Name = "PC " + k,
                Ontology = "PC",
                Mz = 700 + k,
                Rt = 5 + k * 0.1,
                AverageHeight = heights.Average(),
                SamplePeaks = samples.Select((s, i) => new AlignedSamplePeak(s.FileId, s.FileName, s.Class, s.SampleType, 5, 4.9, 5.1, 700 + k, heights[i], heights[i] * 10, 20, false)).ToList(),
            });
        }

        var vm = new StatisticsViewModel(new NoDialogs());
        var session = new ResultSession(Path.GetTempPath(), Array.Empty<CompMs.MsdialCore.DataObj.AnalysisFileBean>(), null, null, null, Array.Empty<ExportedFile>(), null, IonizationMode.LCMS);
        vm.Load(session, features, samples);
        return (vm, samples);
    }

    /// <summary>
    /// Twelve injections in two classes where only a handful of features actually differ, which is
    /// what a real comparison looks like: the model has something to find and plenty of noise to
    /// find it in.
    /// </summary>
    internal static (StatisticsViewModel Vm, IReadOnlyList<AlignmentSpotRow> Discriminating) Comparison()
    {
        var samples = new List<SampleInfo>();
        for (var i = 0; i < 12; i++)
        {
            samples.Add(new SampleInfo(i, (i < 6 ? "T" : "C") + (i % 6 + 1), i < 6 ? "treated" : "control", "Sample", InjectionOrder: i + 1));
        }

        var rng = new Random(7);
        var features = new List<AlignmentSpotRow>();
        var discriminating = new List<AlignmentSpotRow>();
        for (var k = 0; k < 40; k++)
        {
            var level = 4000.0 * (k + 1);
            var separates = k % 5 == 0;                    // eight of the forty carry the difference
            var heights = new double[samples.Count];
            for (var i = 0; i < samples.Count; i++)
            {
                var fold = separates ? (samples[i].Class == "treated" ? 3.0 : 1.0) : 1.0;
                heights[i] = level * fold * (0.85 + rng.NextDouble() * 0.3);
            }
            var row = new AlignmentSpotRow
            {
                Id = k,
                Name = separates ? $"PC {30 + k}:1" : "Unknown",
                Ontology = separates ? "PC" : string.Empty,
                Mz = 700 + k,
                Rt = 5 + k * 0.1,
                AverageHeight = heights.Average(),
                SamplePeaks = samples.Select((s, i) => new AlignedSamplePeak(s.FileId, s.FileName, s.Class, s.SampleType, 5, 4.9, 5.1, 700 + k, heights[i], heights[i] * 10, 20, false)).ToList(),
            };
            features.Add(row);
            if (separates) discriminating.Add(row);
        }

        var vm = new StatisticsViewModel(new NoDialogs());
        var session = new ResultSession(Path.GetTempPath(), Array.Empty<CompMs.MsdialCore.DataObj.AnalysisFileBean>(), null, null, null, Array.Empty<ExportedFile>(), null, IonizationMode.LCMS);
        vm.Load(session, features, samples);
        return (vm, discriminating);
    }

    [AvaloniaFact]
    public async Task The_discriminant_model_finds_the_features_that_actually_differ()
    {
        var (vm, discriminating) = Comparison();
        await vm.FitDiscriminantCommand.ExecuteAsync(null);

        Assert.True(vm.HasPls);
        Assert.True(vm.PlsTrustworthy, "a real difference in twelve injections should survive: " + vm.PlsQuality);

        // the eight features carrying the difference should be the eight the model leans on
        var top = vm.VipRows.Take(discriminating.Count).Select(r => r.FeatureId).ToHashSet();
        Assert.All(discriminating, f => Assert.Contains(f.Id, top));
        Assert.All(vm.VipRows.Where(r => top.Contains(r.FeatureId)), r => Assert.Equal("treated", r.Side));
    }

    [AvaloniaFact]
    public async Task Fitting_the_discriminant_model_puts_the_declared_classes_on_opposite_sides()
    {
        var (vm, _) = Loaded();
        vm.PlsPermutations = "50";
        await vm.FitDiscriminantCommand.ExecuteAsync(null);

        Assert.True(vm.HasPls);
        Assert.Equal(4, vm.PlsScores.Count);
        Assert.NotEmpty(vm.VipRows);
        Assert.Contains(vm.VipRows, r => r.Matters);
        Assert.NotEmpty(vm.PlsQuality);

        var treated = vm.PlsScores.Where(s => s.Group == "treated").Average(s => s.X);
        var control = vm.PlsScores.Where(s => s.Group == "control").Average(s => s.X);
        Assert.True(Math.Sign(treated) != Math.Sign(control), "the two classes should sit on opposite sides");
    }

    [AvaloniaFact]
    public async Task Choosing_a_feature_from_the_discriminant_table_opens_it()
    {
        var (vm, _) = Loaded();
        vm.PlsPermutations = "0";
        await vm.FitDiscriminantCommand.ExecuteAsync(null);

        var opened = -1;
        vm.RequestShowFeature = id => opened = id;
        vm.SelectedVipRow = vm.VipRows[0];
        Assert.Equal(vm.VipRows[0].FeatureId, opened);
    }

    [AvaloniaFact]
    public async Task Correcting_the_drift_tightens_the_controls_and_can_feed_the_other_views()
    {
        var (vm, _) = Drifting();
        await vm.ApplyCorrectionCommand.ExecuteAsync(null);

        Assert.True(vm.HasCorrection);
        Assert.NotEmpty(vm.CorrectionRows);
        Assert.All(vm.CorrectionRows, r => Assert.True(r.CvAfter < r.CvBefore, $"{r.Label}: {r.CvBefore:F1} % before, {r.CvAfter:F1} % after"));
        Assert.Contains("quality control", vm.CorrectionDetail);

        // picking a feature draws its response across the run, before and after
        Assert.NotNull(vm.SelectedCorrectionRow);
        Assert.Equal(18, vm.DriftBefore.Count);
        Assert.Equal(18, vm.DriftAfter.Count);
        Assert.Contains(vm.DriftBefore, p => p.Group == "QC");

        // and the corrected values reach the rest of the page only when asked
        var raw = vm.Scores.Select(s => s.X).ToList();
        vm.UseCorrectedValues = true;
        Assert.Contains("drift corrected", vm.Summary);
        Assert.NotEqual(raw, vm.Scores.Select(s => s.X).ToList());
    }

    [AvaloniaFact]
    public async Task Without_quality_controls_nothing_is_corrected_and_it_says_why()
    {
        var (vm, _) = Loaded();   // four injections, none marked QC
        await vm.ApplyCorrectionCommand.ExecuteAsync(null);

        Assert.False(vm.HasCorrection);
        Assert.False(vm.UseCorrectedValues);
        Assert.Contains("QC", vm.CorrectionDetail);
    }

    [AvaloniaFact]
    public async Task The_orthogonal_model_puts_the_separation_on_one_axis_and_the_rest_beside_it()
    {
        var (vm, _) = Comparison();
        vm.OplsPermutations = "100";
        await vm.FitOrthogonalCommand.ExecuteAsync(null);

        Assert.True(vm.HasOpls, vm.OplsVerdict);
        Assert.Equal(12, vm.OplsScores.Count);
        Assert.NotEmpty(vm.SPlotRows);
        Assert.Contains(vm.SPlotRows, r => r.Reliable);
        Assert.Contains("survives cross-validation", vm.OplsVerdict);

        // the classes sit on opposite ends of the predictive axis, and do not overlap
        var treated = vm.OplsScores.Where(s => s.Group == "treated").Select(s => s.X).ToList();
        var control = vm.OplsScores.Where(s => s.Group == "control").Select(s => s.X).ToList();
        Assert.True(treated.Max() < control.Min() || control.Max() < treated.Min());
    }

    [AvaloniaFact]
    public async Task Clicking_the_S_plot_opens_that_feature()
    {
        var (vm, _) = Comparison();
        vm.OplsPermutations = "0";
        await vm.FitOrthogonalCommand.ExecuteAsync(null);

        var opened = -1;
        vm.RequestShowFeature = id => opened = id;
        vm.SelectedSPlotPoint = vm.SPlot[0];
        Assert.Equal(((OplsLoading)vm.SPlot[0].Tag!).FeatureId, opened);

        opened = -1;
        vm.SelectedSPlotRow = vm.SPlotRows[0];
        Assert.Equal(vm.SPlotRows[0].FeatureId, opened);
    }

    [AvaloniaFact]
    public async Task Three_classes_get_a_reason_rather_than_an_empty_plot()
    {
        var (vm, _) = Drifting();   // QC, treated and control
        vm.OplsPermutations = "0";
        await vm.FitOrthogonalCommand.ExecuteAsync(null);

        Assert.False(vm.HasOpls);
        Assert.Empty(vm.OplsScores);
        Assert.Contains("two classes", vm.OplsVerdict);
    }
}
