using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace OpenDIAL.Desktop.Controls;

public sealed record BarItem(string Label, double Value, string Group);

/// <summary>Vertical bar chart (one bar per sample) coloured by group/class, with a legend and hover tooltip.</summary>
public sealed class BarChart : ChartBase
{
    public static readonly StyledProperty<IReadOnlyList<BarItem>?> ItemsProperty = AvaloniaProperty.Register<BarChart, IReadOnlyList<BarItem>?>(nameof(Items));

    static BarChart()
    {
        AffectsRender<BarChart>(ItemsProperty);
    }

    public BarChart()
    {
        YLabel = "Height";
    }

    public IReadOnlyList<BarItem>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    protected override bool ShowXTicks => false;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsProperty)
        {
            ViewXMin = double.NaN;
            ViewXMax = double.NaN;
        }
    }

    protected override (double Min, double Max) DataXExtent()
    {
        var items = Items;
        if (items is null || items.Count == 0) return (double.NaN, double.NaN);
        return (-0.5, items.Count - 0.5);
    }

    protected override (double Min, double Max) DataYExtent(double xMin, double xMax)
    {
        var items = Items;
        if (items is null || items.Count == 0) return (0, 1);
        var max = items.Max(i => i.Value);
        return (0, max <= 0 ? 1 : max);
    }

    private Dictionary<string, Color> GroupColors()
    {
        var map = new Dictionary<string, Color>(StringComparer.Ordinal);
        foreach (var g in (Items ?? Array.Empty<BarItem>()).Select(i => i.Group).Distinct())
        {
            map[g] = Palette[map.Count % Palette.Length];
        }
        return map;
    }

    protected override void RenderPlot(DrawingContext ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, double xMin, double xMax, double yMin, double yMax)
    {
        var items = Items!;
        var colors = GroupColors();
        var slot = plot.Width / Math.Max(1, xMax - xMin);
        var barWidth = Math.Max(2, slot * 0.7);
        var zero = ty(0);

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var cx = tx(i);
            if (cx < plot.X - slot || cx > plot.Right + slot) continue;
            var top = ty(Math.Max(0, item.Value));
            var color = colors[item.Group];
            ctx.DrawRectangle(new SolidColorBrush(color), null, new Rect(cx - barWidth / 2, top, barWidth, Math.Max(0, zero - top)), 2, 2);
        }

        // legend
        var lx = plot.Right - 8;
        foreach (var (group, color) in colors.Reverse())
        {
            var ft = MakeText(group, 10, TextBrush);
            lx -= ft.Width;
            ctx.DrawText(ft, new Point(lx, plot.Y + 4));
            lx -= 14;
            ctx.DrawRectangle(new SolidColorBrush(color), null, new Rect(lx, plot.Y + 7, 10, 10), 2, 2);
            lx -= 12;
        }
    }

    protected override void RenderOverlay(DrawingContext ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, double xMin, double xMax)
    {
        var items = Items;
        if (items is null || items.Count == 0) return;
        var slot = plot.Width / Math.Max(1, xMax - xMin);
        var labelEvery = Math.Max(1, (int)Math.Ceiling(64 / slot));
        for (var i = 0; i < items.Count; i += labelEvery)
        {
            var cx = tx(i);
            if (cx < plot.X || cx > plot.Right) continue;
            var text = items[i].Label.Length > 14 ? items[i].Label[..13] + "…" : items[i].Label;
            var ft = MakeText(text, 9.5, MutedBrush);
            ctx.DrawText(ft, new Point(cx - ft.Width / 2, plot.Bottom + 6));
        }
    }

    protected override IReadOnlyList<string>? GetTooltip(Point pos, Rect plot, Func<double, double> tx, Func<double, double> ty)
    {
        var items = Items;
        if (items is null || items.Count == 0) return null;
        var (min, max) = DataXExtent();
        var xMin = double.IsNaN(ViewXMin) ? min : ViewXMin;
        var xMax = double.IsNaN(ViewXMax) ? max : ViewXMax;
        var x = xMin + (pos.X - plot.X) / plot.Width * (xMax - xMin);
        var i = (int)Math.Round(x);
        if (i < 0 || i >= items.Count) return null;
        var item = items[i];
        return new[] { item.Label, $"Height {FormatIntensity(item.Value)}", $"Class {item.Group}" };
    }
}
