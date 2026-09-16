using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using MilX.Desktop.Charts;
using MilX.Desktop.Controls;
using MilX.Desktop.Views;
using Xunit;

namespace MilX.Desktop.Tests;

/// <summary>
/// A figure leaves the application looking the way the figure wants, not the way the window does:
/// light from a dark session, on white or on nothing, with larger type for a slide — and the chart
/// on screen is exactly as it was afterwards.
/// </summary>
public class ChartExportTests
{
    private const string DarkPaper = "#1e2124";
    private const string LightPaper = "#ffffff";

    private static (SpectrumChart Chart, Window Window) Spectrum(ThemeVariant variant)
    {
        var chart = new SpectrumChart
        {
            Peaks = new[] { new Point(184.07, 100), new Point(496.34, 42), new Point(760.58, 18) },
            PrecursorMz = 760.58,
            Title = "Representative MS/MS",
        };
        var window = new Window { Content = chart, Width = 560, Height = 320, RequestedThemeVariant = variant };
        window.Show();
        window.UpdateLayout();
        return (chart, window);
    }

    [AvaloniaFact]
    public void A_figure_is_light_while_the_window_stays_dark()
    {
        var (chart, window) = Spectrum(ThemeVariant.Dark);
        Assert.Contains(DarkPaper, ChartExport.ToSvg(chart), StringComparison.Ordinal);

        var light = ChartExport.Render(chart, new ChartExportOptions { Theme = "Light" }, c => ChartExport.ToSvg(c));
        Assert.Contains(LightPaper, light, StringComparison.Ordinal);
        Assert.DoesNotContain(DarkPaper, light, StringComparison.Ordinal);
        Assert.Contains("<text", light, StringComparison.Ordinal);   // still a vector, not a picture of one

        // and the window is left as it was: the chart on screen is dark again
        Assert.Null(ChartTheme.GetVariant(chart));
        Assert.Contains(DarkPaper, ChartExport.ToSvg(chart), StringComparison.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public void A_figure_is_dark_while_the_window_stays_light()
    {
        var (chart, window) = Spectrum(ThemeVariant.Light);
        var dark = ChartExport.Render(chart, new ChartExportOptions { Theme = "Dark" }, c => ChartExport.ToSvg(c));
        Assert.Contains(DarkPaper, dark, StringComparison.Ordinal);
        Assert.Contains(LightPaper, ChartExport.ToSvg(chart), StringComparison.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public void As_on_screen_leaves_the_theme_alone()
    {
        var (chart, window) = Spectrum(ThemeVariant.Dark);
        var same = ChartExport.Render(chart, new ChartExportOptions { Theme = "Screen" }, c => ChartExport.ToSvg(c));
        Assert.Contains(DarkPaper, same, StringComparison.Ordinal);
        window.Close();
    }

    [AvaloniaFact]
    public void The_paper_can_be_white_or_nothing_at_all()
    {
        var (chart, window) = Spectrum(ThemeVariant.Dark);
        var folder = Directory.CreateTempSubdirectory("milx-figures").FullName;

        var onWhite = Path.Combine(folder, "white.svg");
        ChartExport.Save(chart, onWhite, new ChartExportOptions { Theme = "Dark", Background = "White" });
        var white = File.ReadAllText(onWhite);
        Assert.Contains(LightPaper, white, StringComparison.Ordinal);   // white behind dark ink, as asked

        var onNothing = Path.Combine(folder, "clear.svg");
        ChartExport.Save(chart, onNothing, new ChartExportOptions { Theme = "Light", Background = "Transparent" });
        var clear = File.ReadAllText(onNothing);
        // nothing opaque behind the figure: the one rectangle over the whole picture is see-through
        Assert.Contains("fill-opacity=\"0\"", clear, StringComparison.Ordinal);
        Assert.DoesNotContain($"width=\"560\" height=\"320\" fill=\"{LightPaper}\"", clear, StringComparison.Ordinal);

        Assert.Null(ChartTheme.GetPaper(chart));
        Directory.Delete(folder, true);
        window.Close();
    }

    [AvaloniaFact]
    public void The_type_can_be_larger_for_a_slide_and_goes_back_to_what_it_was()
    {
        var (chart, window) = Spectrum(ThemeVariant.Light);
        chart.FontScale = 1.0;
        var screen = ChartExport.ToSvg(chart);
        var slide = ChartExport.Render(chart, new ChartExportOptions { Theme = "Screen", FontScale = 1.8 }, c => ChartExport.ToSvg(c));
        Assert.NotEqual(screen, slide);
        Assert.Equal(1.0, chart.FontScale);
        window.Close();
    }

    [AvaloniaFact]
    public void A_png_is_written_at_the_resolution_asked_for()
    {
        var (chart, window) = Spectrum(ThemeVariant.Dark);
        var folder = Directory.CreateTempSubdirectory("milx-figures").FullName;
        var path = Path.Combine(folder, "figure.png");
        ChartExport.Save(chart, path, new ChartExportOptions { Format = "png", Scale = 2, Theme = "Light" });

        var bytes = File.ReadAllBytes(path);
        Assert.True(bytes.Length > 1000, $"the picture was {bytes.Length} bytes");
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        // the PNG header carries the size, big-endian, from byte 16
        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
        Assert.Equal((int)Math.Ceiling(chart.Bounds.Width * 2), width);
        Directory.Delete(folder, true);
        window.Close();
    }

    [AvaloniaFact]
    public void A_picture_at_three_times_the_size_is_the_same_picture()
    {
        // The title is given a width to fit into and the y label is rotated. Handing the control to
        // the framework to redraw at a higher resolution drew both at the wrong size — a title twice
        // as tall as it should be, the axis label off the edge — while everything around them scaled
        // properly. The ink in the band the title sits in is the cheapest way to see that.
        var chart = new SpectrumChart
        {
            Peaks = new[] { new Point(100, 100), new Point(200, 40) },
            Title = "Representative MS/MS · Cer d12:0/4:0 (spot 196, m/z 288.2543) · mirror: library reference",
            YLabel = "Relative intensity (%)",
            XLabel = "m/z",
        };
        var window = new Window { Content = chart, Width = 540, Height = 240 };
        window.Show();
        window.UpdateLayout();

        var onScreen = TitleDepth(chart, 1);
        var large = TitleDepth(chart, 3);
        Assert.InRange(large, onScreen * 0.8, onScreen * 1.2);
        window.Close();
    }

    /// <summary>
    /// How far down the title's ink reaches, in the figure's own units, at that resolution. A title
    /// drawn too large reaches further down; everything else in that band is paper or a pale rule.
    /// </summary>
    private static double TitleDepth(Control chart, double scale)
    {
        using var bitmap = ChartExport.Render(chart, new ChartExportOptions { Theme = "Light" },
            c => ChartExport.RenderBitmap(c, scale));
        var size = bitmap.PixelSize;
        var raw = new byte[size.Width * size.Height * 4];
        unsafe
        {
            fixed (byte* p = raw) bitmap.CopyPixels(new PixelRect(size), (IntPtr)p, raw.Length, size.Width * 4);
        }
        var rows = Math.Max(1, size.Height / 8);
        var deepest = 0;
        for (var y = 0; y < rows; y++)
        {
            for (var x = 0; x < size.Width; x++)
            {
                var at = (y * size.Width + x) * 4;
                if (raw[at] < 200 || raw[at + 1] < 200 || raw[at + 2] < 200) deepest = y;
            }
        }
        return deepest / scale;
    }

    [AvaloniaFact]
    public void The_dialog_offers_what_the_file_will_be_and_previews_it()
    {
        var (chart, window) = Spectrum(ThemeVariant.Dark);
        var dialog = new ChartExportWindow();
        dialog.Show();
        dialog.Prepare(chart, "Representative MS/MS", new ChartExportOptions { Format = "png", Scale = 4, Theme = "Light", Background = "White", FontScale = 1.4 });

        var options = dialog.Options;
        Assert.Equal("png", options.Format);
        Assert.Equal(4, options.Scale);
        Assert.Equal("Light", options.Theme);
        Assert.Equal("White", options.Background);
        Assert.Equal(1.4, options.FontScale, 2);
        Assert.NotNull(dialog.FindControl<Image>("Preview")!.Source);

        // and the chart behind the dialog is untouched by the preview
        Assert.Null(ChartTheme.GetVariant(chart));
        dialog.Close();
        window.Close();
    }

    [Fact]
    public void A_figure_is_named_after_the_panel_it_came_from()
    {
        Assert.Equal("volcano-log2-fold-change.svg", ChartExportFlow.SuggestedName("VOLCANO · log2 fold change", "svg"));
        Assert.Equal("chart.png", ChartExportFlow.SuggestedName(null, "png"));
        Assert.Equal("chart.svg", ChartExportFlow.SuggestedName("···", "svg"));
    }

    [Fact]
    public void The_settings_of_an_export_are_plain_enough_to_be_saved()
    {
        var options = new ChartExportOptions { Format = "png", Scale = 6, Theme = "Light", Background = "Transparent", FontScale = 1.2 };
        var json = System.Text.Json.JsonSerializer.Serialize(options);
        var back = System.Text.Json.JsonSerializer.Deserialize<ChartExportOptions>(json)!;
        Assert.Equal(options, back);
        Assert.DoesNotContain("Variant", json, StringComparison.Ordinal);
        Assert.Equal(ThemeVariant.Light, back.Variant);
        Assert.Equal(0, back.Paper!.Value.A);
    }
}

/// <summary>The menu that offers the export, on a chart that uses the right button to pan.</summary>
public class ChartMenuTests
{
    [AvaloniaFact]
    public void A_right_click_offers_the_export_and_a_right_drag_still_pans()
    {
        var chart = new SpectrumChart
        {
            Peaks = new[] { new Point(100, 100), new Point(200, 40) },
            Title = "Representative MS/MS",
        };
        var window = new Window { Content = chart, Width = 540, Height = 240 };
        window.Show();
        window.UpdateLayout();

        var middle = new Point(270, 120);
        window.MouseDown(middle, Avalonia.Input.MouseButton.Right);
        window.MouseUp(middle, Avalonia.Input.MouseButton.Right);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        Assert.True(chart.ExportMenuIsOpen, "a right click should offer the export menu");

        window.Close();
    }
}
