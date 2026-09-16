using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using MilX.Pipeline.Statistics.Pathways;

namespace MilX.Desktop.Controls;

/// <summary>
/// The lipid reaction network as BioPAN draws it: the classes (or species, or fatty acids) as
/// nodes, the reactions as arrows, green where the reaction runs faster in the first class than in
/// the second, purple where it runs slower, grey where nothing changed, and dotted where it could
/// not be tested. A pathway chosen in the table lights up as a chain. The layout is a spring
/// embedder with a fixed seed, so the same network draws the same way every time.
/// </summary>
public sealed class PathwayGraph : Control, Charts.IChartRenderable
{
    public static readonly StyledProperty<PathwayResult?> ResultProperty =
        AvaloniaProperty.Register<PathwayGraph, PathwayResult?>(nameof(Result));

    public static readonly StyledProperty<ReactionScore?> SelectedReactionProperty =
        AvaloniaProperty.Register<PathwayGraph, ReactionScore?>(nameof(SelectedReaction), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<IReadOnlyList<string>?> HighlightedChainProperty =
        AvaloniaProperty.Register<PathwayGraph, IReadOnlyList<string>?>(nameof(HighlightedChain));

    public static readonly StyledProperty<bool> ShowUnchangedProperty =
        AvaloniaProperty.Register<PathwayGraph, bool>(nameof(ShowUnchanged), true);

    public static readonly StyledProperty<bool> ShowUntestedProperty =
        AvaloniaProperty.Register<PathwayGraph, bool>(nameof(ShowUntested), true);

    public static readonly StyledProperty<double> FontScaleProperty =
        AvaloniaProperty.Register<PathwayGraph, double>(nameof(FontScale), 1.0);

    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<PathwayGraph, string?>(nameof(Title));

    private Dictionary<string, Point> _positions = new();
    private readonly List<(ReactionScore Reaction, Point A, Point B)> _drawnEdges = new();
    private readonly Dictionary<string, Point> _drawnNodes = new();

    static PathwayGraph()
    {
        AffectsRender<PathwayGraph>(ResultProperty, SelectedReactionProperty, HighlightedChainProperty, ShowUnchangedProperty, ShowUntestedProperty, FontScaleProperty, TitleProperty);
    }

    public PathwayGraph()
    {
        Charts.ChartExportFlow.AttachMenu(this, () => Title ?? "pathways");
    }

    public PathwayResult? Result { get => GetValue(ResultProperty); set => SetValue(ResultProperty, value); }
    public ReactionScore? SelectedReaction { get => GetValue(SelectedReactionProperty); set => SetValue(SelectedReactionProperty, value); }
    public IReadOnlyList<string>? HighlightedChain { get => GetValue(HighlightedChainProperty); set => SetValue(HighlightedChainProperty, value); }
    public bool ShowUnchanged { get => GetValue(ShowUnchangedProperty); set => SetValue(ShowUnchangedProperty, value); }
    public bool ShowUntested { get => GetValue(ShowUntestedProperty); set => SetValue(ShowUntestedProperty, value); }
    public double FontScale { get => GetValue(FontScaleProperty); set => SetValue(FontScaleProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }

    private bool Dark => Charts.ChartTheme.IsDark(this, Application.Current?.ActualThemeVariant);

    /// <summary>BioPAN's colours: green for a reaction running faster in the first class, purple for slower.</summary>
    public static Color ActiveColor => Color.Parse("#2f9e5f");
    public static Color SuppressedColor => Color.Parse("#8a5bd8");

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ResultProperty) Layout();
    }

