using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace MilX.Desktop.Controls;

/// <summary>One bar of a ranking: a name, its value, its group, and optionally a level per class to draw as cells beside it.</summary>
public sealed record RankItem(string Label, double Value, string Group, object? Tag = null, IReadOnlyList<double>? ClassLevels = null, string? Detail = null);

/// <summary>
/// Horizontal bars in rank order — the VIP scores, a forest's importance, an enrichment — with
/// the option of a strip of cells beside each bar showing the feature's level in every class, the
/// way MetaboAnalyst's VIP plot does. Clicking a bar reports its tag.
/// </summary>
public sealed class RankChart : Control, Charts.IChartRenderable
{
    public static readonly StyledProperty<IReadOnlyList<RankItem>?> ItemsProperty = AvaloniaProperty.Register<RankChart, IReadOnlyList<RankItem>?>(nameof(Items));
    public static readonly StyledProperty<IReadOnlyList<string>?> ClassNamesProperty = AvaloniaProperty.Register<RankChart, IReadOnlyList<string>?>(nameof(ClassNames));
    public static readonly StyledProperty<string?> XLabelProperty = AvaloniaProperty.Register<RankChart, string?>(nameof(XLabel));
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<RankChart, string?>(nameof(Title));
    public static readonly StyledProperty<double> FontScaleProperty = AvaloniaProperty.Register<RankChart, double>(nameof(FontScale), 1.0);
    public static readonly StyledProperty<double?> ThresholdProperty = AvaloniaProperty.Register<RankChart, double?>(nameof(Threshold));
    public static readonly StyledProperty<object?> SelectedTagProperty = AvaloniaProperty.Register<RankChart, object?>(nameof(SelectedTag), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<bool> ColourByGroupProperty = AvaloniaProperty.Register<RankChart, bool>(nameof(ColourByGroup), true);

    private Point? _hover;
    private double _rowH;
    private double _top;

    static RankChart()
    {
        AffectsRender<RankChart>(ItemsProperty, ClassNamesProperty, XLabelProperty, TitleProperty, FontScaleProperty, ThresholdProperty, SelectedTagProperty, ColourByGroupProperty);
    }

    public RankChart()
    {
        Charts.ChartExportFlow.AttachMenu(this, () => Title);
        ClipToBounds = true;
        MinHeight = 60;
    }

    public IReadOnlyList<RankItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    /// <summary>The names of the classes the <see cref="RankItem.ClassLevels"/> cells stand for.</summary>
    public IReadOnlyList<string>? ClassNames { get => GetValue(ClassNamesProperty); set => SetValue(ClassNamesProperty, value); }
    public string? XLabel { get => GetValue(XLabelProperty); set => SetValue(XLabelProperty, value); }
    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public double FontScale { get => GetValue(FontScaleProperty); set => SetValue(FontScaleProperty, value); }
    /// <summary>A dashed line at this value: VIP 1, or p 0.05.</summary>
    public double? Threshold { get => GetValue(ThresholdProperty); set => SetValue(ThresholdProperty, value); }
    public object? SelectedTag { get => GetValue(SelectedTagProperty); set => SetValue(SelectedTagProperty, value); }
    /// <summary>Bars take their group's colour; otherwise every bar is the accent.</summary>
    public bool ColourByGroup { get => GetValue(ColourByGroupProperty); set => SetValue(ColourByGroupProperty, value); }

    private bool Dark => Charts.ChartTheme.IsDark(this, Application.Current?.ActualThemeVariant);

    public override void Render(DrawingContext context) => RenderTo(new Charts.AvaloniaCanvas(context));

    public void RenderTo(Charts.ChartCanvas ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        ctx.DrawRectangle(new SolidColorBrush(Charts.ChartTheme.Paper(this, ChartPalette.Surface(Dark))), null, new Rect(Bounds.Size));
        var items = Items;
        var ink = new SolidColorBrush(ChartPalette.Ink(Dark));
        var muted = new SolidColorBrush(ChartPalette.Muted(Dark));
        if (items is null || items.Count == 0 || w < 60 || h < 30)
        {
            var empty = Text("No data", 12, new SolidColorBrush(ChartPalette.Faint(Dark)));
            ctx.DrawText(empty, new Point(w / 2 - empty.Width / 2, h / 2 - empty.Height / 2));
            return;
        }
        var fontSize = 9.5 * FontScale;
        var titleH = string.IsNullOrEmpty(Title) ? 4 : 22 * FontScale;
        var labelW = Math.Min(w * 0.42, items.Max(i => Text(i.Label.Length > 34 ? i.Label[..33] + "…" : i.Label, fontSize, ink).Width) + 12);
        var classes = ClassNames;
        var cellsW = classes is { Count: > 0 } && items.Any(i => i.ClassLevels is not null) ? Math.Min(120, classes.Count * 14 + 6) : 0;
        var left = labelW + 6;
        var right = w - cellsW - 10;
        var bottom = h - (string.IsNullOrEmpty(XLabel) ? 18 : 32 * FontScale);
        _top = titleH + (cellsW > 0 ? 14 * FontScale + 4 : 4);
        _rowH = (bottom - _top) / items.Count;
        var barsW = Math.Max(10, right - left);
        var min = Math.Min(0, items.Min(i => i.Value));
        var max = Math.Max(items.Max(i => i.Value), Threshold ?? double.MinValue);
        if (max <= min) max = min + 1;
        double X(double v) => left + (v - min) / (max - min) * barsW;

        if (!string.IsNullOrEmpty(Title)) ctx.DrawText(Text(Title, 12 * FontScale, ink, FontWeight.SemiBold), new Point(8, 4));

        // grid
        var gridPen = new Pen(new SolidColorBrush(ChartPalette.Line(Dark)), 1);
        var step = NiceStep(max - min, Math.Max(2, (int)(barsW / 80)));
        for (var v = Math.Ceiling(min / step) * step; v <= max + step * 1e-6; v += step)
        {
            var x = X(v);
            ctx.DrawLine(gridPen, new Point(x, _top), new Point(x, bottom));
            var t = Text(v.ToString(step >= 1 ? "0" : "0.##", CultureInfo.InvariantCulture), 9 * FontScale, muted);
            ctx.DrawText(t, new Point(x - t.Width / 2, bottom + 3));
        }
        if (Threshold is { } threshold)
        {
            var x = X(threshold);
            ctx.DrawLine(new Pen(muted, 1, dashStyle: DashStyle.Dash), new Point(x, _top), new Point(x, bottom));
        }

        var groups = items.Select(i => i.Group).Distinct().ToList();
        var barH = Math.Max(2, _rowH * 0.62);
        var every = Math.Max(1, (int)Math.Ceiling(fontSize * 1.25 / _rowH));
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var y = _top + i * _rowH + (_rowH - barH) / 2;
            var colour = ColourByGroup ? ChartPalette.ForIndex(Dark, groups.IndexOf(item.Group)) : ChartPalette.Accent(Dark);
            var selected = item.Tag is not null && Equals(item.Tag, SelectedTag);
            var x0 = X(Math.Min(0, item.Value));
            var x1 = X(Math.Max(0, item.Value));
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(selected ? (byte)255 : (byte)200, colour.R, colour.G, colour.B)), selected ? new Pen(ink, 1.2) : null, new Rect(x0, y, Math.Max(1, x1 - x0), barH), 2, 2);
            if (i % every == 0)
            {
                var label = item.Label.Length > 34 ? item.Label[..33] + "…" : item.Label;
                var t = Text(label, fontSize, ink);
                t.MaxTextWidth = labelW - 4;
                t.Trimming = TextTrimming.CharacterEllipsis;
                t.MaxLineCount = 1;
                ctx.DrawText(t, new Point(labelW - t.Width, _top + i * _rowH + _rowH / 2 - t.Height / 2));
            }
            if (cellsW > 0 && item.ClassLevels is { Count: > 0 } levels && classes is not null)
            {
                var cellW = (cellsW - 6) / classes.Count;
                var lo = levels.Min();
                var hi = levels.Max();
                for (var c = 0; c < Math.Min(levels.Count, classes.Count); c++)
                {
                    var f = hi > lo ? (levels[c] - lo) / (hi - lo) : 0.5;
                    var cell = Lerp(Color.Parse("#2166ac"), Color.Parse("#f7f7f7"), Math.Min(1, f * 2));
                    if (f > 0.5) cell = Lerp(Color.Parse("#f7f7f7"), Color.Parse("#b2182b"), (f - 0.5) * 2);
                    ctx.DrawRectangle(new SolidColorBrush(cell), null, new Rect(right + 6 + c * cellW, _top + i * _rowH + 1, Math.Max(1, cellW - 1), Math.Max(1, _rowH - 2)));
                }
            }
        }
        if (cellsW > 0 && classes is not null)
        {
            var cellW = (cellsW - 6) / classes.Count;
            for (var c = 0; c < classes.Count; c++)
            {
                var t = Text(classes[c].Length > 8 ? classes[c][..7] + "…" : classes[c], 8.5 * FontScale, muted);
                using (ctx.PushTransform(Matrix.CreateRotation(-Math.PI / 4) * Matrix.CreateTranslation(right + 6 + c * cellW + cellW / 2, _top - 2)))
                {
                    ctx.DrawText(t, new Point(0, -t.Height));
                }
            }
            var low = Text("low", 8 * FontScale, muted);
            var high = Text("high", 8 * FontScale, muted);
            ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#2166ac")), null, new Rect(right + 6, bottom + 4, 8, 8));
            ctx.DrawText(low, new Point(right + 16, bottom + 2));
            ctx.DrawRectangle(new SolidColorBrush(Color.Parse("#b2182b")), null, new Rect(right + 6 + low.Width + 22, bottom + 4, 8, 8));
            ctx.DrawText(high, new Point(right + 16 + low.Width + 22, bottom + 2));
        }
        if (!string.IsNullOrEmpty(XLabel))
        {
            var t = Text(XLabel, 11 * FontScale, muted);
            ctx.DrawText(t, new Point(left + barsW / 2 - t.Width / 2, h - t.Height - 2));
        }
        ctx.DrawLine(new Pen(new SolidColorBrush(ChartPalette.LineStrong(Dark)), 1), new Point(left, _top), new Point(left, bottom));

        if (_hover is { } p && p.Y >= _top && p.Y < bottom && ChartBase.TooltipsEnabled)
        {
            var i = Math.Clamp((int)((p.Y - _top) / _rowH), 0, items.Count - 1);
            var item = items[i];
            var lines = new List<string> { item.Label, $"{XLabel ?? "value"}: {item.Value.ToString("0.###", CultureInfo.InvariantCulture)}", item.Group };
            if (item.Detail is not null) lines.Add(item.Detail);
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

    private static double NiceStep(double range, int ticks)
    {
        if (range <= 0) return 1;
        var rough = range / Math.Max(1, ticks);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var residual = rough / magnitude;
        return (residual < 1.5 ? 1 : residual < 3 ? 2 : residual < 7 ? 5 : 10) * magnitude;
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromArgb(255,
        (byte)(a.R + (b.R - a.R) * Math.Clamp(t, 0, 1)), (byte)(a.G + (b.G - a.G) * Math.Clamp(t, 0, 1)), (byte)(a.B + (b.B - a.B) * Math.Clamp(t, 0, 1)));

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
        var items = Items;
        if (items is null || items.Count == 0 || _rowH <= 0) return;
        var p = e.GetPosition(this);
        var i = (int)((p.Y - _top) / _rowH);
        if (i >= 0 && i < items.Count && items[i].Tag is not null) SelectedTag = items[i].Tag;
    }
}
