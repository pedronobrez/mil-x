using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;

namespace OpenDIAL.Desktop.Controls;

/// <summary>
/// Shared plumbing for the custom charts: plot area, nice axis ticks, theme-aware colours,
/// mouse-wheel zoom and drag-pan on the X axis, double-click reset and a drawn tooltip.
/// Subclasses supply the data extents and draw inside the plot rectangle.
/// </summary>
public abstract class ChartBase : Control
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<ChartBase, string?>(nameof(Title));
    public static readonly StyledProperty<string?> XLabelProperty = AvaloniaProperty.Register<ChartBase, string?>(nameof(XLabel));
    public static readonly StyledProperty<string?> YLabelProperty = AvaloniaProperty.Register<ChartBase, string?>(nameof(YLabel));

    protected static readonly Color[] Palette =
    {
        Color.Parse("#4E79A7"), Color.Parse("#F28E2B"), Color.Parse("#E15759"), Color.Parse("#76B7B2"), Color.Parse("#59A14F"),
        Color.Parse("#EDC948"), Color.Parse("#B07AA1"), Color.Parse("#FF9DA7"), Color.Parse("#9C755F"), Color.Parse("#BAB0AC"),
    };

    private const double MarginLeft = 62, MarginTop = 24, MarginRight = 14, MarginBottom = 36;

    // view state (NaN => automatic)
    protected double ViewXMin = double.NaN;
    protected double ViewXMax = double.NaN;
    private Point? _hover;
    private Point? _dragStart;
    private double _dragXMin, _dragXMax;
    private bool _dragged;

    static ChartBase()
    {
        AffectsRender<ChartBase>(TitleProperty, XLabelProperty, YLabelProperty);
        FocusableProperty.OverrideDefaultValue<ChartBase>(true);
    }

    protected ChartBase()
    {
        ClipToBounds = true;
        MinHeight = 120;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? XLabel { get => GetValue(XLabelProperty); set => SetValue(XLabelProperty, value); }
    public string? YLabel { get => GetValue(YLabelProperty); set => SetValue(YLabelProperty, value); }

    // ------------------------------------------------------------------ theme

    protected bool IsDark => ActualThemeVariant == ThemeVariant.Dark;
    protected IBrush TextBrush => IsDark ? new SolidColorBrush(Color.Parse("#E6E6E6")) : new SolidColorBrush(Color.Parse("#222222"));
    protected IBrush MutedBrush => IsDark ? new SolidColorBrush(Color.Parse("#9A9A9A")) : new SolidColorBrush(Color.Parse("#666666"));
    protected IBrush GridBrush => IsDark ? new SolidColorBrush(Color.Parse("#2E2E2E")) : new SolidColorBrush(Color.Parse("#E4E4E4"));
    protected IBrush AxisBrush => IsDark ? new SolidColorBrush(Color.Parse("#7A7A7A")) : new SolidColorBrush(Color.Parse("#8A8A8A"));
    protected IBrush PlotBackground => IsDark ? new SolidColorBrush(Color.Parse("#1B1B1B")) : new SolidColorBrush(Colors.White);
    protected IBrush TooltipBackground => IsDark ? new SolidColorBrush(Color.Parse("#EE2A2A2A")) : new SolidColorBrush(Color.Parse("#F2FFFFFF"));
    protected Color AccentColor => IsDark ? Color.Parse("#6FA8DC") : Color.Parse("#2F5F9E");

    // ------------------------------------------------------------------ abstract surface

    /// <summary>Full X extent of the data (or (NaN,NaN) when empty).</summary>
    protected abstract (double Min, double Max) DataXExtent();

    /// <summary>Y extent for the visible X range.</summary>
    protected abstract (double Min, double Max) DataYExtent(double xMin, double xMax);

    protected abstract void RenderPlot(DrawingContext ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, double xMin, double xMax, double yMin, double yMax);

    /// <summary>Tooltip lines for the hovered position, or null.</summary>
    protected virtual IReadOnlyList<string>? GetTooltip(Point pos, Rect plot, Func<double, double> tx, Func<double, double> ty) => null;

    protected virtual bool PadYRange => true;

    /// <summary>False for categorical X axes (the subclass draws its own labels).</summary>
    protected virtual bool ShowXTicks => true;

    /// <summary>Drawn after the plot, without clipping (axis labels for categorical charts, legends...).</summary>
    protected virtual void RenderOverlay(DrawingContext ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, double xMin, double xMax) { }

    protected Rect PlotRect => new(MarginLeft, MarginTop, Math.Max(1, Bounds.Width - MarginLeft - MarginRight), Math.Max(1, Bounds.Height - MarginTop - MarginBottom));

    public void ResetView()
    {
        ViewXMin = double.NaN;
        ViewXMax = double.NaN;
        InvalidateVisual();
    }

    // ------------------------------------------------------------------ rendering

    public sealed override void Render(DrawingContext ctx)
    {
        var plot = PlotRect;
        ctx.DrawRectangle(PlotBackground, null, new Rect(Bounds.Size));

        var (dataXMin, dataXMax) = DataXExtent();
        if (double.IsNaN(dataXMin) || double.IsNaN(dataXMax))
        {
            DrawAxesFrame(ctx, plot);
            DrawTextCentered(ctx, "No data", plot.Center, MutedBrush, 13);
            DrawTitle(ctx);
            return;
        }
        if (dataXMax <= dataXMin)
        {
            dataXMax = dataXMin + 1;
        }
        var xMin = double.IsNaN(ViewXMin) ? dataXMin : ViewXMin;
        var xMax = double.IsNaN(ViewXMax) ? dataXMax : ViewXMax;
        if (xMax <= xMin) { xMin = dataXMin; xMax = dataXMax; }

        var (yMin, yMax) = DataYExtent(xMin, xMax);
        if (double.IsNaN(yMin) || double.IsNaN(yMax) || yMax <= yMin)
        {
            yMin = Math.Min(0, yMin is double.NaN ? 0 : yMin);
            yMax = yMin + 1;
        }
        if (PadYRange)
        {
            var pad = (yMax - yMin) * 0.06;
            yMax += pad;
            if (yMin < 0) yMin -= pad;
        }

        double Tx(double x) => plot.X + (x - xMin) / (xMax - xMin) * plot.Width;
        double Ty(double y) => plot.Bottom - (y - yMin) / (yMax - yMin) * plot.Height;

        DrawGridAndTicks(ctx, plot, xMin, xMax, yMin, yMax, Tx, Ty);
        using (ctx.PushClip(plot))
        {
            RenderPlot(ctx, plot, Tx, Ty, xMin, xMax, yMin, yMax);
        }
        DrawAxesFrame(ctx, plot);
        RenderOverlay(ctx, plot, Tx, Ty, xMin, xMax);
        DrawTitle(ctx);

        if (_hover is { } h && plot.Contains(h) && GetTooltip(h, plot, Tx, Ty) is { Count: > 0 } lines)
        {
            DrawTooltip(ctx, h, lines);
        }
    }

    private void DrawTitle(DrawingContext ctx)
    {
        if (!string.IsNullOrEmpty(Title))
        {
            var ft = MakeText(Title!, 12, TextBrush, FontWeight.SemiBold);
            ctx.DrawText(ft, new Point(MarginLeft, 4));
        }
    }

    private void DrawAxesFrame(DrawingContext ctx, Rect plot)
    {
        var pen = new Pen(AxisBrush, 1);
        ctx.DrawLine(pen, new Point(plot.X, plot.Bottom), new Point(plot.Right, plot.Bottom));
        ctx.DrawLine(pen, new Point(plot.X, plot.Y), new Point(plot.X, plot.Bottom));
        if (!string.IsNullOrEmpty(XLabel))
        {
            var ft = MakeText(XLabel!, 11, MutedBrush);
            ctx.DrawText(ft, new Point(plot.Center.X - ft.Width / 2, Bounds.Height - ft.Height - 2));
        }
        if (!string.IsNullOrEmpty(YLabel))
        {
            var ft = MakeText(YLabel!, 11, MutedBrush);
            using (ctx.PushTransform(Matrix.CreateRotation(-Math.PI / 2) * Matrix.CreateTranslation(4 + ft.Height, plot.Center.Y + ft.Width / 2)))
            {
                ctx.DrawText(ft, new Point(0, -ft.Height));
            }
        }
    }

    private void DrawGridAndTicks(DrawingContext ctx, Rect plot, double xMin, double xMax, double yMin, double yMax, Func<double, double> tx, Func<double, double> ty)
    {
        var gridPen = new Pen(GridBrush, 1);
        var tickPen = new Pen(AxisBrush, 1);

        var xStep = NiceStep(xMax - xMin, Math.Max(2, (int)(plot.Width / 80)));
        var xFormat = TickFormat(xStep, xMax);
        for (var x = Math.Ceiling(xMin / xStep) * xStep; x <= xMax + xStep * 1e-6 && ShowXTicks; x += xStep)
        {
            var px = tx(x);
            ctx.DrawLine(gridPen, new Point(px, plot.Y), new Point(px, plot.Bottom));
            ctx.DrawLine(tickPen, new Point(px, plot.Bottom), new Point(px, plot.Bottom + 4));
            var ft = MakeText(FormatTick(x, xFormat), 10, MutedBrush);
            ctx.DrawText(ft, new Point(px - ft.Width / 2, plot.Bottom + 6));
        }

        var yStep = NiceStep(yMax - yMin, Math.Max(2, (int)(plot.Height / 40)));
        var yFormat = TickFormat(yStep, Math.Max(Math.Abs(yMax), Math.Abs(yMin)));
        for (var y = Math.Ceiling(yMin / yStep) * yStep; y <= yMax + yStep * 1e-6; y += yStep)
        {
            var py = ty(y);
            ctx.DrawLine(gridPen, new Point(plot.X, py), new Point(plot.Right, py));
            ctx.DrawLine(tickPen, new Point(plot.X - 4, py), new Point(plot.X, py));
            var ft = MakeText(FormatTick(Math.Abs(y) < yStep * 1e-9 ? 0 : y, yFormat), 10, MutedBrush);
            ctx.DrawText(ft, new Point(plot.X - 7 - ft.Width, py - ft.Height / 2));
        }
    }

    private void DrawTooltip(DrawingContext ctx, Point at, IReadOnlyList<string> lines)
    {
        var texts = lines.Select(l => MakeText(l, 11, TextBrush)).ToList();
        var w = texts.Max(t => t.Width) + 12;
        var h = texts.Sum(t => t.Height) + 8;
        var x = at.X + 14;
        var y = at.Y - h - 6;
        if (x + w > Bounds.Width) x = at.X - w - 10;
        if (y < 0) y = at.Y + 16;
        var rect = new Rect(x, y, w, h);
        ctx.DrawRectangle(TooltipBackground, new Pen(AxisBrush, 1), rect, 4, 4);
        var cy = y + 4;
        foreach (var t in texts)
        {
            ctx.DrawText(t, new Point(x + 6, cy));
            cy += t.Height;
        }
    }

    // ------------------------------------------------------------------ helpers

    protected static FormattedText MakeText(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal)
        => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, weight), size, brush);

    protected static void DrawTextCentered(DrawingContext ctx, string text, Point center, IBrush brush, double size)
    {
        var ft = MakeText(text, size, brush);
        ctx.DrawText(ft, new Point(center.X - ft.Width / 2, center.Y - ft.Height / 2));
    }

    protected static double NiceStep(double range, int targetTicks)
    {
        if (range <= 0 || double.IsInfinity(range) || double.IsNaN(range)) return 1;
        var rough = range / Math.Max(1, targetTicks);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var residual = rough / magnitude;
        var nice = residual < 1.5 ? 1 : residual < 3 ? 2 : residual < 7 ? 5 : 10;
        return nice * magnitude;
    }

    private static string TickFormat(double step, double magnitude)
    {
        if (Math.Abs(magnitude) >= 1e6) return "E";
        if (step >= 1) return "F0";
        var decimals = Math.Clamp((int)Math.Ceiling(-Math.Log10(step)), 0, 6);
        return "F" + decimals;
    }

    private static string FormatTick(double value, string format)
        => format == "E" ? value.ToString("0.0E0", CultureInfo.InvariantCulture) : value.ToString(format, CultureInfo.InvariantCulture);

    protected static string FormatIntensity(double value)
        => Math.Abs(value) >= 1e5 ? value.ToString("0.00E0", CultureInfo.InvariantCulture) : value.ToString("0.#", CultureInfo.InvariantCulture);

    // ------------------------------------------------------------------ interaction

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var plot = PlotRect;
        var pos = e.GetPosition(this);
        var (dataMin, dataMax) = DataXExtent();
        if (double.IsNaN(dataMin) || !plot.Contains(pos)) return;
        var xMin = double.IsNaN(ViewXMin) ? dataMin : ViewXMin;
        var xMax = double.IsNaN(ViewXMax) ? dataMax : ViewXMax;
        var factor = e.Delta.Y > 0 ? 0.8 : 1.25;
        var cursorX = xMin + (pos.X - plot.X) / plot.Width * (xMax - xMin);
        var newMin = cursorX - (cursorX - xMin) * factor;
        var newMax = cursorX + (xMax - cursorX) * factor;
        var minRange = (dataMax - dataMin) * 1e-4;
        if (newMax - newMin < minRange) return;
        ViewXMin = Math.Max(dataMin, newMin);
        ViewXMax = Math.Min(dataMax, newMax);
        if (ViewXMin <= dataMin && ViewXMax >= dataMax) { ViewXMin = double.NaN; ViewXMax = double.NaN; }
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        if (e.ClickCount == 2)
        {
            ResetView();
            return;
        }
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed)
        {
            var (dataMin, dataMax) = DataXExtent();
            if (double.IsNaN(dataMin)) return;
            _dragStart = e.GetPosition(this);
            _dragXMin = double.IsNaN(ViewXMin) ? dataMin : ViewXMin;
            _dragXMax = double.IsNaN(ViewXMax) ? dataMax : ViewXMax;
            _dragged = false;
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pos = e.GetPosition(this);
        if (_dragStart is { } start)
        {
            var plot = PlotRect;
            var dx = pos.X - start.X;
            if (Math.Abs(dx) > 2) _dragged = true;
            var (dataMin, dataMax) = DataXExtent();
            var range = _dragXMax - _dragXMin;
            var shift = -dx / plot.Width * range;
            var newMin = _dragXMin + shift;
            var newMax = _dragXMax + shift;
            if (newMin < dataMin) { newMin = dataMin; newMax = dataMin + range; }
            if (newMax > dataMax) { newMax = dataMax; newMin = dataMax - range; }
            ViewXMin = newMin;
            ViewXMax = newMax;
            if (ViewXMin <= dataMin && ViewXMax >= dataMax) { ViewXMin = double.NaN; ViewXMax = double.NaN; }
        }
        _hover = pos;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragStart is not null)
        {
            _dragStart = null;
            e.Pointer.Capture(null);
            InvalidateVisual();
        }
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        _hover = null;
        InvalidateVisual();
    }

    protected bool WasDragged => _dragged;
}
