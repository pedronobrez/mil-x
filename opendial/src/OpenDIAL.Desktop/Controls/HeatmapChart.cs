using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using OpenDIAL.Pipeline.Statistics;

namespace OpenDIAL.Desktop.Controls;

/// <summary>A matrix to draw as cells: rows, columns, and a value per cell, with the colour of each row's and column's group.</summary>
public sealed record HeatmapData(
    IReadOnlyList<string> RowLabels,
    IReadOnlyList<string> ColumnLabels,
    double[,] Values,
    IReadOnlyList<string>? RowGroups = null,
    IReadOnlyList<string>? ColumnGroups = null,
    ClusterNode? RowTree = null,
    ClusterNode? ColumnTree = null,
    string ValueName = "",
    bool Symmetric = false,
    IReadOnlyList<int>? RowIds = null)
{
    public static HeatmapData From(HeatmapResult r) => new(r.RowLabels, r.ColumnLabels, r.Values, r.RowGroups, r.ColumnGroups, r.RowTree, r.ColumnTree, r.ValueName, false, r.RowFeatureIds);
    public static HeatmapData From(CorrelationMatrix m) => new(m.Labels, m.Labels, m.Values, m.Groups, m.Groups, null, null, m.Kind + " correlation", true, m.FeatureIds);
}

