using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Desktop.ViewModels;
using OpenDIAL.Desktop.Views;
using OpenDIAL.Pipeline.Curation;
using OpenDIAL.Pipeline.Results;
using Xunit;

namespace OpenDIAL.Desktop.Tests;

/// <summary>
/// The other polarity, in the review workspace: the ion table says which compounds both runs saw,
/// what they weigh as neutral molecules, and which side quantifies them.
/// </summary>
public class PolarityWorkspaceTests
{
    private static IReadOnlyList<SampleInfo> Samples() => new[]
    {
        new SampleInfo(0, "liver_01_pos", "liver", "Sample", 1),
        new SampleInfo(1, "liver_02_pos", "liver", "Sample", 2),
        new SampleInfo(2, "liver_03_pos", "treated", "Sample", 3),
        new SampleInfo(3, "liver_04_pos", "treated", "Sample", 4),
    };

    private static IReadOnlyList<SampleInfo> NegativeSamples() =>
        Samples().Select(s => new SampleInfo(s.FileId, s.FileName.Replace("_pos", "_neg"), s.Class, s.SampleType, s.InjectionOrder)).ToList();

    private static AlignmentSpotRow Row(int id, string name, double mz, double rt, string adduct, double[] heights, double signalToNoise, IReadOnlyList<SampleInfo> samples)
        => new()
        {
            Id = id,
            Name = name,
            Mz = mz,
            Rt = rt,
            Adduct = adduct,
            AverageHeight = heights.Average(),
            SignalToNoiseAverage = signalToNoise,
            FillPercent = 100,
            MsmsAssigned = true,
            IsotopeWeight = 0,
            SampleHeights = samples.Select((s, i) => new SampleValue(s.FileId, s.FileName, s.Class, heights[i])).ToList(),
            SamplePeaks = samples.Select((s, i) => new AlignedSamplePeak(s.FileId, s.FileName, s.Class, s.SampleType, rt, rt - 0.1, rt + 0.1, mz, heights[i], heights[i] * 10, signalToNoise, false)).ToList(),
        };

    // a neutral of 759.5778 and one of 500.2500: protonated, deprotonated, and one seen only in positive
    private static readonly double[] Rising = { 100, 210, 320, 480 };
    private static readonly double[] RisingLower = { 40, 86, 128, 195 };
    private static readonly double[] Flat = { 900, 880, 910, 870 };

