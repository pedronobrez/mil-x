using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpenDIAL.Desktop.Controls;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Desktop.ViewModels;
using OpenDIAL.Desktop.Views;
using OpenDIAL.Pipeline.Model;
using OpenDIAL.Pipeline.Results;
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

    private static (StatisticsViewModel Vm, int Features) Loaded()
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

        vm.AnnotatedOnly = true;
        Assert.Equal(10, vm.Loadings.Count);   // one feature in three is annotated

        vm.Scaling = "None (centre only)";
        Assert.NotEqual(before, vm.Variance[0].Percent);
    }

    [AvaloniaFact]
    public void The_statistics_view_binds_and_shows_its_three_readings()
    {
        var (vm, _) = Loaded();
        var view = new StatisticsView { DataContext = vm };
        var window = new Window { Content = view, Width = 1400, Height = 900 };
        window.Show();

        var tabs = view.GetVisualDescendants().OfType<TabControl>().FirstOrDefault(t => t.Name == "StatsTabs");
        Assert.NotNull(tabs);
        Assert.Equal(3, tabs!.Items.Count);

        var scatters = view.GetVisualDescendants().OfType<ScatterChart>().ToList();
        Assert.True(scatters.Count >= 2, "scores and loadings");
        Assert.Equal(vm.Scores, scatters[0].Items);

        tabs.SelectedIndex = 1;
        var dendrogram = view.GetVisualDescendants().OfType<Dendrogram>().FirstOrDefault();
        Assert.NotNull(dendrogram);
        Assert.Same(vm.ClusterRoot, dendrogram!.Root);

        tabs.SelectedIndex = 2;
        Assert.NotNull(view.GetVisualDescendants().OfType<NetworkGraph>().FirstOrDefault());
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
}
