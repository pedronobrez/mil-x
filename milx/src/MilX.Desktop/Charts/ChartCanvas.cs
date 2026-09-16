using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Avalonia;
using Avalonia.Media;

namespace MilX.Desktop.Charts;

/// <summary>A chart that can draw itself on any canvas, which is what makes it exportable.</summary>
public interface IChartRenderable
{
    void RenderTo(ChartCanvas canvas);
}

/// <summary>
/// The six things a chart draws — lines, rectangles, ellipses, paths, text, and a clip or a
/// transform pushed around them — as an abstract surface. The screen gets Avalonia's own
/// context; an export gets an SVG writer; the chart's code is the same for both, so what is saved
/// is exactly what was on screen.
///
/// (Avalonia's DrawingContext cannot be subclassed outside its assembly, which is why this exists.)
/// </summary>
public abstract class ChartCanvas
{
    private static readonly ConditionalWeakTable<Geometry, string> Paths = new();

    public abstract void DrawLine(IPen pen, Point a, Point b);
    public abstract void DrawRectangle(IBrush? brush, IPen? pen, Rect rect, double radiusX = 0, double radiusY = 0);
    public abstract void DrawEllipse(IBrush? brush, IPen? pen, Point center, double radiusX, double radiusY);
    public abstract void DrawGeometry(IBrush? brush, IPen? pen, Geometry geometry);
    public abstract void DrawText(FormattedText text, Point origin);
    public abstract IDisposable PushClip(Rect rect);
    public abstract IDisposable PushTransform(Matrix matrix);

    /// <summary>A polyline as a geometry the SVG writer can write back out as a path.</summary>
    public static StreamGeometry Polyline(IReadOnlyList<Point> points, bool close = false)
    {
        var geometry = new StreamGeometry();
        var path = new StringBuilder();
        using (var g = geometry.Open())
        {
            if (points.Count > 0)
            {
                g.BeginFigure(points[0], close);
                path.Append(CultureInfo.InvariantCulture, $"M{F(points[0].X)} {F(points[0].Y)}");
                for (var i = 1; i < points.Count; i++)
                {
                    g.LineTo(points[i]);
                    path.Append(CultureInfo.InvariantCulture, $"L{F(points[i].X)} {F(points[i].Y)}");
                }
                g.EndFigure(close);
                if (close) path.Append('Z');
            }
        }
        Paths.AddOrUpdate(geometry, path.ToString());
        return geometry;
    }

    /// <summary>The path data of a geometry built through <see cref="Polyline"/>, or null.</summary>
    protected static string? PathOf(Geometry geometry) => Paths.TryGetValue(geometry, out var path) ? path : null;

    protected static string F(double v) => double.IsNaN(v) || double.IsInfinity(v) ? "0" : Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// Text for a chart. Avalonia's <see cref="FormattedText"/> does not give its text back, so the
/// text and the face it was made with are kept beside it for the SVG writer.
/// </summary>
public static class ChartText
{
    private sealed record Info(string Text, double Size, FontWeight Weight, FontStyle Style, IBrush? Brush);
    private static readonly ConditionalWeakTable<FormattedText, Info> Registry = new();

    /// <summary>The same arguments as the FormattedText constructor.</summary>
    public static FormattedText Make(string text, CultureInfo culture, FlowDirection flow, Typeface typeface, double size, IBrush? brush)
    {
        var formatted = new FormattedText(text, culture, flow, typeface, size, brush);
        Registry.AddOrUpdate(formatted, new Info(text, size, typeface.Weight, typeface.Style, brush));
        return formatted;
    }

    internal static (string Text, double Size, FontWeight Weight, FontStyle Style, IBrush? Brush)? Describe(FormattedText text) =>
        Registry.TryGetValue(text, out var info) ? (info.Text, info.Size, info.Weight, info.Style, info.Brush) : null;
}

/// <summary>The screen: everything goes straight to Avalonia's context.</summary>
public sealed class AvaloniaCanvas : ChartCanvas
{
    private readonly DrawingContext _context;
    public AvaloniaCanvas(DrawingContext context) => _context = context;