    private static (AnalyticsViewModel Vm, string Folder) NewAnalytics()
    {
        var folder = Path.Combine(Path.GetTempPath(), "opendial-polarity-ui-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var vm = new AnalyticsViewModel(new RawDataCache());
        var samples = Samples();
        var store = CurationStore.Load(Path.Combine(folder, "AlignResult-1.arf"));
        vm.LoadForTest(new[]
        {
            Row(1, "PC 34:1", 760.5851, 5.00, "[M+H]+", Rising, 20, samples),
            Row(2, "Unknown", 501.2573, 3.00, "[M+H]+", Flat, 30, samples),
        }, samples, store);
        return (vm, folder);
    }

    private static AlignmentTable NegativeRun()
    {
        var samples = NegativeSamples();
        return new AlignmentTable(samples, new[]
        {
            Row(11, "PC 34:1", 758.5705, 5.02, "[M-H]-", RisingLower, 55, samples),
            Row(12, "Unknown", 281.2486, 2.10, "[M-H]-", Flat, 40, samples),
        }, "test");
    }

    [AvaloniaFact]
    public void Linking_the_other_polarity_marks_what_both_runs_saw()
    {
        var (vm, _) = NewAnalytics();
        Assert.False(vm.HasPolarityLink);
        Assert.All(vm.IonRows, r => Assert.Equal(string.Empty, r.PolarityText));

        var sentence = vm.LinkPolarity(NegativeRun());

        Assert.True(vm.HasPolarityLink);
        Assert.Contains("in both polarities", sentence, StringComparison.Ordinal);
        var paired = vm.IonRows.Single(r => r.Id == 1);
        var alone = vm.IonRows.Single(r => r.Id == 2);
        Assert.True(paired.SeenInBoth);
        Assert.False(alone.SeenInBoth);
        Assert.Equal("±", paired.PolarityText);
        Assert.Equal("+", alone.PolarityText);
        Assert.Contains("758.5705", paired.PartnerText, StringComparison.Ordinal);
        Assert.Contains("PC 34:1", paired.PartnerText, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void The_neutral_mass_is_shown_whether_or_not_a_polarity_is_linked()
    {
        var (vm, _) = NewAnalytics();
        var row = vm.IonRows.Single(r => r.Id == 1);
        Assert.Equal(759.5778, row.NeutralMass, 3);
        Assert.Equal("759.5778", row.NeutralText);
    }

    [AvaloniaFact]
    public void The_side_that_measured_the_compound_better_is_the_one_that_quantifies()
    {
        var (vm, _) = NewAnalytics();
        vm.LinkPolarity(NegativeRun());
        var paired = vm.IonRows.Single(r => r.Id == 1);
        Assert.Equal("negative", paired.QuantifyText);   // 55 against 20
        Assert.False(paired.QuantifiesHere);
    }

    [AvaloniaFact]
    public void The_polarity_filter_narrows_the_table_and_unlinking_puts_it_back()
    {
        var (vm, _) = NewAnalytics();
        vm.LinkPolarity(NegativeRun());

        vm.PolarityFilter = "Seen in both";
        Assert.Single(vm.IonRows);
        Assert.Equal(1, vm.IonRows[0].Id);

        vm.PolarityFilter = "Only in this polarity";
        Assert.Single(vm.IonRows);
        Assert.Equal(2, vm.IonRows[0].Id);

        vm.UnlinkPolarity();
        Assert.False(vm.HasPolarityLink);
        Assert.Equal("All", vm.PolarityFilter);
        Assert.Equal(2, vm.IonRows.Count);
        Assert.All(vm.IonRows, r => Assert.False(r.SeenInBoth));
    }

    [AvaloniaFact]
    public void A_run_of_the_same_polarity_is_refused_rather_than_paired()
    {
        var (vm, _) = NewAnalytics();
        var samples = Samples();
        var alsoPositive = new AlignmentTable(samples, new[]
        {
            Row(11, "PC 34:1", 760.5851, 5.00, "[M+H]+", RisingLower, 55, samples),
        }, "test");

        var sentence = vm.LinkPolarity(alsoPositive);
        Assert.False(vm.HasPolarityLink);
        Assert.Contains("other polarity", sentence, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public void The_ion_table_draws_the_polarity_columns()
    {
        var (vm, _) = NewAnalytics();
        vm.LinkPolarity(NegativeRun());

        var window = new Window { Width = 1400, Height = 900, Content = new IonTableView { DataContext = vm } };
        window.Show();
        window.UpdateLayout();

        var grid = window.GetVisualDescendants().OfType<DataGrid>().Single();
        var headers = grid.Columns.Select(c => c.Header?.ToString()).ToList();
        Assert.Contains("Neutral", headers);
        Assert.Contains("Pol", headers);
        Assert.Contains("Other polarity", headers);
        Assert.Contains("Quantify", headers);
        window.Close();
    }

    [AvaloniaFact]
    public void The_pairing_is_written_beside_the_alignment_and_read_back()
    {
        var (vm, folder) = NewAnalytics();
        vm.LinkPolarity(NegativeRun());

        var path = PolarityPairFile.FileFor(Path.Combine(folder, "AlignResult-1.arf2"));
        PolarityPairFile.Save(path, vm.PolarityPairs!, Path.Combine(folder, "AlignResult-1.arf2"), Path.Combine(folder, "AlignResult-2.arf2"), new PolarityLinkOptions());

        var read = PolarityPairFile.Load(path);
        Assert.NotNull(read);
        Assert.Single(read!.Result.Pairs);
        Assert.Equal(PolarityChoice.Negative, read.Result.Pairs[0].Quantify);
        Directory.Delete(folder, true);
    }
}
