using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using OpenDIAL.Pipeline.Statistics;

namespace OpenDIAL.Desktop.Controls;

public sealed class NetworkNodeEventArgs : RoutedEventArgs
{
    public NetworkNodeEventArgs(RoutedEvent routedEvent, NetworkNode node) : base(routedEvent) { Node = node; }
    public NetworkNode Node { get; }
}

/// <summary>
/// The molecular network: features whose product spectra look alike, joined by an edge. Members of
/// one lipid class fragment the same way, so they form a cluster; a feature named as one class
/// sitting inside another class's cluster is worth a second look, and an unknown next to a named
/// cluster is a candidate for the same family.
///
/// The layout is a plain spring embedder run once when the data changes: repulsion between every
/// pair, attraction along the edges, cooling over a fixed number of steps. It is deterministic, so
/// the same network always draws the same way.
/// </summary>
public sealed class NetworkGraph : Control, Charts.IChartRenderable
{
    public static readonly StyledProperty<SpectralNetworkResult?> NetworkProperty =
        AvaloniaProperty.Register<NetworkGraph, SpectralNetworkResult?>(nameof(Network));

    public static readonly StyledProperty<int> SelectedFeatureIdProperty =
        AvaloniaProperty.Register<NetworkGraph, int>(nameof(SelectedFeatureId), -1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly RoutedEvent<NetworkNodeEventArgs> NodeClickedEvent =
        RoutedEvent.Register<NetworkGraph, NetworkNodeEventArgs>(nameof(NodeClicked), RoutingStrategies.Bubble);

    private Dictionary<int, Point> _positions = new();
    private List<string> _groups = new();

    static NetworkGraph()
    {
        AffectsRender<NetworkGraph>(NetworkProperty, SelectedFeatureIdProperty);
    }

    public SpectralNetworkResult? Network { get => GetValue(NetworkProperty); set => SetValue(NetworkProperty, value); }
    public int SelectedFeatureId { get => GetValue(SelectedFeatureIdProperty); set => SetValue(SelectedFeatureIdProperty, value); }
    public event EventHandler<NetworkNodeEventArgs> NodeClicked { add => AddHandler(NodeClickedEvent, value); remove => RemoveHandler(NodeClickedEvent, value); }

    private bool Dark => (Application.Current?.ActualThemeVariant ?? Avalonia.Styling.ThemeVariant.Light) == Avalonia.Styling.ThemeVariant.Dark;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == NetworkProperty) Layout();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var network = Network;
        if (network is null || _positions.Count == 0) return;
        var p = e.GetPosition(this);
        NetworkNode? best = null;
        var bestDistance = 14.0;
        foreach (var node in network.Nodes)
        {
            if (!_positions.TryGetValue(node.FeatureId, out var pos)) continue;
            var d = Math.Sqrt((pos.X - p.X) * (pos.X - p.X) + (pos.Y - p.Y) * (pos.Y - p.Y));
            if (d < bestDistance) { bestDistance = d; best = node; }
        }
        if (best is null) return;
        SelectedFeatureId = best.FeatureId;
        RaiseEvent(new NetworkNodeEventArgs(NodeClickedEvent, best));
    }

    /// <summary>Spring embedder, deterministic: same input, same picture.</summary>
    private void Layout()
    {
        _positions = new Dictionary<int, Point>();
        var network = Network;
        if (network is null || network.Nodes.Count == 0) return;
        _groups = network.Nodes.Select(n => n.Group).Distinct().ToList();

        var nodes = network.Nodes;
        var n = nodes.Count;
        var index = nodes.Select((node, i) => (node.FeatureId, i)).ToDictionary(x => x.FeatureId, x => x.i);
        var x = new double[n];
        var y = new double[n];
        // Seeding on a circle keeps the radial symmetry the repulsion then inflates, and the picture
        // stays a ring however long it runs. A scattered start breaks it; the seed is fixed so the
        // same network still draws the same way every time.
        var rng = new Random(42);
        var side = Math.Sqrt(5000.0 * n);
        for (var i = 0; i < n; i++)
        {
            x[i] = (rng.NextDouble() - 0.5) * side;
            y[i] = (rng.NextDouble() - 0.5) * side;
        }

        var edges = network.Edges
            .Where(e => index.ContainsKey(e.SourceId) && index.ContainsKey(e.TargetId))
            .Select(e => (A: index[e.SourceId], B: index[e.TargetId], W: e.Similarity))
            .ToList();

        var steps = n > 400 ? 150 : 400;
        // the drawing area has to grow with the node count or the repulsion flattens everything
        var area = 5000.0 * n;
        var k = Math.Sqrt(area / Math.Max(1, n));
        for (var step = 0; step < steps; step++)
        {
            var temperature = 12.0 * (1.0 - (double)step / steps) + 0.5;
            var dx = new double[n];
            var dy = new double[n];
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    var ddx = x[i] - x[j];
                    var ddy = y[i] - y[j];
                    var dist2 = ddx * ddx + ddy * ddy;
                    if (dist2 < 0.01) { ddx = 0.1; ddy = 0.1; dist2 = 0.02; }
                    var force = k * k / dist2;
                    dx[i] += ddx * force; dy[i] += ddy * force;
                    dx[j] -= ddx * force; dy[j] -= ddy * force;
                }
            }
            foreach (var (a, b, w) in edges)
            {
                var ddx = x[a] - x[b];
                var ddy = y[a] - y[b];
                var dist = Math.Sqrt(ddx * ddx + ddy * ddy);
                if (dist < 0.01) continue;
                var force = dist * dist / k * (0.5 + w);
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
        for (var i = 0; i < n; i++) _positions[nodes[i].FeatureId] = new Point(x[i], y[i]);
    }

    public override void Render(DrawingContext context) => RenderTo(new Charts.AvaloniaCanvas(context));

    public void RenderTo(Charts.ChartCanvas context)
    {
        var network = Network;
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (network is null || network.Nodes.Count == 0 || width < 40 || height < 40)
        {
            if (width > 40 && height > 20)
            {
                var hint = Charts.ChartText.Make("No network. Lower the similarity cut-off, or process a run with MS/MS.",
                    System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 12,
                    new SolidColorBrush(ChartPalette.Faint(Dark)));
                context.DrawText(hint, new Point((width - hint.Width) / 2, height / 2));
            }
            return;
        }
        if (_positions.Count == 0) Layout();

        var minX = _positions.Values.Min(p => p.X);
        var maxX = _positions.Values.Max(p => p.X);
        var minY = _positions.Values.Min(p => p.Y);
        var maxY = _positions.Values.Max(p => p.Y);
        const double margin = 24;
        var scaleX = (width - margin * 2) / Math.Max(1e-6, maxX - minX);
        var scaleY = (height - margin * 2) / Math.Max(1e-6, maxY - minY);
        var scale = Math.Min(scaleX, scaleY);
        Point Map(Point p) => new(margin + (p.X - minX) * scale, margin + (p.Y - minY) * scale);

        // an edge has to read against the panel: the muted ink, thicker the more alike the spectra
        var edgeColour = ChartPalette.Muted(Dark);
        foreach (var edge in network.Edges)
        {
            if (!_positions.TryGetValue(edge.SourceId, out var a) || !_positions.TryGetValue(edge.TargetId, out var b)) continue;
            var touchesSelection = edge.SourceId == SelectedFeatureId || edge.TargetId == SelectedFeatureId;
            var strength = Math.Clamp((edge.Similarity - 0.5) / 0.5, 0.15, 1.0);
            var pen = touchesSelection
                ? new Pen(new SolidColorBrush(ChartPalette.Accent(Dark)), 2.2)
                : new Pen(new SolidColorBrush(Color.FromArgb((byte)(90 + 120 * strength), edgeColour.R, edgeColour.G, edgeColour.B)), 0.9 + 1.4 * strength);
            context.DrawLine(pen, Map(a), Map(b));
        }

        var heights = network.Nodes.Select(n => n.Height).Where(h => h > 0).ToList();
        var maxHeightValue = heights.Count > 0 ? heights.Max() : 1;
        foreach (var node in network.Nodes)
        {
            if (!_positions.TryGetValue(node.FeatureId, out var p)) continue;
            var centre = Map(p);
            var radius = 3.5 + 4.5 * Math.Sqrt(Math.Max(0, node.Height) / maxHeightValue);
            var colour = ChartPalette.ForIndex(Dark, Math.Max(0, _groups.IndexOf(node.Group)));
            var fill = new SolidColorBrush(node.IsAnnotated ? colour : Color.FromArgb(0x66, colour.R, colour.G, colour.B));
            var outline = node.FeatureId == SelectedFeatureId
                ? new Pen(new SolidColorBrush(ChartPalette.Accent(Dark)), 2.5)
                : new Pen(new SolidColorBrush(ChartPalette.Surface(Dark)), 1);
            context.DrawEllipse(fill, outline, centre, radius, radius);
        }

        // label the selected node and its class, which is all that fits without clutter
        var selected = network.Nodes.FirstOrDefault(n => n.FeatureId == SelectedFeatureId);
        if (selected is not null && _positions.TryGetValue(selected.FeatureId, out var sp))
        {
            var centre = Map(sp);
            var text = Charts.ChartText.Make($"{selected.Label}  ·  {selected.Group}", System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, Typeface.Default, 11.5, new SolidColorBrush(ChartPalette.Ink(Dark)));
            var box = new Rect(centre.X + 10, centre.Y - text.Height / 2 - 2, text.Width + 10, text.Height + 4);
            context.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xEE, ChartPalette.Surface(Dark).R, ChartPalette.Surface(Dark).G, ChartPalette.Surface(Dark).B)),
                new Pen(new SolidColorBrush(ChartPalette.Line(Dark)), 1), box, 4, 4);
            context.DrawText(text, new Point(box.X + 5, box.Y + 2));
        }
    }
}