    public override void DrawLine(IPen pen, Point a, Point b) => _context.DrawLine(pen, a, b);
    public override void DrawRectangle(IBrush? brush, IPen? pen, Rect rect, double radiusX = 0, double radiusY = 0) => _context.DrawRectangle(brush, pen, rect, radiusX, radiusY);
    public override void DrawEllipse(IBrush? brush, IPen? pen, Point center, double radiusX, double radiusY) => _context.DrawEllipse(brush, pen, center, radiusX, radiusY);
    public override void DrawGeometry(IBrush? brush, IPen? pen, Geometry geometry) => _context.DrawGeometry(brush, pen, geometry);
    public override void DrawText(FormattedText text, Point origin) => _context.DrawText(text, origin);
    public override IDisposable PushClip(Rect rect) => new State(_context.PushClip(rect));
    public override IDisposable PushTransform(Matrix matrix) => new State(_context.PushTransform(matrix));

    private sealed class State : IDisposable
    {
        private DrawingContext.PushedState _state;
        public State(DrawingContext.PushedState state) => _state = state;
        public void Dispose() => _state.Dispose();
    }
}

/// <summary>
/// The SVG writer: the same calls become elements, with the text as text and the lines as lines,
/// and every pushed clip or transform a group around what follows.
/// </summary>
public sealed class SvgCanvas : ChartCanvas
{
    private readonly StringBuilder _svg = new();
    private readonly Stack<string> _closers = new();
    private int _clipId;

    public SvgCanvas(double width, double height, Color? background = null)
    {
        Width = width;
        Height = height;
        _svg.Append(CultureInfo.InvariantCulture, $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{F(width)}\" height=\"{F(height)}\" viewBox=\"0 0 {F(width)} {F(height)}\" font-family=\"Inter, Helvetica, Arial, sans-serif\">\n");
        if (background is { } bg) _svg.Append(CultureInfo.InvariantCulture, $"<rect width=\"{F(width)}\" height=\"{F(height)}\" fill=\"{Hex(bg)}\"/>\n");
    }

    public double Width { get; }
    public double Height { get; }

    public string ToSvg()
    {
        var s = new StringBuilder(_svg.ToString());
        foreach (var closer in _closers) s.Append(closer);
        return s + "</svg>\n";
    }

    public override void DrawLine(IPen pen, Point a, Point b) =>
        _svg.Append(CultureInfo.InvariantCulture, $"<line x1=\"{F(a.X)}\" y1=\"{F(a.Y)}\" x2=\"{F(b.X)}\" y2=\"{F(b.Y)}\"{Stroke(pen)}/>\n");

    public override void DrawRectangle(IBrush? brush, IPen? pen, Rect rect, double radiusX = 0, double radiusY = 0) =>
        _svg.Append(CultureInfo.InvariantCulture, $"<rect x=\"{F(rect.X)}\" y=\"{F(rect.Y)}\" width=\"{F(rect.Width)}\" height=\"{F(rect.Height)}\"{(radiusX > 0 ? $" rx=\"{F(radiusX)}\"" : string.Empty)}{Fill(brush)}{Stroke(pen)}/>\n");

    public override void DrawEllipse(IBrush? brush, IPen? pen, Point center, double radiusX, double radiusY) =>
        _svg.Append(CultureInfo.InvariantCulture, $"<ellipse cx=\"{F(center.X)}\" cy=\"{F(center.Y)}\" rx=\"{F(radiusX)}\" ry=\"{F(radiusY)}\"{Fill(brush)}{Stroke(pen)}/>\n");

    public override void DrawGeometry(IBrush? brush, IPen? pen, Geometry geometry)
    {
        var path = PathOf(geometry);
        if (!string.IsNullOrEmpty(path))
        {
            _svg.Append(CultureInfo.InvariantCulture, $"<path d=\"{path}\"{Fill(brush)}{Stroke(pen)}/>\n");
            return;
        }
        var b = geometry.Bounds;
        _svg.Append(CultureInfo.InvariantCulture, $"<rect x=\"{F(b.X)}\" y=\"{F(b.Y)}\" width=\"{F(b.Width)}\" height=\"{F(b.Height)}\"{Fill(brush)}{Stroke(pen)}/>\n");
    }

