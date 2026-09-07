using Avalonia.Controls;
using Avalonia.VisualTree;
using Avalonia.Headless.XUnit;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Desktop.ViewModels;
using OpenDIAL.Desktop.Views;
using OpenDIAL.Pipeline.Curation;
using OpenDIAL.Pipeline.Results;
using Xunit;

namespace OpenDIAL.Desktop.Tests;

/// <summary>
/// The review workspace driven headlessly: the views are the real compiled XAML, so a binding that
/// only breaks at runtime breaks a test here. The view models are fed synthetic features rather
/// than a processed run, because these tests are about the interface, not the pipeline.
/// </summary>
public class ReviewWorkspaceTests
{
    private static IReadOnlyList<SampleInfo> Samples() => new[]
    {
        new SampleInfo(0, "liver A", "liver", "Sample"),
        new SampleInfo(1, "liver B", "liver", "Sample"),
        new SampleInfo(2, "blank", "blank", "Blank"),
    };

    private static AlignmentSpotRow Row(int id, string name, string ontology, double mz, double rt, bool msms = true, int isotope = 0)
    {
        var samples = Samples();
        return new AlignmentSpotRow
        {
            Id = id,
            Name = name,
            Ontology = ontology,
            Mz = mz,
            Rt = rt,
            Adduct = "[M+H]+",
            AverageHeight = 1000 * (id + 1),
            FillPercent = 100,
            MsmsAssigned = msms,
            IsotopeWeight = isotope,
            SampleHeights = samples.Select(s => new SampleValue(s.FileId, s.FileName, s.Class, 1000 * (id + 1))).ToList(),
            SamplePeaks = samples.Select(s => new AlignedSamplePeak(s.FileId, s.FileName, s.Class, s.SampleType, rt, rt - 0.1, rt + 0.1, mz, 1000 * (id + 1), 10000, 20, false)).ToList(),
            ClassHeights = new[] { new ClassHeight("liver", 1000 * (id + 1)), new ClassHeight("blank", 10) },
        };
    }

    /// <summary>The same fixture, reachable from the visual tests.</summary>
    internal static (AnalyticsViewModel Vm, CurationStore Store, string Folder) NewAnalyticsForVisuals() => NewAnalytics();

