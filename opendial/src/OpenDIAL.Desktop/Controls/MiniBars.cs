using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using OpenDIAL.Pipeline.Results;

namespace OpenDIAL.Desktop.Controls;

/// <summary>
/// The abundance of one feature drawn small enough to live inside a table cell: one bar per sample
/// class, scaled to the tallest of them, coloured with the same palette the charts use. It is what
/// lets a reviewer scan the ion table for a feature that is as high in the blanks as in the samples
/// without opening a panel.
/// </summary>
public sealed class MiniBars : Control
{
    public static readonly StyledProperty<IReadOnlyList<ClassHeight>?> ItemsProperty =
        AvaloniaProperty.Register<MiniBars, IReadOnlyList<ClassHeight>?>(nameof(Items));

    static MiniBars()
    {
        AffectsRender<MiniBars>(ItemsProperty);
    }

    public IReadOnlyList<ClassHeight>? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }

    private bool _dark => (Application.Current?.ActualThemeVariant ?? Avalonia.Styling.ThemeVariant.Light) == Avalonia.Styling.ThemeVariant.Dark;

    public override void Render(DrawingContext context)
    {
        var items = Items;
        if (items is null || items.Count == 0) return;
        var width = Bounds.Width;
        var height = Bounds.Height;
        if (width <= 2 || height <= 2) return;

        var max = items.Max(i => i.Mean);
        if (max <= 0) return;

        var gap = items.Count > 12 ? 0.5 : 1.5;
        var barWidth = Math.Max(1.0, (width - gap * (items.Count - 1)) / items.Count);
        var baseline = height - 1;

        for (var i = 0; i < items.Count; i++)
        {
            var value = Math.Max(0, items[i].Mean);
            var barHeight = Math.Max(1.0, value / max * (height - 3));
            var x = i * (barWidth + gap);
            var color = ChartPalette.ForIndex(_dark, i);
            context.FillRectangle(new SolidColorBrush(color), new Rect(x, baseline - barHeight, barWidth, barHeight));
        }
    }
}