    public override void DrawText(FormattedText text, Point origin)
    {
        var info = ChartText.Describe(text);
        if (info is null) return;
        var (content, size, weight, style, brush) = info.Value;
        if (string.IsNullOrEmpty(content)) return;
        // a trimmed label on screen is trimmed here too, by the same rule of thumb: what fits its box
        if (text.MaxTextWidth > 0 && text.MaxTextWidth < double.MaxValue && text.MaxLineCount == 1)
        {
            var perChar = Math.Max(1, text.Width / Math.Max(1, content.Length));
            var fits = (int)Math.Floor(text.MaxTextWidth / perChar);
            if (fits < content.Length && fits > 1) content = content[..(fits - 1)] + "…";
        }
        var weightAttribute = weight >= FontWeight.SemiBold ? " font-weight=\"600\"" : string.Empty;
        var styleAttribute = style == FontStyle.Italic ? " font-style=\"italic\"" : string.Empty;
        _svg.Append(CultureInfo.InvariantCulture,
            $"<text x=\"{F(origin.X)}\" y=\"{F(origin.Y + text.Baseline)}\" font-size=\"{F(size)}\"{weightAttribute}{styleAttribute}{Fill(brush)}>{Escape(content)}</text>\n");
    }

    public override IDisposable PushClip(Rect rect)
    {
        var id = "clip" + ++_clipId;
        _svg.Append(CultureInfo.InvariantCulture, $"<clipPath id=\"{id}\"><rect x=\"{F(rect.X)}\" y=\"{F(rect.Y)}\" width=\"{F(rect.Width)}\" height=\"{F(rect.Height)}\"/></clipPath>\n<g clip-path=\"url(#{id})\">\n");
        return Push();
    }

    public override IDisposable PushTransform(Matrix m)
    {
        _svg.Append(CultureInfo.InvariantCulture, $"<g transform=\"matrix({F(m.M11)} {F(m.M12)} {F(m.M21)} {F(m.M22)} {F(m.M31)} {F(m.M32)})\">\n");
        return Push();
    }

    private IDisposable Push()
    {
        _closers.Push("</g>\n");
        return new Popper(this);
    }

    private sealed class Popper : IDisposable
    {
        private readonly SvgCanvas _owner;
        private bool _done;
        public Popper(SvgCanvas owner) => _owner = owner;
        public void Dispose()
        {
            if (_done) return;
            _done = true;
            if (_owner._closers.Count > 0) _owner._svg.Append(_owner._closers.Pop());
        }
    }

    private static string Hex(Color c) => $"#{c.R:x2}{c.G:x2}{c.B:x2}";

    private static (string Hex, double Opacity)? Paint(IBrush? brush) => brush switch
    {
        ISolidColorBrush solid => (Hex(solid.Color), solid.Color.A / 255.0 * solid.Opacity),
        null => null,
        _ => ("#888888", brush.Opacity),
    };

    private static string Fill(IBrush? brush)
    {
        var paint = Paint(brush);
        if (paint is null) return " fill=\"none\"";
        var (hex, opacity) = paint.Value;
        return $" fill=\"{hex}\"" + (opacity < 0.999 ? $" fill-opacity=\"{F(opacity)}\"" : string.Empty);
    }

    private static string Stroke(IPen? pen)
    {
        if (pen is null) return string.Empty;
        var paint = Paint(pen.Brush);
        if (paint is null) return string.Empty;
        var (hex, opacity) = paint.Value;
        var s = $" stroke=\"{hex}\" stroke-width=\"{F(pen.Thickness)}\"";
        if (opacity < 0.999) s += $" stroke-opacity=\"{F(opacity)}\"";
        if (pen.DashStyle?.Dashes is { Count: > 0 } dashes) s += $" stroke-dasharray=\"{string.Join(' ', dashes.Select(d => F(d * pen.Thickness)))}\"";
        if (pen.LineCap == PenLineCap.Round) s += " stroke-linecap=\"round\"";
        return s;
    }

    private static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
