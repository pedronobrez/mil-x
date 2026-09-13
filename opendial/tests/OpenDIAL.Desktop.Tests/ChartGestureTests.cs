using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using OpenDIAL.Desktop.Controls;
using Xunit;

namespace OpenDIAL.Desktop.Tests;

/// <summary>
/// The trackpad on a laptop with no mouse: two fingers sideways walk along the axis, and the
/// command key reaches the intensity axis. The direction of travel is not a matter of taste — it
/// has to be the direction every other scroller in the application moves in for the same event.
/// </summary>
public class ChartGestureTests
{
    private static (SpectrumChart Chart, Window Window) Zoomed()
    {
        var chart = new SpectrumChart
        {
            Peaks = new[] { new Point(100, 40), new Point(300, 100), new Point(500, 25), new Point(700, 60) },
        };
        var window = new Window { Content = chart, Width = 600, Height = 300 };
        window.Show();
        window.UpdateLayout();
        chart.SetView(300, 500);   // a window inside the data, so there is room to pan
        window.UpdateLayout();
        return (chart, window);
    }

    private static readonly Point Middle = new(300, 150);

    [AvaloniaFact]
    public void A_sideways_swipe_walks_the_axis_the_way_a_scroller_does()
    {
        // what a scroller does with this very event, in this very framework
        var content = new Border { Width = 4000, Height = 80 };
        var scroller = new ScrollViewer { Content = content, Width = 300, Height = 100, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        var scrollWindow = new Window { Content = scroller, Width = 320, Height = 120 };
        scrollWindow.Show();
        scrollWindow.UpdateLayout();
        scroller.Offset = new Vector(1000, 0);
        scrollWindow.UpdateLayout();
        var offsetBefore = scroller.Offset.X;
        scrollWindow.MouseWheel(new Point(150, 50), new Vector(1, 0));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var scrollerWentBack = scroller.Offset.X < offsetBefore;
        scrollWindow.Close();

        var (chart, window) = Zoomed();
        var before = chart.ViewWindow;
        window.MouseWheel(Middle, new Vector(1, 0));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var after = chart.ViewWindow;

        Assert.NotEqual(before.Min, after.Min, 3);
        Assert.Equal(before.Max - before.Min, after.Max - after.Min, 3);   // it moved, it did not zoom
        Assert.Equal(scrollerWentBack, after.Min < before.Min);
        window.Close();
    }

    [AvaloniaFact]
    public void Shift_with_an_ordinary_wheel_walks_it_too()
    {
        var (chart, window) = Zoomed();
        var before = chart.ViewWindow;
        window.MouseWheel(Middle, new Vector(0, 1), RawInputModifiers.Shift);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var after = chart.ViewWindow;
        Assert.True(after.Min < before.Min, $"the window went from {before.Min} to {after.Min}");
        Assert.Equal(before.Max - before.Min, after.Max - after.Min, 3);
        window.Close();
    }

    [AvaloniaFact]
    public void A_plain_wheel_still_zooms_the_axis()
    {
        var (chart, window) = Zoomed();
        var before = chart.ViewWindow;
        window.MouseWheel(Middle, new Vector(0, 1));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        var after = chart.ViewWindow;
        Assert.True(after.Max - after.Min < before.Max - before.Min, "a plain wheel should zoom in");
        window.Close();
    }

    [AvaloniaFact]
    public void The_command_key_stretches_the_intensity_axis_and_a_double_click_puts_it_back()
    {
        var (chart, window) = Zoomed();
        Assert.Equal(1, chart.IntensityZoom, 3);
        window.MouseWheel(Middle, new Vector(0, 1), RawInputModifiers.Meta);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(chart.IntensityZoom > 1, $"the intensity axis stayed at {chart.IntensityZoom}");
        var stretched = chart.IntensityZoom;

        window.MouseWheel(Middle, new Vector(0, -1), RawInputModifiers.Control);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(chart.IntensityZoom < stretched, "the other way should let it back down");

        window.MouseWheel(Middle, new Vector(0, 1), RawInputModifiers.Meta);
        chart.ResetView();
        Assert.Equal(1, chart.IntensityZoom, 3);
        window.Close();
    }
}

/// <summary>
/// The two graphs are now something to go into rather than pictures: the wheel zooms about the
/// pointer, a drag moves the drawing, a node of the network can be pulled to a better place, and a
/// double click puts everything back. The network's own hit test was measured in the layout's
/// coordinates rather than the panel's, so a node could not be clicked at all.
/// </summary>
public class GraphInteractionTests
{
    [Fact]
    public void The_wheel_keeps_the_point_under_the_pointer_where_it_is()
    {
        var view = new GraphView();
        var anchor = new Point(200, 120);
        var before = view.ToDrawing(anchor);
        Assert.True(view.ZoomAbout(anchor, 2));
        var after = view.ToDrawing(anchor);
        Assert.Equal(before.X, after.X, 6);
        Assert.Equal(before.Y, after.Y, 6);
        Assert.Equal(2, view.Zoom, 6);
    }

    [Fact]
    public void A_drag_moves_the_drawing_and_a_reset_puts_it_back()
    {
        var view = new GraphView();
        view.MoveBy(new Vector(40, -25));
        Assert.True(view.IsMoved);
        var p = view.ToDrawing(new Point(140, 75));
        Assert.Equal(100, p.X, 6);
        Assert.Equal(100, p.Y, 6);
        view.Reset();
        Assert.False(view.IsMoved);
        Assert.Equal(new Point(140, 75), view.ToDrawing(new Point(140, 75)));
    }

    [Fact]
    public void The_zoom_stays_within_reach()
    {
        var view = new GraphView();
        for (var i = 0; i < 40; i++) view.ZoomAbout(new Point(0, 0), 2);
        Assert.InRange(view.Zoom, 1, 12);
        for (var i = 0; i < 80; i++) view.ZoomAbout(new Point(0, 0), 0.5);
        Assert.InRange(view.Zoom, 0.4, 1);
    }
}