    private readonly GraphView _graph = new();
    private Point? _dragFrom;
    private bool _dragged;

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_dragFrom is not { } from) return;
        var here = e.GetPosition(this);
        var delta = here - from;
        if (!_dragged && Math.Abs(delta.X) + Math.Abs(delta.Y) < 3) return;
        _dragged = true;
        _dragFrom = here;
        _graph.MoveBy(delta);
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _dragFrom = null;
        _dragged = false;
        e.Pointer.Capture(null);
    }

    /// <summary>The wheel goes into the map, about the pointer; a double click lays it flat again.</summary>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (_graph.ZoomAbout(e.GetPosition(this), e.Delta.Y > 0 ? 1.2 : 1 / 1.2)) InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (e.ClickCount == 2)
        {
            _graph.Reset();
            InvalidateVisual();
            return;
        }
        _dragFrom = e.GetPosition(this);
        _dragged = false;
        e.Pointer.Capture(this);
        if (Result is null) return;
        // the drawing is zoomed and moved under the pointer, so the pointer comes back through it
        var p = _graph.ToDrawing(e.GetPosition(this));
        // an edge under the pointer wins; the distance to the segment, not to its line
        ReactionScore? best = null;
        var bestDistance = 7.0;
        foreach (var (reaction, a, b) in _drawnEdges)
        {
            var d = DistanceToSegment(p, a, b);
            if (d < bestDistance) { bestDistance = d; best = reaction; }
        }
        if (best is not null) { SelectedReaction = best; return; }
        // otherwise a node: select the reaction that leaves it, if one does
        foreach (var (name, centre) in _drawnNodes)
        {
            if (Math.Abs(centre.X - p.X) < 16 && Math.Abs(centre.Y - p.Y) < 16)
            {
                var leaving = Result.Reactions.FirstOrDefault(r => r.Tested && r.Reactant == name) ?? Result.Reactions.FirstOrDefault(r => r.Reactant == name || r.Product == name);
                if (leaving is not null) SelectedReaction = leaving;
                return;
            }
        }
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len2 = dx * dx + dy * dy;
        var t = len2 < 1e-9 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2, 0, 1);
        var qx = a.X + t * dx;
        var qy = a.Y + t * dy;
        return Math.Sqrt((p.X - qx) * (p.X - qx) + (p.Y - qy) * (p.Y - qy));
    }

    /// <summary>Spring embedder over the nodes that take part in a drawn reaction, seeded on a circle.</summary>
    private void Layout()
    {
        _positions = new Dictionary<string, Point>();
        var result = Result;
        if (result is null) return;
        var names = result.Nodes.Select(n => n.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
        var n = names.Count;
        if (n == 0) return;
        var index = names.Select((name, i) => (name, i)).ToDictionary(x => x.name, x => x.i, StringComparer.Ordinal);
        var x = new double[n];
        var y = new double[n];
        var radius = 100.0 + 12.0 * n;
        for (var i = 0; i < n; i++)
        {
            var angle = 2 * Math.PI * i / n;
            x[i] = radius * Math.Cos(angle);
            y[i] = radius * Math.Sin(angle);
        }
        var edges = result.Reactions
            .Where(r => index.ContainsKey(r.Reactant) && index.ContainsKey(r.Product))
            .Select(r => (A: index[r.Reactant], B: index[r.Product]))
            .Distinct()
            .ToList();
        var area = 40000.0 + 9000.0 * n;
        var k = Math.Sqrt(area / n);
        const int steps = 500;
        for (var step = 0; step < steps; step++)
        {
            var temperature = 10.0 * (1.0 - (double)step / steps) + 0.3;
            var dx = new double[n];
            var dy = new double[n];
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    var ddx = x[i] - x[j];
                    var ddy = y[i] - y[j];
                    var dist2 = Math.Max(0.01, ddx * ddx + ddy * ddy);
                    var force = k * k / dist2;
                    dx[i] += ddx * force; dy[i] += ddy * force;
                    dx[j] -= ddx * force; dy[j] -= ddy * force;
                }
            }
            foreach (var (a, b) in edges)
            {
                var ddx = x[a] - x[b];
                var ddy = y[a] - y[b];
                var dist = Math.Sqrt(ddx * ddx + ddy * ddy);
                if (dist < 0.01) continue;
                var force = dist * dist / k;
                dx[a] -= ddx / dist * force; dy[a] -= ddy / dist * force;
                dx[b] += ddx / dist * force; dy[b] += ddy / dist * force;
            }
            for (var i = 0; i < n; i++)
            {
                var len = Math.Sqrt(dx[i] * dx[i] + dy[i] * dy[i]);
                if (len < 1e-9) continue;
                var move = Math.Min(len, temperature);
                x[i] += dx[i] / len * move;
                y[i] += dy[i] / len * move;
            }
        }
        for (var i = 0; i < n; i++) _positions[names[i]] = new Point(x[i], y[i]);
    }

    public override void Render(DrawingContext context) => RenderTo(new Charts.AvaloniaCanvas(context));

    public void RenderTo(Charts.ChartCanvas context)
    {
        Charts.ChartTheme.PaintPaper(this, context);
        using var view = context.PushTransform(_graph.Matrix);
        _drawnEdges.Clear();
        _drawnNodes.Clear();
        var result = Result;
        var width = Bounds.Width;
        var height = Bounds.Height;
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        if (result is null || result.Nodes.Count == 0 || width < 60 || height < 60)
        {
            if (width > 60 && height > 20)
            {
                var hint = Charts.ChartText.Make("No network yet. Compute the pathways; the classes come from the confirmed analytes.",
                    culture, FlowDirection.LeftToRight, Typeface.Default, 12 * FontScale, new SolidColorBrush(ChartPalette.Faint(Dark)));
                context.DrawText(hint, new Point((width - hint.Width) / 2, height / 2));
            }
            return;
        }
        if (_positions.Count == 0) Layout();

        var top = 8.0;
        if (!string.IsNullOrEmpty(Title))
        {
            var title = Charts.ChartText.Make(Title!, culture, FlowDirection.LeftToRight, new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, FontWeight.SemiBold), 12 * FontScale, new SolidColorBrush(ChartPalette.Ink(Dark)));
            context.DrawText(title, new Point((width - title.Width) / 2, 6));
            top = title.Height + 12;
        }
        var minX = _positions.Values.Min(p => p.X);
        var maxX = _positions.Values.Max(p => p.X);
        var minY = _positions.Values.Min(p => p.Y);
        var maxY = _positions.Values.Max(p => p.Y);
        var few = result.Nodes.Count <= 40;
        var margin = few ? 70.0 : 30.0;
        var legendWidth = 150.0;
        var scale = Math.Min((width - margin * 2 - legendWidth) / Math.Max(1e-6, maxX - minX), (height - top - margin * 2) / Math.Max(1e-6, maxY - minY));
        var offsetX = margin + ((width - legendWidth - margin * 2) - (maxX - minX) * scale) / 2;
        var offsetY = top + margin + ((height - top - margin * 2) - (maxY - minY) * scale) / 2;
        Point Map(Point p) => new(offsetX + (p.X - minX) * scale, offsetY + (p.Y - minY) * scale);

        var nodeRadius = few ? 9.0 : 5.0;
        var highlighted = HighlightedChain;
        var highlightedEdges = new HashSet<(string, string)>();
        if (highlighted is { Count: > 1 })
        {
            for (var i = 0; i + 1 < highlighted.Count; i++) highlightedEdges.Add((highlighted[i], highlighted[i + 1]));
        }

        // edges first, arrows pointing from reactant to product; both directions of a pair bow apart
        var pairs = result.Reactions.GroupBy(r => (Math.Min(string.CompareOrdinal(r.Reactant, r.Product), 0) < 0 ? r.Reactant + "|" + r.Product : r.Product + "|" + r.Reactant)).ToDictionary(g => g.Key, g => g.Count());
        foreach (var reaction in result.Reactions.OrderBy(r => r.Status == "unchanged" ? 0 : r.Tested ? 2 : 1))
        {
            if (!_positions.TryGetValue(reaction.Reactant, out var pa) || !_positions.TryGetValue(reaction.Product, out var pb)) continue;
            if (!reaction.Tested && !ShowUntested) continue;
            if (reaction.Status == "unchanged" && !ShowUnchanged) continue;
            var a = Map(pa);
            var b = Map(pb);
            var isSelected = ReferenceEquals(reaction, SelectedReaction) || (SelectedReaction is not null && reaction.Reactant == SelectedReaction.Reactant && reaction.Product == SelectedReaction.Product);
            var inChain = highlightedEdges.Contains((reaction.Reactant, reaction.Product));
            var colour = reaction.Status switch
            {
                "active" => ActiveColor,
                "suppressed" => SuppressedColor,
                "unchanged" => ChartPalette.Muted(Dark),
                _ => ChartPalette.Faint(Dark),
            };
            var alpha = highlighted is { Count: > 1 } && !inChain ? (byte)70 : reaction.Status == "unchanged" ? (byte)150 : (byte)230;
            var thickness = reaction.Tested ? 1.2 + Math.Min(3.0, Math.Abs(reaction.Z)) * 0.9 : 1.0;
            if (isSelected || inChain) thickness += 1.2;
            var pen = new Pen(new SolidColorBrush(Color.FromArgb(alpha, colour.R, colour.G, colour.B)), thickness)
            {
                DashStyle = reaction.Tested ? null : new DashStyle(new double[] { 3, 3 }, 0),
            };
            // shorten to the node edges, and bow when the reverse reaction is drawn too
            var dx = b.X - a.X;
            var dy = b.Y - a.Y;
            var len = Math.Max(1e-6, Math.Sqrt(dx * dx + dy * dy));
            var ux = dx / len;
            var uy = dy / len;
            var key = string.CompareOrdinal(reaction.Reactant, reaction.Product) < 0 ? reaction.Reactant + "|" + reaction.Product : reaction.Product + "|" + reaction.Reactant;
            var bow = pairs.TryGetValue(key, out var count) && count > 1 ? 6.0 : 0.0;
            var start = new Point(a.X + ux * (nodeRadius + 2) - uy * bow, a.Y + uy * (nodeRadius + 2) + ux * bow);
            var end = new Point(b.X - ux * (nodeRadius + 6) - uy * bow, b.Y - uy * (nodeRadius + 6) + ux * bow);
            context.DrawLine(pen, start, end);
            // the arrowhead
            var tip = new Point(b.X - ux * (nodeRadius + 1) - uy * bow, b.Y - uy * (nodeRadius + 1) + ux * bow);
            var head = 5.0 + thickness;
            var left = new Point(tip.X - ux * head - uy * head * 0.55, tip.Y - uy * head + ux * head * 0.55);
            var right = new Point(tip.X - ux * head + uy * head * 0.55, tip.Y - uy * head - ux * head * 0.55);
            context.DrawGeometry(new SolidColorBrush(Color.FromArgb(alpha, colour.R, colour.G, colour.B)), null, Charts.ChartCanvas.Polyline(new[] { tip, left, right }, close: true));
            _drawnEdges.Add((reaction, start, end));
        }

        // the nodes, coloured by whether they rose or fell between the classes
        var ink = new SolidColorBrush(ChartPalette.Ink(Dark));
        var surface = ChartPalette.Surface(Dark);
        foreach (var node in result.Nodes)
        {
            if (!_positions.TryGetValue(node.Name, out var p)) continue;
            var centre = Map(p);
            _drawnNodes[node.Name] = centre;
            var inChain = highlighted?.Contains(node.Name) == true;
            var change = node.Log2Change;
            var fill = double.IsNaN(change) ? ChartPalette.Faint(Dark)
                : change > 0.5 ? Color.FromArgb(0xB0, ActiveColor.R, ActiveColor.G, ActiveColor.B)
                : change < -0.5 ? Color.FromArgb(0xB0, SuppressedColor.R, SuppressedColor.G, SuppressedColor.B)
                : ChartPalette.LineStrong(Dark);
            var outline = inChain ? new Pen(new SolidColorBrush(ChartPalette.Accent(Dark)), 2.5) : new Pen(new SolidColorBrush(ChartPalette.Ink(Dark)), 1);
            context.DrawEllipse(new SolidColorBrush(fill), outline, centre, nodeRadius, nodeRadius);
            if (few || inChain)
            {
                var label = Charts.ChartText.Make(node.Name, culture, FlowDirection.LeftToRight,
                    new Typeface(Typeface.Default.FontFamily, FontStyle.Normal, inChain ? FontWeight.SemiBold : FontWeight.Normal), (few ? 11.5 : 10.5) * FontScale, ink);
                var box = new Rect(centre.X - label.Width / 2 - 3, centre.Y + nodeRadius + 3, label.Width + 6, label.Height + 2);
                context.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xD8, surface.R, surface.G, surface.B)), null, box, 3, 3);
                context.DrawText(label, new Point(box.X + 3, box.Y + 1));
            }
        }

        // the legend
        var lx = width - legendWidth + 6;
        var ly = top + 6;
        void Entry(string text, Color colour, bool dashed)
        {
            var pen = new Pen(new SolidColorBrush(colour), 2.2) { DashStyle = dashed ? new DashStyle(new double[] { 3, 3 }, 0) : null };
            context.DrawLine(pen, new Point(lx, ly + 7), new Point(lx + 22, ly + 7));
            var t = Charts.ChartText.Make(text, culture, FlowDirection.LeftToRight, Typeface.Default, 10.5 * FontScale, new SolidColorBrush(ChartPalette.Muted(Dark)));
            context.DrawText(t, new Point(lx + 28, ly));
            ly += t.Height + 4;
        }
        Entry($"faster in {result.ClassA}", ActiveColor, false);
        Entry($"slower in {result.ClassA}", SuppressedColor, false);
        Entry("unchanged", ChartPalette.Muted(Dark), false);
        Entry("not testable", ChartPalette.Faint(Dark), true);
        var note = Charts.ChartText.Make($"|Z| ≥ {result.Threshold:0.###}", culture, FlowDirection.LeftToRight, Typeface.Default, 10.5 * FontScale, new SolidColorBrush(ChartPalette.Faint(Dark)));
        context.DrawText(note, new Point(lx, ly + 2));
    }
}