    private static (AnalyticsViewModel Vm, CurationStore Store, string Folder) NewAnalytics()
    {
        var folder = Path.Combine(Path.GetTempPath(), "opendial-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var vm = new AnalyticsViewModel(new RawDataCache());
        var store = CurationStore.Load(Path.Combine(folder, "AlignResult-1.arf"));
        var rows = new[]
        {
            Row(0, "PC 34:1", "PC", 760.5851, 11.4),
            Row(1, "low score: PE 36:2", "PE", 744.5538, 10.9),
            Row(2, "Unknown", string.Empty, 500.3000, 4.2, msms: false),
            Row(3, "TG 52:2", "TG", 876.8017, 18.7, isotope: 1),
        };
        vm.LoadForTest(rows, Samples(), store);
        return (vm, store, folder);
    }

    [AvaloniaFact]
    public void The_ion_table_lists_every_feature_and_the_filters_narrow_it()
    {
        var (vm, _, _) = NewAnalytics();
        Assert.Equal(4, vm.IonRows.Count);

        vm.FilterText = "PC";
        Assert.Single(vm.IonRows);
        Assert.Equal(0, vm.IonRows[0].Id);

        vm.FilterText = string.Empty;
        vm.AnnotationFilter = "Unknown";
        Assert.Single(vm.IonRows);
        Assert.Equal(2, vm.IonRows[0].Id);

        vm.AnnotationFilter = "All";
        vm.MsmsOnly = true;
        Assert.Equal(3, vm.IonRows.Count);
        Assert.DoesNotContain(vm.IonRows, r => r.Id == 2);

        vm.MsmsOnly = false;
        vm.MolecularIonOnly = true;
        Assert.DoesNotContain(vm.IonRows, r => r.Id == 3);   // M+1

        vm.MolecularIonOnly = false;
        vm.MzFrom = "700";
        vm.MzTo = "800";
        Assert.Equal(2, vm.IonRows.Count);

        vm.MzFrom = vm.MzTo = string.Empty;
        vm.OntologyFilter = "TG";
        Assert.Single(vm.IonRows);
    }

    [AvaloniaFact]
    public void Tagging_a_feature_updates_the_row_the_counts_and_the_store()
    {
        var (vm, store, _) = NewAnalytics();
        vm.SelectedRow = vm.IonRows.First(r => r.Id == 1);

        vm.ToggleTagCommand.Execute("3");   // Misannotation
        Assert.True(vm.SelectedRow!.IsRejected);
        Assert.Contains("Misannotation", vm.SelectedRow.TagText);
        Assert.True(store.HasTag(1, PeakSpotTagKind.Misannotation));
        Assert.Equal(1, vm.RejectedCount);
        Assert.True(vm.CurationDirty);

        vm.ToggleTagCommand.Execute("3");
        Assert.False(vm.SelectedRow.IsRejected);
        Assert.False(store.HasTag(1, PeakSpotTagKind.Misannotation));
        Assert.Equal(0, vm.RejectedCount);
    }

    [AvaloniaFact]
    public void Confirm_tags_the_feature_and_moves_to_the_next_one()
    {
        var (vm, store, _) = NewAnalytics();
        vm.SelectedRow = vm.IonRows[0];

        vm.ConfirmAndNextCommand.Execute(null);

        Assert.True(store.HasTag(0, PeakSpotTagKind.Confirmed));
        Assert.Equal(1, vm.SelectedRow!.Id);
        Assert.Equal(1, vm.ConfirmedCount);

        vm.RejectAndNextCommand.Execute(null);
        Assert.True(store.HasTag(1, PeakSpotTagKind.Misannotation));
        Assert.False(store.HasTag(1, PeakSpotTagKind.Confirmed));
        Assert.Equal(2, vm.SelectedRow.Id);
    }

    [AvaloniaFact]
    public void The_tag_filter_follows_the_tagging()
    {
        var (vm, _, _) = NewAnalytics();
        vm.SelectedRow = vm.IonRows.First(r => r.Id == 2);
        vm.ToggleTagCommand.Execute("1");

        vm.TagFilter = "Confirmed";
        Assert.Single(vm.IonRows);
        Assert.Equal(2, vm.IonRows[0].Id);

        vm.TagFilter = "Untagged";
        Assert.Equal(3, vm.IonRows.Count);
        Assert.DoesNotContain(vm.IonRows, r => r.Id == 2);
    }

    [AvaloniaFact]
    public void Bringing_a_hidden_feature_into_view_clears_the_filter_that_hid_it()
    {
        var (vm, _, _) = NewAnalytics();
        vm.OntologyFilter = "PC";
        Assert.Single(vm.IonRows);

        vm.SelectFeature(3);

        Assert.Equal(3, vm.SelectedRow!.Id);
        Assert.Equal(4, vm.IonRows.Count);
        Assert.Equal("All", vm.OntologyFilter);
    }

    [AvaloniaFact]
    public void A_comment_reaches_the_store_and_the_export()
    {
        var (vm, store, folder) = NewAnalytics();
        vm.SelectedRow = vm.IonRows[0];
        vm.SelectedRow!.Comment = "class fragment present";
        vm.ToggleTagCommand.Execute("1");

        Assert.Equal("class fragment present", store.Get(0).Comment);

        var path = Path.Combine(folder, "reviewed.txt");
        var count = vm.ExportReviewedTableAsync(path, areas: false).GetAwaiter().GetResult();
        Assert.Equal(4, count);
        var text = File.ReadAllText(path);
        Assert.Contains("Tags", text);
        Assert.Contains("Confirmed", text);
        Assert.Contains("class fragment present", text);
        Assert.Contains("liver A", text);
        Directory.Delete(folder, true);
    }

    [AvaloniaFact]
    public void The_analytics_view_binds_against_a_loaded_session()
    {
        var (vm, _, _) = NewAnalytics();
        var view = new AnalyticsView { DataContext = vm };
        var window = new Window { Content = view, Width = 1400, Height = 900 };
        window.Show();

        var table = view.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault(g => g.Name == "IonTable");
        Assert.NotNull(table);
        Assert.Equal(vm.IonRows, table!.ItemsSource);

        var tabs = view.GetVisualDescendants().OfType<TabControl>().FirstOrDefault(t => t.Name == "ResultTabs");
        Assert.NotNull(tabs);
        Assert.Equal(9, tabs!.Items.Count);   // peaks, MS/MS, isotopes, candidates, abundance, map, samples, statistics, trend
        window.Close();
    }
}