/// <summary>
/// The heatmap: a colour per cell, the trees that ordered the rows and columns drawn beside them,
/// a strip of group colours along the top, the labels, and a colour scale. Values are mapped from
/// blue through white to red around zero when they straddle it, and through a sequential ramp
/// otherwise. Clicking a row reports its id, so a feature can be opened from its row.
/// </summary>
public sealed class HeatmapChart : Control, Charts.IChartRenderable
{
    public static readonly StyledProperty<HeatmapData?> DataProperty = AvaloniaProperty.Register<HeatmapChart, HeatmapData?>(nameof(Data));
    public static readonly StyledProperty<string> ColorScaleProperty = AvaloniaProperty.Register<HeatmapChart, string>(nameof(ColorScale), "Blue–white–red");
    public static readonly StyledProperty<bool> ShowRowLabelsProperty = AvaloniaProperty.Register<HeatmapChart, bool>(nameof(ShowRowLabels), true);
    public static readonly StyledProperty<bool> ShowColumnLabelsProperty = AvaloniaProperty.Register<HeatmapChart, bool>(nameof(ShowColumnLabels), true);
    public static readonly StyledProperty<bool> ShowTreesProperty = AvaloniaProperty.Register<HeatmapChart, bool>(nameof(ShowTrees), true);
    public static readonly StyledProperty<bool> ShowValuesProperty = AvaloniaProperty.Register<HeatmapChart, bool>(nameof(ShowValues));
    public static readonly StyledProperty<double> FontScaleProperty = AvaloniaProperty.Register<HeatmapChart, double>(nameof(FontScale), 1.0);
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<HeatmapChart, string?>(nameof(Title));
    public static readonly StyledProperty<int> SelectedRowIdProperty = AvaloniaProperty.Register<HeatmapChart, int>(nameof(SelectedRowId), -1, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly string[] ColorScales = { "Blue–white–red", "Viridis", "Greys", "Green–black–red" };

    private Point? _hover;

    static HeatmapChart()
    {
        AffectsRender<HeatmapChart>(DataProperty, ColorScaleProperty, ShowRowLabelsProperty, ShowColumnLabelsProperty, ShowTreesProperty, ShowValuesProperty, FontScaleProperty, TitleProperty, SelectedRowIdProperty);
    }

    public HeatmapChart()
    {
        ClipToBounds = true;
        MinHeight = 80;
    }

    public HeatmapData? Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }
    public string ColorScale { get => GetValue(ColorScaleProperty); set => SetValue(ColorScaleProperty, value); }
    public bool ShowRowLabels { get => GetValue(ShowRowLabelsProperty); set => SetValue(ShowRowLabelsProperty, value); }
    public bool ShowColumnLabels { get => GetValue(ShowColumnLabelsProperty); set => SetValue(ShowColumnLabelsProperty, value); }
    public bool ShowTrees { get => GetValue(ShowTreesProperty); set => SetValue(ShowTreesProperty, value); }
    public bool ShowValues { get => GetValue(ShowValuesProperty); set => SetValue(ShowValuesProperty, value); }
    public double FontScale { get => GetValue(FontScaleProperty); set => SetValue(FontScaleProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    /// <summary>The id of the row last clicked, when the data carries row ids.</summary>
    public int SelectedRowId { get => GetValue(SelectedRowIdProperty); set => SetValue(SelectedRowIdProperty, value); }

    private bool Dark => (Application.Current?.ActualThemeVariant ?? Avalonia.Styling.ThemeVariant.Light) == Avalonia.Styling.ThemeVariant.Dark;

    private Rect _cells;
    private double _cellW, _cellH;

    public override void Render(DrawingContext context) => RenderTo(new Charts.AvaloniaCanvas(context));

    public void RenderTo(Charts.ChartCanvas ctx)
    {
        var data = Data;
        var w = Bounds.Width;
        var h = Bounds.Height;
        ctx.DrawRectangle(new SolidColorBrush(ChartPalette.Surface(Dark)), null, new Rect(Bounds.Size));
        if (data is null || data.RowLabels.Count == 0 || data.ColumnLabels.Count == 0 || w < 60 || h < 40)
        {
            var empty = Text("No data", 12, new SolidColorBrush(ChartPalette.Faint(Dark)));
            ctx.DrawText(empty, new Point(w / 2 - empty.Width / 2, h / 2 - empty.Height / 2));
            return;
        }
        var rows = data.RowLabels.Count;
        var cols = data.ColumnLabels.Count;
        var ink = new SolidColorBrush(ChartPalette.Ink(Dark));
        var muted = new SolidColorBrush(ChartPalette.Muted(Dark));

        // layout: a title, the column tree, the group strip, the cells, the column labels below, the row labels and tree
        var titleH = string.IsNullOrEmpty(Title) ? 6 : 22 * FontScale;
        var fontSize = 9.5 * FontScale;
        var rowLabelW = ShowRowLabels ? Math.Min(w * 0.35, data.RowLabels.Max(l => Text(l, fontSize, ink).Width) + 10) : 0;
        var colLabelH = ShowColumnLabels ? Math.Min(h * 0.3, data.ColumnLabels.Max(l => Text(l, fontSize, ink).Width) + 10) : 0;
        var rowTreeW = ShowTrees && data.RowTree is not null && rows > 1 ? 60 : 0;
        var colTreeH = ShowTrees && data.ColumnTree is not null && cols > 1 ? 50 : 0;
        var stripH = data.ColumnGroups is not null ? 8 : 0;
        var scaleW = 54;
        var left = 8 + rowTreeW;
        var top = titleH + colTreeH + stripH + 4;
        var cellsW = Math.Max(10, w - left - rowLabelW - scaleW - 16);
        var cellsH = Math.Max(10, h - top - colLabelH - 8);
        _cells = new Rect(left, top, cellsW, cellsH);
        _cellW = cellsW / cols;
        _cellH = cellsH / rows;

        if (!string.IsNullOrEmpty(Title))
        {
            ctx.DrawText(Text(Title, 12 * FontScale, ink, FontWeight.SemiBold), new Point(left, 4));
        }

        // the range the colours span
        double min = double.MaxValue, max = double.MinValue;
        for (var r = 0; r < rows; r++)
            for (var c = 0; c < cols; c++)
            {
                var v = data.Values[r, c];
                if (double.IsNaN(v)) continue;
                min = Math.Min(min, v);
                max = Math.Max(max, v);
            }
        if (min == double.MaxValue) { min = 0; max = 1; }
        var diverging = min < 0 && max > 0;
        if (diverging || data.Symmetric) { var m = Math.Max(Math.Abs(min), Math.Abs(max)); min = -m; max = m; diverging = true; }
        if (max <= min) max = min + 1;

        // cells
        for (var r = 0; r < rows; r++)
        {
            for (var c = 0; c < cols; c++)
            {
                var v = data.Values[r, c];
                var rect = new Rect(left + c * _cellW, top + r * _cellH, Math.Ceiling(_cellW), Math.Ceiling(_cellH));
                if (double.IsNaN(v)) { ctx.DrawRectangle(new SolidColorBrush(ChartPalette.Line(Dark)), null, rect); continue; }
                ctx.DrawRectangle(new SolidColorBrush(Colour((v - min) / (max - min), diverging)), null, rect);
                if (ShowValues && _cellW > 26 && _cellH > 12)
                {
                    var t = Text(v.ToString(Math.Abs(v) < 10 ? "0.00" : "0", CultureInfo.InvariantCulture), Math.Min(fontSize, _cellH * 0.7), new SolidColorBrush(Contrast(Colour((v - min) / (max - min), diverging))));
                    ctx.DrawText(t, new Point(rect.Center.X - t.Width / 2, rect.Center.Y - t.Height / 2));
                }
            }
        }
        // the selected row, framed
        if (SelectedRowId >= 0 && data.RowIds is not null)
        {
            var r = data.RowIds.ToList().IndexOf(SelectedRowId);
            if (r >= 0) ctx.DrawRectangle(null, new Pen(ink, 1.5), new Rect(left, top + r * _cellH, cellsW, _cellH));
        }

        // group strips: the columns' classes above, the rows' beside the labels
        var groups = (data.ColumnGroups ?? Array.Empty<string>()).Concat(data.RowGroups ?? Array.Empty<string>()).Distinct().ToList();
        if (data.ColumnGroups is not null)
        {
            for (var c = 0; c < cols; c++)
            {
                var colour = ChartPalette.ForIndex(Dark, groups.IndexOf(data.ColumnGroups[c]));
                ctx.DrawRectangle(new SolidColorBrush(colour), null, new Rect(left + c * _cellW, top - stripH - 1, Math.Ceiling(_cellW), stripH - 2));
            }
        }

        // labels
        if (ShowRowLabels)
        {
            var every = Math.Max(1, (int)Math.Ceiling(fontSize * 1.2 / _cellH));
            for (var r = 0; r < rows; r += every)
            {
                var t = Text(data.RowLabels[r], fontSize, ink);
                t.MaxTextWidth = rowLabelW - 4;
                t.Trimming = TextTrimming.CharacterEllipsis;
                t.MaxLineCount = 1;
                var y = top + r * _cellH + _cellH * every / 2 - t.Height / 2;
                if (data.RowGroups is not null)
                {
                    ctx.DrawRectangle(new SolidColorBrush(ChartPalette.ForIndex(Dark, groups.IndexOf(data.RowGroups[r]))), null, new Rect(left + cellsW + 3, top + r * _cellH, 4, Math.Ceiling(_cellH * every)));
                }
                ctx.DrawText(t, new Point(left + cellsW + 10, y));
            }
        }
        if (ShowColumnLabels)
        {
            var every = Math.Max(1, (int)Math.Ceiling(fontSize * 1.2 / _cellW));
            for (var c = 0; c < cols; c += every)
            {
                var t = Text(data.ColumnLabels[c], fontSize, ink);
                t.MaxTextWidth = colLabelH - 4;
                t.Trimming = TextTrimming.CharacterEllipsis;
                t.MaxLineCount = 1;
                var x = left + c * _cellW + _cellW * every / 2;
                using (ctx.PushTransform(Matrix.CreateRotation(Math.PI / 2) * Matrix.CreateTranslation(x + t.Height / 2, top + cellsH + 4)))
                {
                    ctx.DrawText(t, new Point(0, -t.Height));
                }
            }
        }

        // trees
        var treePen = new Pen(new SolidColorBrush(ChartPalette.LineStrong(Dark)), 1);
        if (rowTreeW > 0) DrawTree(ctx, data.RowTree!, treePen, horizontal: true, new Rect(8, top, rowTreeW - 4, cellsH), rows);
        if (colTreeH > 0) DrawTree(ctx, data.ColumnTree!, treePen, horizontal: false, new Rect(left, titleH, cellsW, colTreeH - 4), cols);

        // colour scale
        var scaleX = w - scaleW + 8;
        var scaleTop = top;
        var scaleH = Math.Min(cellsH, 140.0);
        for (var i = 0; i < scaleH; i++)
        {
            var f = 1 - i / scaleH;
            ctx.DrawRectangle(new SolidColorBrush(Colour(f, diverging)), null, new Rect(scaleX, scaleTop + i, 10, 1.5));
        }
        ctx.DrawText(Text(max.ToString("0.##", CultureInfo.InvariantCulture), 9 * FontScale, muted), new Point(scaleX + 13, scaleTop - 2));
        ctx.DrawText(Text(min.ToString("0.##", CultureInfo.InvariantCulture), 9 * FontScale, muted), new Point(scaleX + 13, scaleTop + scaleH - 10));
        if (diverging) ctx.DrawText(Text("0", 9 * FontScale, muted), new Point(scaleX + 13, scaleTop + scaleH / 2 - 5));
        if (!string.IsNullOrEmpty(data.ValueName))
        {
            var t = Text(data.ValueName, 9 * FontScale, muted);
            t.MaxTextWidth = scaleW + 20;
            using (ctx.PushTransform(Matrix.CreateRotation(-Math.PI / 2) * Matrix.CreateTranslation(w - 4, scaleTop + scaleH)))
            {
                ctx.DrawText(t, new Point(0, -t.Height));
            }
        }

        // legend of the group colours
        if (groups.Count > 0)
        {
            var y = scaleTop + scaleH + 14;
            foreach (var g in groups.Take(12))
            {
                ctx.DrawRectangle(new SolidColorBrush(ChartPalette.ForIndex(Dark, groups.IndexOf(g))), null, new Rect(scaleX, y + 2, 8, 8));
                var t = Text(g, 8.5 * FontScale, muted);
                t.MaxTextWidth = scaleW - 4;
                t.Trimming = TextTrimming.CharacterEllipsis;
                t.MaxLineCount = 1;
                ctx.DrawText(t, new Point(scaleX + 11, y));
                y += 12 * FontScale;
            }
        }

        // tooltip
        if (_hover is { } p && _cells.Contains(p) && ChartBase.TooltipsEnabled)
        {
            var c = Math.Clamp((int)((p.X - left) / _cellW), 0, cols - 1);
            var r = Math.Clamp((int)((p.Y - top) / _cellH), 0, rows - 1);
            var lines = new[] { data.RowLabels[r], data.ColumnLabels[c], data.Values[r, c].ToString("0.###", CultureInfo.InvariantCulture) + (string.IsNullOrEmpty(data.ValueName) ? string.Empty : " " + data.ValueName) };
            var texts = lines.Select(l => Text(l, 11, ink)).ToList();
            var tw = texts.Max(t => t.Width) + 14;
            var th = texts.Sum(t => t.Height) + 8;
            var x = p.X + 14 + tw > w ? p.X - tw - 10 : p.X + 14;
            var y = p.Y - th - 6 < 0 ? p.Y + 16 : p.Y - th - 6;
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xEE, ChartPalette.Surface(Dark).R, ChartPalette.Surface(Dark).G, ChartPalette.Surface(Dark).B)), new Pen(new SolidColorBrush(ChartPalette.LineStrong(Dark)), 1), new Rect(x, y, tw, th), 5, 5);
            var cy = y + 4;
            foreach (var t in texts) { ctx.DrawText(t, new Point(x + 7, cy)); cy += t.Height; }
        }
    }

    private void DrawTree(Charts.ChartCanvas ctx, ClusterNode root, Pen pen, bool horizontal, Rect area, int leaves)
    {
        var maxHeight = MaxHeight(root);
        if (maxHeight <= 0) maxHeight = 1;
        var slot = (horizontal ? area.Height : area.Width) / leaves;
        var position = new Dictionary<ClusterNode, double>();
        var index = 0;
        foreach (var leaf in root.Leaves()) position[leaf] = (index++ + 0.5) * slot;
        double Depth(double h) => horizontal ? area.Right - area.Width * h / maxHeight : area.Bottom - area.Height * h / maxHeight;
        void Draw(ClusterNode node)
        {
            if (node.IsLeaf) return;
            Draw(node.Left!);
            Draw(node.Right!);
            var a = position[node.Left!];
            var b = position[node.Right!];
            position[node] = (a + b) / 2;
            var d = Depth(node.Height);
            var da = node.Left!.IsLeaf ? Depth(0) : Depth(node.Left.Height);
            var db = node.Right!.IsLeaf ? Depth(0) : Depth(node.Right.Height);
            if (horizontal)
            {
                ctx.DrawLine(pen, new Point(d, area.Y + a), new Point(da, area.Y + a));
                ctx.DrawLine(pen, new Point(d, area.Y + b), new Point(db, area.Y + b));
                ctx.DrawLine(pen, new Point(d, area.Y + a), new Point(d, area.Y + b));
            }
            else
            {
                ctx.DrawLine(pen, new Point(area.X + a, d), new Point(area.X + a, da));
                ctx.DrawLine(pen, new Point(area.X + b, d), new Point(area.X + b, db));
                ctx.DrawLine(pen, new Point(area.X + a, d), new Point(area.X + b, d));
            }
        }
        Draw(root);
    }

    private static double MaxHeight(ClusterNode node) => node.IsLeaf ? 0 : Math.Max(node.Height, Math.Max(MaxHeight(node.Left!), MaxHeight(node.Right!)));

    private Color Colour(double f, bool diverging)
    {
        f = Math.Clamp(f, 0, 1);
        switch (ColorScale)
        {
            case "Viridis": return Viridis(f);
            case "Greys": return Lerp(Color.Parse("#f7f7f7"), Color.Parse("#252525"), f);
            case "Green–black–red":
                return f < 0.5 ? Lerp(Color.Parse("#1a9850"), Color.Parse("#111111"), f * 2) : Lerp(Color.Parse("#111111"), Color.Parse("#d73027"), (f - 0.5) * 2);
            default:
                if (!diverging) return Lerp(Color.Parse("#f7fbff"), Color.Parse("#08306b"), f);
                return f < 0.5 ? Lerp(Color.Parse("#2166ac"), Color.Parse("#f7f7f7"), f * 2) : Lerp(Color.Parse("#f7f7f7"), Color.Parse("#b2182b"), (f - 0.5) * 2);
        }
    }

    private static Color Viridis(double f)
    {
        Color[] stops = { Color.Parse("#440154"), Color.Parse("#3b528b"), Color.Parse("#21918c"), Color.Parse("#5ec962"), Color.Parse("#fde725") };
        var x = f * (stops.Length - 1);
        var i = Math.Min(stops.Length - 2, (int)Math.Floor(x));
        return Lerp(stops[i], stops[i + 1], x - i);
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromArgb(255,
        (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

    private static Color Contrast(Color c) => 0.299 * c.R + 0.587 * c.G + 0.114 * c.B > 150 ? Colors.Black : Colors.White;

    private FormattedText Text(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal)
        => Charts.ChartText.Make(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, weight), size, brush);

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        _hover = e.GetPosition(this);
        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var data = Data;
        var p = e.GetPosition(this);
        if (data?.RowIds is null || !_cells.Contains(p) || _cellH <= 0) return;
        var r = Math.Clamp((int)((p.Y - _cells.Y) / _cellH), 0, data.RowLabels.Count - 1);
        if (r < data.RowIds.Count) SelectedRowId = data.RowIds[r];
    }
}
