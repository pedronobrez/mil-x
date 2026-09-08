using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace OpenDIAL.Desktop.Charts;

/// <summary>
/// Writes a chart out as a picture: a PNG rendered at a multiple of the screen's resolution, or an
/// SVG drawn through <see cref="SvgDrawingContext"/> so the lines stay lines and the text stays text.
/// </summary>
public static class ChartExport
{
    /// <summary>Renders the control at <paramref name="scale"/> times its on-screen size.</summary>
    public static void SavePng(Control control, string path, double scale = 3)
    {
        ArgumentNullException.ThrowIfNull(control);
        var width = Math.Max(1, (int)Math.Ceiling(control.Bounds.Width * scale));
        var height = Math.Max(1, (int)Math.Ceiling(control.Bounds.Height * scale));
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96 * scale, 96 * scale));
        bitmap.Render(control);
        bitmap.Save(path);
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
}
