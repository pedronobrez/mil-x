using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using MilX.Desktop.Charts;
using MilX.Desktop.Controls;
using Xunit;

namespace MilX.Desktop.Tests;

public class LabelBudgetTests
{
    [Fact]
    public void The_tallest_of_every_region_is_offered_before_any_second_label()
    {
        // twelve tall peaks crowded into the first eighth of a 100–1960 axis, one lone peak at 1500
        var peaks = Enumerable.Range(0, 12).Select(i => (100.0 + 10 * i, 1000.0 - i)).Append((1500.0, 60.0)).ToList();
        var chosen = LabelBudget.Choose(peaks, 100, 1960);
        Assert.Equal((1000.0, 1000.0).Item1 > 0, true);
        // first round: the tallest of the two occupied regions, tallest first; the lone peak comes second, before the cluster's second peak
        Assert.Equal(100.0, chosen[0].X);
        Assert.Equal(1500.0, chosen[1].X);
        Assert.Equal(101.0 + 9, chosen[2].X);
        // the crowded region claims three in all, the quiet one its only peak
        Assert.Equal(4, chosen.Count);
        // a peak under 2 % of the tallest in view is never offered
        var withTiny = peaks.Append((900.0, 5.0)).ToList();
        Assert.DoesNotContain(LabelBudget.Choose(withTiny, 100, 1960), p => p.X == 900.0);
        // zoomed into the quiet stretch, the floor is relative to what is on screen
        Assert.Contains(LabelBudget.Choose(withTiny, 800, 1960), p => p.X == 900.0);
        // the cap
        Assert.Equal(2, LabelBudget.Choose(peaks, 100, 1960, most: 2).Count);
        Assert.Equal(7, LabelBudget.RegionOf(1959, 100, 1960));
        Assert.Equal(0, LabelBudget.RegionOf(50, 100, 1960));
    }

    [AvaloniaFact]
    public void The_spectrum_chart_names_the_lone_peak_the_old_rule_hid()
    {
        var peaks = Enumerable.Range(0, 12).Select(i => new Point(100.0 + 10 * i, 1000.0 - i)).Append(new Point(1500.0, 60.0)).ToList();
        var chart = new SpectrumChart { Peaks = peaks, Width = 980, Height = 420 };
        var window = new Window { Content = chart, Width = 980, Height = 420 };
        window.Show();
        window.UpdateLayout();
        using var bitmap = ChartExport.RenderBitmap(chart, 1);
        // the cluster's masses are 10 Da apart, about 5 px at this width, so the collision rule keeps one of them; the lone peak is drawn too
        Assert.Equal(2, chart.LabelsDrawn);
        chart.TopLabels = 0;
        using var again = ChartExport.RenderBitmap(chart, 1);
        Assert.Equal(0, chart.LabelsDrawn);
        window.Close();
    }
}
