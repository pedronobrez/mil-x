using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using OpenDIAL.Desktop.Services;
using OpenDIAL.Desktop.Views;
using Xunit;

namespace OpenDIAL.Desktop.Tests;

/// <summary>
/// The ion table's window docks back when dragged over the left part of the main window, and while
/// it is away the peaks and the spectrum take the whole width rather than leaving its column empty.
/// </summary>
public class DockSnapTests
{
    private static readonly PixelRect Main = new(100, 100, 1600, 1000);

    [Fact]
    public void The_drop_zone_is_the_left_part_of_the_main_window()
    {
        var zone = DockSnap.Zone(Main);
        Assert.Equal(100, zone.X);
        Assert.Equal(100, zone.Y);
        Assert.Equal(640, zone.Width);   // two fifths of 1600
        Assert.Equal(1000, zone.Height);
    }

    [Fact]
    public void A_window_held_over_the_zone_is_over_it_and_one_held_beside_it_is_not()
    {
        var frame = new PixelSize(1180, 820);
        // the grip is the middle of the title bar, so the window's left edge may hang off the screen
        Assert.True(DockSnap.IsOverZone(Main, new PixelPoint(-200, 300), frame, 1));       // grip at x = 390
        Assert.True(DockSnap.IsOverZone(Main, new PixelPoint(100, 100), frame, 1));        // grip at x = 690
        Assert.False(DockSnap.IsOverZone(Main, new PixelPoint(400, 300), frame, 1));       // grip at x = 990: the evidence side
        Assert.False(DockSnap.IsOverZone(Main, new PixelPoint(-200, 1300), frame, 1));     // below the main window
        Assert.False(DockSnap.IsOverZone(Main, new PixelPoint(-200, 60), frame, 1));       // grip above it
    }

    [Fact]
    public void A_window_that_merely_appeared_has_not_been_dragged()
    {
        var shown = new PixelPoint(500, 400);
        Assert.False(DockSnap.IsADrag(shown, shown));
        Assert.False(DockSnap.IsADrag(shown, new PixelPoint(510, 395)));
        Assert.True(DockSnap.IsADrag(shown, new PixelPoint(470, 400)));
        Assert.True(DockSnap.IsADrag(shown, new PixelPoint(500, 440)));
    }

    [AvaloniaFact]
    public void The_table_column_collapses_while_the_table_is_in_its_own_window_and_comes_back()
    {
        var (vm, _, folder) = ReviewWorkspaceTests.NewAnalyticsForVisuals();
        var view = new AnalyticsView { DataContext = vm };
        var window = new Window { Content = view, Width = 1400, Height = 900 };
        window.Show();

        var docked = view.TableColumnWidths;
        Assert.True(docked.Table > 300, $"the table had {docked.Table}px");
        Assert.Equal(6, docked.Splitter, 0.5);

        vm.IonTableDetached = true;
        window.UpdateLayout();
        var away = view.TableColumnWidths;
        Assert.Equal(0, away.Table, 0.5);
        Assert.Equal(0, away.Splitter, 0.5);

        vm.IonTableDetached = false;
        window.UpdateLayout();
        var back = view.TableColumnWidths;
        Assert.Equal(docked.Table, back.Table, 0.5);
        Assert.Equal(docked.Splitter, back.Splitter, 0.5);

        // the drop place is hidden until a window is dragged over it, and is not wider than the table's share
        var preview = view.FindControl<Border>("DockPreview")!;
        Assert.False(preview.IsVisible);
        vm.IonTableDockPreview = true;
        window.UpdateLayout();
        Assert.True(preview.IsVisible);
        Assert.InRange(preview.Bounds.Width, 120, docked.Table + 1);

        window.Close();
        try { Directory.Delete(folder, true); } catch { }
    }
}

/// <summary>The mirror's corner labels go to whichever side of the plot has the lower peaks under them.</summary>
public class SpectrumLabelTests
{
    [Fact]
    public void The_label_takes_the_emptier_side()
    {
        var basePeakAtTheRightEdge = new[] { new Point(100, 5), new Point(313, 60), new Point(579, 100) };
        Assert.True(OpenDIAL.Desktop.Controls.SpectrumChart.LeftIsEmptier(basePeakAtTheRightEdge, 50, 620));
        var basePeakAtTheLeftEdge = new[] { new Point(60, 100), new Point(313, 60), new Point(579, 10) };
        Assert.False(OpenDIAL.Desktop.Controls.SpectrumChart.LeftIsEmptier(basePeakAtTheLeftEdge, 50, 620));
        // nothing at either edge: the right corner, where the label always was
        Assert.False(OpenDIAL.Desktop.Controls.SpectrumChart.LeftIsEmptier(new[] { new Point(300, 100) }, 50, 620));
        Assert.False(OpenDIAL.Desktop.Controls.SpectrumChart.LeftIsEmptier(null, 50, 620));
    }
}
