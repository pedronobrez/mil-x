using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace OpenDIAL.Desktop.Charts;

/// <summary>
/// What a chart draws with when the figure is not the screen.
///
/// A chart takes its ink, its rules and its paper from the theme of the window it sits in, which is
/// right while it is on screen and wrong the moment it leaves: a figure for a paper or a slide is
/// wanted light however the application is set, and the reviewer who works at night should not have
/// to switch the whole application to get one. These two attached properties override the variant
/// and the paper for one control, and every chart reads them instead of the window's theme. An
/// export sets them for as long as it takes to draw, and puts them back.
/// </summary>
public sealed class ChartTheme : AvaloniaObject
{
    /// <summary>The variant this chart draws in; null follows the window.</summary>
    public static readonly AttachedProperty<ThemeVariant?> VariantProperty =
        AvaloniaProperty.RegisterAttached<ChartTheme, Control, ThemeVariant?>("Variant");

    /// <summary>
    /// The colour behind the chart; null is the variant's own surface. Fully transparent means a
    /// picture with nothing behind it, which is what a figure dropped onto a coloured slide wants.
    /// </summary>
    public static readonly AttachedProperty<Color?> PaperProperty =
        AvaloniaProperty.RegisterAttached<ChartTheme, Control, Color?>("Paper");

    static ChartTheme()
    {
        VariantProperty.Changed.AddClassHandler<Control>((c, _) => c.InvalidateVisual());
        PaperProperty.Changed.AddClassHandler<Control>((c, _) => c.InvalidateVisual());
    }

    public static ThemeVariant? GetVariant(Control chart) => chart.GetValue(VariantProperty);
    public static void SetVariant(Control chart, ThemeVariant? variant) => chart.SetValue(VariantProperty, variant);
    public static Color? GetPaper(Control chart) => chart.GetValue(PaperProperty);
    public static void SetPaper(Control chart, Color? paper) => chart.SetValue(PaperProperty, paper);

    /// <summary>Whether this chart draws dark, asking the override first and the window second.</summary>
    public static bool IsDark(Control chart, ThemeVariant? windowVariant) =>
        (GetVariant(chart) ?? windowVariant ?? ThemeVariant.Light) == ThemeVariant.Dark;

    /// <summary>The paper for this chart: the override, or the surface it would have had.</summary>
    public static Color Paper(Control chart, Color surface) => GetPaper(chart) ?? surface;

    /// <summary>
    /// Lays the paper down, for the charts that draw straight onto whatever panel is behind them.
    /// On screen there is a panel; in a picture there is nothing, so an export that asked for paper
    /// has to be given it here.
    /// </summary>
    public static void PaintPaper(Control chart, ChartCanvas canvas)
    {
        if (GetPaper(chart) is not { } paper) return;
        canvas.DrawRectangle(new SolidColorBrush(paper), null, new Rect(chart.Bounds.Size));
    }
}
