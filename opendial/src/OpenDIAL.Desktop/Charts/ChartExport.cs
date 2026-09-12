using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;

namespace OpenDIAL.Desktop.Charts;

/// <summary>How a figure should leave the application: what it is written as, and how it looks.</summary>
public sealed record ChartExportOptions
{
    /// <summary>"svg" or "png".</summary>
    public string Format { get; init; } = "svg";

    /// <summary>PNG only: how many times the chart's size on screen the picture is rendered at.</summary>
    public double Scale { get; init; } = 3;

    /// <summary>"Screen", "Light" or "Dark": the theme the figure is drawn in, whatever the window's.</summary>
    public string Theme { get; init; } = "Light";

    /// <summary>"Paper" (the theme's own surface), "White" or "Transparent".</summary>
    public string Background { get; init; } = "Paper";

    /// <summary>Multiplies every font on the figure; 1 is what the screen shows.</summary>
    public double FontScale { get; init; } = 1;

    /// <summary>The theme override a chart is drawn with, or null to keep the window's.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public ThemeVariant? Variant => Theme switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        _ => null,
    };

    /// <summary>The paper behind the figure, or null to leave the chart its own surface.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public Color? Paper => Background switch
    {
        "White" => Colors.White,
        "Transparent" => Color.FromArgb(0, 0, 0, 0),
        _ => null,
    };

    public static readonly string[] Themes = { "Screen", "Light", "Dark" };
    public static readonly string[] Backgrounds = { "Paper", "White", "Transparent" };
}

/// <summary>
/// Writes a chart out as a picture: a PNG rendered at a multiple of the screen's resolution, or an
/// SVG drawn through <see cref="SvgCanvas"/> so the lines stay lines and the text stays text.
///
/// Everything a figure is allowed to differ from the screen in goes through <see cref="Render"/>:
/// the theme, the paper and the size of the type are set on the chart, the picture is drawn, and
/// the chart is put back exactly as it was. A chart is drawn, never copied, so this has to be done
/// on the live control and undone whatever happens.
/// </summary>
public static class ChartExport
{
    /// <summary>Renders the control at <paramref name="scale"/> times its on-screen size.</summary>
    public static void SavePng(Control control, string path, double scale = 3)
    {
        ArgumentNullException.ThrowIfNull(control);
        using var bitmap = RenderBitmap(control, scale);
        bitmap.Save(path);
    }

    /// <summary>
    /// The control as a bitmap, at a multiple of its size on screen.
    ///
    /// A chart draws itself into the bitmap through the same path as the screen and the SVG, under
    /// one scaling transform. Handing the control to the bitmap instead — which is what this did —
    /// asks the framework to redraw it at a higher resolution, and a text given a width to fit into
    /// or a rotation then comes out at the wrong size: a title twice as tall as it should be, an
    /// axis label off the edge. Drawing it ourselves keeps the picture identical to the screen at
    /// every resolution. Anything that is not a chart still goes the old way.
    /// </summary>
    public static RenderTargetBitmap RenderBitmap(Control control, double scale)
    {
        ArgumentNullException.ThrowIfNull(control);
        var width = Math.Max(1, (int)Math.Ceiling(control.Bounds.Width * scale));
        var height = Math.Max(1, (int)Math.Ceiling(control.Bounds.Height * scale));
        if (control is not IChartRenderable chart)
        {
            var plain = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96 * scale, 96 * scale));
            plain.Render(control);
            return plain;
        }
        var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        using (context.PushTransform(Matrix.CreateScale(scale, scale)))
        {
            chart.RenderTo(new AvaloniaCanvas(context));
        }
        return bitmap;
    }

    /// <summary>The control drawn as SVG.</summary>
    public static string ToSvg(Control control, Color? background = null)
    {
        ArgumentNullException.ThrowIfNull(control);
        if (control is not IChartRenderable chart) throw new ArgumentException("Only the charts can be written as SVG.", nameof(control));
        var canvas = new SvgCanvas(control.Bounds.Width, control.Bounds.Height, background);
        chart.RenderTo(canvas);
        return canvas.ToSvg();
    }

    public static void SaveSvg(Control control, string path, Color? background = null) =>
        File.WriteAllText(path, ToSvg(control, background));

    /// <summary>
    /// Draws the chart the way the options ask for, hands it to <paramref name="draw"/>, and puts
    /// the chart back the way it was — including when the drawing throws.
    /// </summary>
    public static T Render<T>(Control control, ChartExportOptions options, Func<Control, T> draw)
    {
        ArgumentNullException.ThrowIfNull(control);
        ArgumentNullException.ThrowIfNull(options);
        var variantWas = ChartTheme.GetVariant(control);
        var paperWas = ChartTheme.GetPaper(control);
        var fontWas = control is Controls.ChartBase b ? b.FontScale
            : control is Controls.HeatmapChart h ? h.FontScale
            : control is Controls.RankChart r ? r.FontScale
            : control is Controls.PathwayGraph g ? g.FontScale
            : 1.0;
        try
        {
            ChartTheme.SetVariant(control, options.Variant);
            ChartTheme.SetPaper(control, options.Paper);
            SetFontScale(control, fontWas * options.FontScale);
            return draw(control);
        }
        finally
        {
            ChartTheme.SetVariant(control, variantWas);
            ChartTheme.SetPaper(control, paperWas);
            SetFontScale(control, fontWas);
            control.InvalidateVisual();
        }
    }

    private static void SetFontScale(Control control, double scale)
    {
        switch (control)
        {
            case Controls.ChartBase b: b.FontScale = scale; break;
            case Controls.HeatmapChart h: h.FontScale = scale; break;
            case Controls.RankChart r: r.FontScale = scale; break;
            case Controls.PathwayGraph g: g.FontScale = scale; break;
        }
    }

    /// <summary>Writes the figure, as asked for, and answers with the path it went to.</summary>
    public static string Save(Control control, string path, ChartExportOptions options) =>
        Render(control, options, chart =>
        {
            if (string.Equals(options.Format, "png", StringComparison.OrdinalIgnoreCase))
            {
                SavePng(chart, path, options.Scale);
            }
            else
            {
                // the chart paints the paper itself, so the SVG's own background is only needed when
                // the chart is one of those that draws straight onto the panel behind it; a figure
                // asked for with nothing behind it gets no rectangle at all, since an SVG fill
                // cannot be see-through
                SaveSvg(chart, path, options.Paper is { A: 0 } ? null : options.Paper ?? SurfaceOf(chart));
            }
            return path;
        });

    /// <summary>The surface the chart would draw on, for an SVG that has nothing behind it.</summary>
    private static Color SurfaceOf(Control chart) =>
        Controls.ChartPalette.Surface(ChartTheme.IsDark(chart, Application.Current?.ActualThemeVariant));
}
