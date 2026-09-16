using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using MilX.Pipeline.Statistics;

namespace MilX.Desktop.Controls;

/// <summary>
/// The sample tree of a hierarchical clustering, drawn left to right with the leaves labelled on
/// the right and coloured by class. Replicates of one class should join at a low height and the
/// blanks should hang off on their own; an injection that joins the wrong group is the thing to
/// look at.
/// </summary>
public sealed class Dendrogram : Control, Charts.IChartRenderable
{
    public static readonly StyledProperty<ClusterNode?> RootProperty =
        AvaloniaProperty.Register<Dendrogram, ClusterNode?>(nameof(Root));

    static Dendrogram()
    {
        AffectsRender<Dendrogram>(RootProperty);
    }

    public Dendrogram()
    {
        Charts.ChartExportFlow.AttachMenu(this, () => "clustering tree");
    }

    public ClusterNode? Root { get => GetValue(RootProperty); set => SetValue(RootProperty, value); }

    private bool Dark => Charts.ChartTheme.IsDark(this, Application.Current?.ActualThemeVariant);

    public override void Render(DrawingContext context) => RenderTo(new Charts.AvaloniaCanvas(context));

    public void RenderTo(Charts.ChartCanvas context)
    {
        Charts.ChartTheme.PaintPaper(this, context);
        var root = Root;
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (root is null || width < 40 || height < 20) return;

        var leaves = root.Leaves().ToList();
        if (leaves.Count == 0) return;

        var groups = leaves.Select(l => l.Group).Distinct().ToList();
        var pen = new Pen(new SolidColorBrush(ChartPalette.LineStrong(Dark)), 1.2);
        var textBrush = new SolidColorBrush(ChartPalette.Ink(Dark));
        var mutedBrush = new SolidColorBrush(ChartPalette.Muted(Dark));

        const double labelWidth = 190;
        const double padding = 10;
        var plotWidth = Math.Max(30, width - labelWidth - padding * 2);
        var rowHeight = (height - padding * 2) / leaves.Count;
        if (rowHeight < 6) rowHeight = 6;

        var maxHeight = MaxHeight(root);
        if (maxHeight <= 0) maxHeight = 1;

        var y = new Dictionary<ClusterNode, double>();
        for (var i = 0; i < leaves.Count; i++)
        {
            y[leaves[i]] = padding + rowHeight * (i + 0.5);
        }

        double X(double h) => padding + plotWidth * (1 - h / maxHeight);

        void Draw(ClusterNode node)
        {
            if (node.IsLeaf)
            {
                var yy = y[node];
                var colour = ChartPalette.ForIndex(Dark, groups.IndexOf(node.Group));
                var leafX = padding + plotWidth;
                context.DrawEllipse(new SolidColorBrush(colour), null, new Point(leafX, yy), 3, 3);
                var text = Charts.ChartText.Make(node.Label, System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 11, textBrush);
                context.DrawText(text, new Point(leafX + 8, yy - text.Height / 2));
                var groupText = Charts.ChartText.Make(node.Group, System.Globalization.CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, Typeface.Default, 9.5, mutedBrush);
                context.DrawText(groupText, new Point(leafX + 8 + Math.Min(text.Width + 6, labelWidth - 60), yy - groupText.Height / 2));
                return;
            }

            Draw(node.Left!);
            Draw(node.Right!);
            var yl = y[node.Left!];
            var yr = y[node.Right!];
            y[node] = (yl + yr) / 2;

            var x = X(node.Height);
            var xl = node.Left!.IsLeaf ? padding + plotWidth : X(node.Left.Height);
            var xr = node.Right!.IsLeaf ? padding + plotWidth : X(node.Right.Height);
            context.DrawLine(pen, new Point(x, yl), new Point(xl, yl));
            context.DrawLine(pen, new Point(x, yr), new Point(xr, yr));
            context.DrawLine(pen, new Point(x, yl), new Point(x, yr));
        }

        Draw(root);

        // a scale for the merge height, which is one minus the correlation
        var axisPen = new Pen(new SolidColorBrush(ChartPalette.Line(Dark)), 1);
        context.DrawLine(axisPen, new Point(padding, height - padding + 2), new Point(padding + plotWidth, height - padding + 2));
        var far = Charts.ChartText.Make(maxHeight.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 9.5, mutedBrush);
        context.DrawText(far, new Point(padding, height - padding + 4));
        var near = Charts.ChartText.Make("0", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 9.5, mutedBrush);
        context.DrawText(near, new Point(padding + plotWidth - 6, height - padding + 4));
    }

    private static double MaxHeight(ClusterNode node) =>
        node.IsLeaf ? 0 : Math.Max(node.Height, Math.Max(MaxHeight(node.Left!), MaxHeight(node.Right!)));
}
