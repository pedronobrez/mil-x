using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;

namespace OpenDIAL.Desktop.Controls;

/// <summary>Raised when the user clicks (without dragging) inside the plot; X/Y are data coordinates.</summary>
public sealed class ChartPointEventArgs : RoutedEventArgs
{
    public ChartPointEventArgs(RoutedEvent routedEvent, double x, double y) : base(routedEvent) { X = x; Y = y; }
    public double X { get; }
    public double Y { get; }
}

/// <summary>Raised when the user selects an X range (Shift+drag or in range-selection mode).</summary>
public sealed class ChartRangeEventArgs : RoutedEventArgs
{
    public ChartRangeEventArgs(RoutedEvent routedEvent, double start, double end, bool final) : base(routedEvent) { Start = start; End = end; IsFinal = final; }
    public double Start { get; }
    public double End { get; }
    /// <summary>False while the pointer is still down.</summary>
    public bool IsFinal { get; }
}

/// <summary>
/// Shared plumbing for the custom charts: plot area, nice axis ticks, theme tokens, drag-to-zoom on X,
/// wheel zoom, right-drag pan, double-click fit, Shift+drag range selection, click events and a drawn tooltip.
/// Subclasses supply the data extents and draw inside the plot rectangle.
/// </summary>
public abstract class ChartBase : Control, Charts.IChartRenderable
{
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<ChartBase, string?>(nameof(Title));
    public static readonly StyledProperty<string?> XLabelProperty = AvaloniaProperty.Register<ChartBase, string?>(nameof(XLabel));
    public static readonly StyledProperty<string?> YLabelProperty = AvaloniaProperty.Register<ChartBase, string?>(nameof(YLabel));
    public static readonly StyledProperty<bool> CompactProperty = AvaloniaProperty.Register<ChartBase, bool>(nameof(Compact));
    public static readonly StyledProperty<bool> RangeSelectionModeProperty = AvaloniaProperty.Register<ChartBase, bool>(nameof(RangeSelectionMode));
    public static readonly StyledProperty<double> SelectionStartProperty = AvaloniaProperty.Register<ChartBase, double>(nameof(SelectionStart), double.NaN);
    public static readonly StyledProperty<double> SelectionEndProperty = AvaloniaProperty.Register<ChartBase, double>(nameof(SelectionEnd), double.NaN);
    public static readonly StyledProperty<double> FixedXMinProperty = AvaloniaProperty.Register<ChartBase, double>(nameof(FixedXMin), double.NaN);
    public static readonly StyledProperty<double> FixedXMaxProperty = AvaloniaProperty.Register<ChartBase, double>(nameof(FixedXMax), double.NaN);
    public static readonly StyledProperty<double> FixedYMaxProperty = AvaloniaProperty.Register<ChartBase, double>(nameof(FixedYMax), double.NaN);
    public static readonly StyledProperty<bool> IsHighlightedProperty = AvaloniaProperty.Register<ChartBase, bool>(nameof(IsHighlighted));
    public static readonly StyledProperty<bool> ShowTooltipProperty = AvaloniaProperty.Register<ChartBase, bool>(nameof(ShowTooltip), true);
    public static readonly StyledProperty<double> PointSizeProperty = AvaloniaProperty.Register<ChartBase, double>(nameof(PointSize), 4.2);
    public static readonly StyledProperty<double> FontScaleProperty = AvaloniaProperty.Register<ChartBase, double>(nameof(FontScale), 1.0);
    public static readonly StyledProperty<bool> ShowGridProperty = AvaloniaProperty.Register<ChartBase, bool>(nameof(ShowGrid), true);
    public static readonly StyledProperty<string> PaletteProperty = AvaloniaProperty.Register<ChartBase, string>(nameof(Palette), "Tableau");
    public static readonly StyledProperty<bool> ShowPointLabelsProperty = AvaloniaProperty.Register<ChartBase, bool>(nameof(ShowPointLabels));

    public static readonly RoutedEvent<ChartPointEventArgs> PointClickedEvent = RoutedEvent.Register<ChartBase, ChartPointEventArgs>(nameof(PointClicked), RoutingStrategies.Bubble);
    public static readonly RoutedEvent<ChartRangeEventArgs> RangeSelectedEvent = RoutedEvent.Register<ChartBase, ChartRangeEventArgs>(nameof(RangeSelected), RoutingStrategies.Bubble);
    public static readonly RoutedEvent<RoutedEventArgs> ChartDoubleClickedEvent = RoutedEvent.Register<ChartBase, RoutedEventArgs>(nameof(ChartDoubleClicked), RoutingStrategies.Bubble);

    /// <summary>Accent first, then a categorical palette (Tableau 10 without the blue).</summary>
    protected static readonly Color[] Categorical =
    {
        Color.Parse("#E15759"), Color.Parse("#59A14F"), Color.Parse("#F28E2B"), Color.Parse("#B07AA1"), Color.Parse("#76B7B2"),
        Color.Parse("#EDC948"), Color.Parse("#9C755F"), Color.Parse("#FF9DA7"), Color.Parse("#4E79A7"), Color.Parse("#BAB0AC"),
    };

    /// <summary>Global switch used by the snapshot diagnostics (the real pointer would otherwise leave a tooltip in the image).</summary>
    public static bool TooltipsEnabled { get; set; } = true;

    // view state (NaN => automatic)
    protected double ViewXMin = double.NaN;
    protected double ViewXMax = double.NaN;
    private Point? _hover;
    private Point? _dragStart;
    private double _dragXMin, _dragXMax;
    private bool _dragged;
    private bool _panning;
    private bool _selecting;
    private double _selX0, _selX1;

    static ChartBase()
    {
        AffectsRender<ChartBase>(TitleProperty, XLabelProperty, YLabelProperty, CompactProperty, SelectionStartProperty, SelectionEndProperty,
            FixedXMinProperty, FixedXMaxProperty, FixedYMaxProperty, IsHighlightedProperty, RangeSelectionModeProperty,
            PointSizeProperty, FontScaleProperty, ShowGridProperty, PaletteProperty, ShowPointLabelsProperty);
        FocusableProperty.OverrideDefaultValue<ChartBase>(true);
    }

    protected ChartBase()
    {
        ClipToBounds = true;
        MinHeight = 60;
        ActualThemeVariantChanged += (_, _) => InvalidateVisual();
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? XLabel { get => GetValue(XLabelProperty); set => SetValue(XLabelProperty, value); }
    public string? YLabel { get => GetValue(YLabelProperty); set => SetValue(YLabelProperty, value); }
    /// <summary>Small panel mode: tight margins, no axis titles, title drawn inside.</summary>
    public bool Compact { get => GetValue(CompactProperty); set => SetValue(CompactProperty, value); }
    /// <summary>When true a plain left drag selects a range instead of zooming (Shift+drag always selects).</summary>
    public bool RangeSelectionMode { get => GetValue(RangeSelectionModeProperty); set => SetValue(RangeSelectionModeProperty, value); }
    public double SelectionStart { get => GetValue(SelectionStartProperty); set => SetValue(SelectionStartProperty, value); }
    public double SelectionEnd { get => GetValue(SelectionEndProperty); set => SetValue(SelectionEndProperty, value); }
    /// <summary>Initial X range (NaN = data extent). Used to link panels; zooming is still possible.</summary>
    public double FixedXMin { get => GetValue(FixedXMinProperty); set => SetValue(FixedXMinProperty, value); }
    public double FixedXMax { get => GetValue(FixedXMaxProperty); set => SetValue(FixedXMaxProperty, value); }
    /// <summary>Y maximum shared between panels (NaN = automatic).</summary>
    public double FixedYMax { get => GetValue(FixedYMaxProperty); set => SetValue(FixedYMaxProperty, value); }
    /// <summary>Draws an accent frame (the selected panel of a grid).</summary>
    public bool IsHighlighted { get => GetValue(IsHighlightedProperty); set => SetValue(IsHighlightedProperty, value); }
    public bool ShowTooltip { get => GetValue(ShowTooltipProperty); set => SetValue(ShowTooltipProperty, value); }
    /// <summary>Radius of a plotted point, for the charts that draw points.</summary>
    public double PointSize { get => GetValue(PointSizeProperty); set => SetValue(PointSizeProperty, value); }
    /// <summary>Multiplies every font size on the chart; 1.3 is a figure for a slide.</summary>
    public double FontScale { get => GetValue(FontScaleProperty); set => SetValue(FontScaleProperty, value); }
    public bool ShowGrid { get => GetValue(ShowGridProperty); set => SetValue(ShowGridProperty, value); }
    /// <summary>"Tableau", "Okabe-Ito" (colour-blind safe), "Grey" or "Accent".</summary>
    public string Palette { get => GetValue(PaletteProperty); set => SetValue(PaletteProperty, value); }
    /// <summary>Label every point with its name, for the charts that have names to show.</summary>
    public bool ShowPointLabels { get => GetValue(ShowPointLabelsProperty); set => SetValue(ShowPointLabelsProperty, value); }

    public event EventHandler<ChartPointEventArgs> PointClicked { add => AddHandler(PointClickedEvent, value); remove => RemoveHandler(PointClickedEvent, value); }
    public event EventHandler<ChartRangeEventArgs> RangeSelected { add => AddHandler(RangeSelectedEvent, value); remove => RemoveHandler(RangeSelectedEvent, value); }
    public event EventHandler<RoutedEventArgs> ChartDoubleClicked { add => AddHandler(ChartDoubleClickedEvent, value); remove => RemoveHandler(ChartDoubleClickedEvent, value); }

    // ------------------------------------------------------------------ theme tokens

    protected bool IsDark => ActualThemeVariant == ThemeVariant.Dark;
    protected Color InkColor => IsDark ? Color.Parse("#e6e8eb") : Color.Parse("#1a1d21");
    protected Color MutedColor => IsDark ? Color.Parse("#9ba3ae") : Color.Parse("#6b7280");
    protected Color FaintColor => IsDark ? Color.Parse("#6b7280") : Color.Parse("#9aa1ac");
    protected Color LineColor => IsDark ? Color.Parse("#2c3035") : Color.Parse("#e3e5ea");
    protected Color LineStrongColor => IsDark ? Color.Parse("#3c4148") : Color.Parse("#cfd3da");
    protected Color SurfaceColor => IsDark ? Color.Parse("#1e2124") : Color.Parse("#ffffff");
    protected Color AccentColor => IsDark ? Color.Parse("#6f9be0") : Color.Parse("#234b8c");
    protected Color AccentSoftColor => IsDark ? Color.Parse("#22304a") : Color.Parse("#e7edf7");
    protected Color WarningColor => IsDark ? Color.Parse("#e0a844") : Color.Parse("#a86a00");
    protected Color DangerColor => IsDark ? Color.Parse("#e07070") : Color.Parse("#b03030");

    protected IBrush TextBrush => new SolidColorBrush(InkColor);
    protected IBrush MutedBrush => new SolidColorBrush(MutedColor);
    protected IBrush GridBrush => new SolidColorBrush(LineColor);
    protected IBrush AxisBrush => new SolidColorBrush(LineStrongColor);
    protected IBrush PlotBackground => new SolidColorBrush(SurfaceColor);
    protected IBrush TooltipBackground => new SolidColorBrush(Color.FromArgb(0xEE, SurfaceColor.R, SurfaceColor.G, SurfaceColor.B));

    /// <summary>Series colour by index: accent first, then the categorical palette, or whatever palette was chosen.</summary>
    protected Color SeriesColor(int index) => Palette switch
    {
        "Okabe-Ito" => OkabeIto[index % OkabeIto.Length],
        "Grey" => Greys[index % Greys.Length],
        "Accent" => index == 0 ? AccentColor : Color.FromArgb(255, (byte)Math.Min(255, AccentColor.R + 40 * index), (byte)Math.Min(255, AccentColor.G + 30 * index), (byte)Math.Min(255, AccentColor.B + 20 * index)),
        _ => index == 0 ? AccentColor : Categorical[(index - 1) % Categorical.Length],
    };

    /// <summary>Okabe and Ito's eight colours, told apart by every kind of colour vision.</summary>
    protected static readonly Color[] OkabeIto =
    {
        Color.Parse("#0072B2"), Color.Parse("#E69F00"), Color.Parse("#009E73"), Color.Parse("#D55E00"),
        Color.Parse("#CC79A7"), Color.Parse("#56B4E9"), Color.Parse("#F0E442"), Color.Parse("#000000"),
    };

    protected static readonly Color[] Greys =
    {
        Color.Parse("#222222"), Color.Parse("#777777"), Color.Parse("#AAAAAA"), Color.Parse("#444444"), Color.Parse("#999999"), Color.Parse("#CCCCCC"),
    };

    /// <summary>A polyline the SVG export can write back out as a path.</summary>
    protected static StreamGeometry Polyline(IReadOnlyList<Point> points, bool close = false) => Charts.ChartCanvas.Polyline(points, close);

    public static Color ColorForIndex(bool dark, int index) => index == 0 ? (dark ? Color.Parse("#6f9be0") : Color.Parse("#234b8c")) : Categorical[(index - 1) % Categorical.Length];

    // ------------------------------------------------------------------ abstract surface

    protected abstract (double Min, double Max) DataXExtent();
    protected abstract (double Min, double Max) DataYExtent(double xMin, double xMax);
    protected abstract void RenderPlot(Charts.ChartCanvas ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, double xMin, double xMax, double yMin, double yMax);
    protected virtual IReadOnlyList<string>? GetTooltip(Point pos, Rect plot, Func<double, double> tx, Func<double, double> ty) => null;
    protected virtual bool PadYRange => true;
    protected virtual bool ShowXTicks => true;
    protected virtual void RenderOverlay(Charts.ChartCanvas ctx, Rect plot, Func<double, double> tx, Func<double, double> ty, double xMin, double xMax) { }

    private double MarginLeft => Compact ? 40 : 64 * Math.Max(1, FontScale);
    private double MarginTop => Compact ? 20 : (string.IsNullOrEmpty(Title) ? 12 : 26 * Math.Max(1, FontScale));
    private double MarginRight => Compact ? 8 : 14;
    private double MarginBottom => Compact ? 18 : (string.IsNullOrEmpty(XLabel) ? 24 : 38 * Math.Max(1, FontScale));

    protected Rect PlotRect => new(MarginLeft, MarginTop, Math.Max(1, Bounds.Width - MarginLeft - MarginRight), Math.Max(1, Bounds.Height - MarginTop - MarginBottom));

    public void ResetView()
    {
        ViewXMin = double.NaN;
        ViewXMax = double.NaN;
        InvalidateVisual();
    }

    public void SetView(double xMin, double xMax)
    {
        ViewXMin = xMin;
        ViewXMax = xMax;
        InvalidateVisual();
    }

    protected (double Min, double Max) CurrentXRange()
    {
        var (dataXMin, dataXMax) = DataXExtent();
        if (double.IsNaN(dataXMin)) return (double.NaN, double.NaN);
        var min = !double.IsNaN(ViewXMin) ? ViewXMin : !double.IsNaN(FixedXMin) ? FixedXMin : dataXMin;
        var max = !double.IsNaN(ViewXMax) ? ViewXMax : !double.IsNaN(FixedXMax) ? FixedXMax : dataXMax;
        if (max <= min) { min = dataXMin; max = dataXMax; }
        if (max <= min) max = min + 1;
        return (min, max);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FixedXMinProperty || change.Property == FixedXMaxProperty)
        {
            ViewXMin = double.NaN;
            ViewXMax = double.NaN;
        }
    }

    // ------------------------------------------------------------------ rendering

    public sealed override void Render(DrawingContext context) => RenderTo(new Charts.AvaloniaCanvas(context));

    /// <summary>Draws the chart on any canvas: the screen, or the SVG writer of an export.</summary>
    public void RenderTo(Charts.ChartCanvas ctx)
    {
        var plot = PlotRect;
        ctx.DrawRectangle(PlotBackground, null, new Rect(Bounds.Size));

        var (dataXMin, dataXMax) = DataXExtent();
        if (double.IsNaN(dataXMin) || double.IsNaN(dataXMax))
        {
            DrawAxesFrame(ctx, plot);
            DrawTextCentered(ctx, "No data", plot.Center, new SolidColorBrush(FaintColor), Compact ? 11 : 13);
            DrawTitle(ctx, plot);
            DrawHighlightFrame(ctx);
            return;
        }
        var (xMin, xMax) = CurrentXRange();

        var (yMin, yMax) = DataYExtent(xMin, xMax);
        if (!double.IsNaN(FixedYMax) && FixedYMax > 0 && yMin >= 0) yMax = FixedYMax;
        if (double.IsNaN(yMin) || double.IsNaN(yMax) || yMax <= yMin)
        {
            yMin = Math.Min(0, double.IsNaN(yMin) ? 0 : yMin);
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
            DrawSelection(ctx, plot, Tx);
        }
        DrawAxesFrame(ctx, plot);
        RenderOverlay(ctx, plot, Tx, Ty, xMin, xMax);
        DrawTitle(ctx, plot);
        DrawHighlightFrame(ctx);

        if (ShowTooltip && TooltipsEnabled && _hover is { } h && plot.Contains(h) && _dragStart is null && GetTooltip(h, plot, Tx, Ty) is { Count: > 0 } lines)
        {
            DrawTooltip(ctx, h, lines);
        }
    }

    private void DrawSelection(Charts.ChartCanvas ctx, Rect plot, Func<double, double> tx)
    {
        double s0, s1;
        if (_selecting) { s0 = _selX0; s1 = _selX1; }
        else if (!double.IsNaN(SelectionStart) && !double.IsNaN(SelectionEnd)) { s0 = SelectionStart; s1 = SelectionEnd; }
        else if (_dragStart is not null && _dragged && !_panning)
        {
            // rubber band zoom preview
            var x0 = Math.Min(_dragStart.Value.X, _hover?.X ?? _dragStart.Value.X);
            var x1 = Math.Max(_dragStart.Value.X, _hover?.X ?? _dragStart.Value.X);
            ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(0x30, AccentColor.R, AccentColor.G, AccentColor.B)), new Pen(new SolidColorBrush(AccentColor), 1), new Rect(x0, plot.Y, Math.Max(1, x1 - x0), plot.Height));
            return;
        }
        else return;
        if (s1 < s0) (s0, s1) = (s1, s0);
        var px0 = tx(s0);
        var px1 = tx(s1);
        var brush = new SolidColorBrush(Color.FromArgb(0x38, WarningColor.R, WarningColor.G, WarningColor.B));
        var pen = new Pen(new SolidColorBrush(WarningColor), 1, dashStyle: DashStyle.Dash);
        ctx.DrawRectangle(brush, null, new Rect(px0, plot.Y, Math.Max(1, px1 - px0), plot.Height));
        ctx.DrawLine(pen, new Point(px0, plot.Y), new Point(px0, plot.Bottom));
        ctx.DrawLine(pen, new Point(px1, plot.Y), new Point(px1, plot.Bottom));
    }

    private void DrawHighlightFrame(Charts.ChartCanvas ctx)
    {
        if (!IsHighlighted) return;
        var r = new Rect(Bounds.Size).Deflate(1);
        ctx.DrawRectangle(null, new Pen(new SolidColorBrush(AccentColor), 2), r, 4, 4);
    }

    private void DrawTitle(Charts.ChartCanvas ctx, Rect plot)
    {
        if (string.IsNullOrEmpty(Title)) return;
        if (Compact)
        {
            var ft = MakeText(Title!, 10.5, TextBrush, FontWeight.SemiBold);
            ft.MaxTextWidth = Math.Max(20, Bounds.Width - 8);
            ft.Trimming = TextTrimming.CharacterEllipsis;
            ft.MaxLineCount = 1;
            ctx.DrawText(ft, new Point(Math.Max(4, plot.Center.X - ft.Width / 2), 3));
        }
        else
        {
            var ft = Text(Title!, 12, TextBrush, FontWeight.SemiBold);
            ft.MaxTextWidth = Math.Max(20, Bounds.Width - MarginLeft - 8);
            ft.Trimming = TextTrimming.CharacterEllipsis;
            ft.MaxLineCount = 1;
            ctx.DrawText(ft, new Point(MarginLeft, 5));
        }
    }

    private void DrawAxesFrame(Charts.ChartCanvas ctx, Rect plot)
    {
        var pen = new Pen(AxisBrush, 1);
        ctx.DrawLine(pen, new Point(plot.X, plot.Bottom), new Point(plot.Right, plot.Bottom));
        ctx.DrawLine(pen, new Point(plot.X, plot.Y), new Point(plot.X, plot.Bottom));
        if (Compact) return;
        if (!string.IsNullOrEmpty(XLabel))
        {
            var ft = Text(XLabel!, 11, MutedBrush);
            ctx.DrawText(ft, new Point(plot.Center.X - ft.Width / 2, Bounds.Height - ft.Height - 2));
        }
        if (!string.IsNullOrEmpty(YLabel))
        {
            var ft = Text(YLabel!, 11, MutedBrush);
            using (ctx.PushTransform(Matrix.CreateRotation(-Math.PI / 2) * Matrix.CreateTranslation(4 + ft.Height, plot.Center.Y + ft.Width / 2)))
            {
                ctx.DrawText(ft, new Point(0, -ft.Height));
            }
        }
    }

    private void DrawGridAndTicks(Charts.ChartCanvas ctx, Rect plot, double xMin, double xMax, double yMin, double yMax, Func<double, double> tx, Func<double, double> ty)
    {
        var gridPen = new Pen(GridBrush, 1);
        var tickPen = new Pen(AxisBrush, 1);
        var fontSize = Compact ? 9 : 10;

        var xStep = NiceStep(xMax - xMin, Math.Max(2, (int)(plot.Width / (Compact ? 60 : 80))));
        var xFormat = TickFormat(xStep, xMax);
        for (var x = Math.Ceiling(xMin / xStep) * xStep; x <= xMax + xStep * 1e-6 && ShowXTicks; x += xStep)
        {
            var px = tx(x);
            if (ShowGrid) ctx.DrawLine(gridPen, new Point(px, plot.Y), new Point(px, plot.Bottom));
            ctx.DrawLine(tickPen, new Point(px, plot.Bottom), new Point(px, plot.Bottom + 3));
            var ft = Text(FormatTick(x, xFormat), fontSize, MutedBrush);
            ctx.DrawText(ft, new Point(px - ft.Width / 2, plot.Bottom + 4));
        }

        var yStep = NiceStep(yMax - yMin, Math.Max(2, (int)(plot.Height / (Compact ? 30 : 40))));
        var yFormat = TickFormat(yStep, Math.Max(Math.Abs(yMax), Math.Abs(yMin)));
        for (var y = Math.Ceiling(yMin / yStep) * yStep; y <= yMax + yStep * 1e-6; y += yStep)
        {
            var py = ty(y);
            if (ShowGrid) ctx.DrawLine(gridPen, new Point(plot.X, py), new Point(plot.Right, py));
            ctx.DrawLine(tickPen, new Point(plot.X - 3, py), new Point(plot.X, py));
            var ft = Text(FormatTick(Math.Abs(y) < yStep * 1e-9 ? 0 : y, yFormat), fontSize, MutedBrush);
            ctx.DrawText(ft, new Point(plot.X - 6 - ft.Width, py - ft.Height / 2));
        }
    }

    private void DrawTooltip(Charts.ChartCanvas ctx, Point at, IReadOnlyList<string> lines)
    {
        var texts = lines.Select(l => MakeText(l, 11, TextBrush)).ToList();
        var w = texts.Max(t => t.Width) + 14;
        var h = texts.Sum(t => t.Height) + 8;
        var x = at.X + 14;
        var y = at.Y - h - 6;
        if (x + w > Bounds.Width) x = at.X - w - 10;
        if (y < 0) y = at.Y + 16;
        var rect = new Rect(x, y, w, h);
        ctx.DrawRectangle(TooltipBackground, new Pen(AxisBrush, 1), rect, 5, 5);
        var cy = y + 4;
        foreach (var t in texts)
        {
            ctx.DrawText(t, new Point(x + 7, cy));
            cy += t.Height;
        }
    }

    /// <summary>Legend box drawn top-right of the plot with a translucent surface.</summary>
    /// <summary>
    /// The legend goes in the corner of the plot with the least underneath it: the top right by
    /// habit, but a volcano plot's named points live exactly there, and a legend over the finding
    /// is worse than a legend in an odd place. <paramref name="occupied"/> is what it must avoid.
    /// </summary>
    protected void DrawLegend(Charts.ChartCanvas ctx, Rect plot, IReadOnlyList<(string Label, Color Color)> entries, IReadOnlyList<Rect>? occupied = null)
    {
        if (entries.Count == 0) return;
        var texts = entries.Select(e => Text(e.Label.Length > 40 ? e.Label[..39] + "…" : e.Label, 10.5, TextBrush)).ToList();
        var lineH = texts.Max(t => t.Height) + 2;
        var w = texts.Max(t => t.Width) + 34;
        var h = lineH * texts.Count + 8;
        var x = plot.Right - w - 8;
        var y = plot.Y + 8;
        if (occupied is { Count: > 0 })
        {
            var corners = new[]
            {
                new Rect(plot.Right - w - 8, plot.Y + 8, w, h),
                new Rect(plot.X + 8, plot.Y + 8, w, h),
                new Rect(plot.Right - w - 8, plot.Bottom - h - 8, w, h),
                new Rect(plot.X + 8, plot.Bottom - h - 8, w, h),
            };
            var best = corners.Select(c => (Rect: c, Hits: occupied.Count(o => c.Inflate(6).Intersects(o)))).OrderBy(c => c.Hits).First();
            x = best.Rect.X;
            y = best.Rect.Y;
        }
        ctx.DrawRectangle(new SolidColorBrush(Color.FromArgb(0xD8, SurfaceColor.R, SurfaceColor.G, SurfaceColor.B)), new Pen(GridBrush, 1), new Rect(x, y, w, h), 5, 5);
        for (var i = 0; i < texts.Count; i++)
        {
            var cy = y + 4 + i * lineH;
            ctx.DrawLine(new Pen(new SolidColorBrush(entries[i].Color), 2), new Point(x + 8, cy + lineH / 2), new Point(x + 24, cy + lineH / 2));
            ctx.DrawText(texts[i], new Point(x + 30, cy));
        }
    }

    // ------------------------------------------------------------------ helpers

    protected static FormattedText MakeText(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal)
        => Charts.ChartText.Make(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, weight), size, brush);

    /// <summary>Text at the chart's font scale.</summary>
    protected FormattedText Text(string text, double size, IBrush brush, FontWeight weight = FontWeight.Normal)
        => MakeText(text, size * FontScale, brush, weight);

    protected static void DrawTextCentered(Charts.ChartCanvas ctx, string text, Point center, IBrush brush, double size)
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
        if (Math.Abs(magnitude) >= 1e5) return "E";
        if (step >= 1) return "F0";
        var decimals = Math.Clamp((int)Math.Ceiling(-Math.Log10(step)), 0, 6);
        return "F" + decimals;
    }

    private static string FormatTick(double value, string format)
        => format == "E" ? value.ToString("0.0E0", CultureInfo.InvariantCulture) : value.ToString(format, CultureInfo.InvariantCulture);

    public static string FormatIntensity(double value)
        => Math.Abs(value) >= 1e5 ? value.ToString("0.00E0", CultureInfo.InvariantCulture) : value.ToString("0.#", CultureInfo.InvariantCulture);

    protected double XAt(Point pos, Rect plot)
    {
        var (xMin, xMax) = CurrentXRange();
        return xMin + (pos.X - plot.X) / plot.Width * (xMax - xMin);
    }

    // ------------------------------------------------------------------ interaction

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        var plot = PlotRect;
        var pos = e.GetPosition(this);
        var (dataMin, dataMax) = DataXExtent();
        if (double.IsNaN(dataMin) || !plot.Contains(pos)) return;
        var (xMin, xMax) = CurrentXRange();
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
        var props = e.GetCurrentPoint(this).Properties;
        if (e.ClickCount == 2 && props.IsLeftButtonPressed)
        {
            ResetView();
            RaiseEvent(new RoutedEventArgs(ChartDoubleClickedEvent));
            e.Handled = true;
            return;
        }
        var (dataMin, dataMax) = DataXExtent();
        if (double.IsNaN(dataMin)) return;
        var pos = e.GetPosition(this);
        if (!PlotRect.Contains(pos)) return;
        if (props.IsLeftButtonPressed || props.IsRightButtonPressed || props.IsMiddleButtonPressed)
        {
            _dragStart = pos;
            var (xMin, xMax) = CurrentXRange();
            _dragXMin = xMin;
            _dragXMax = xMax;
            _dragged = false;
            _panning = !props.IsLeftButtonPressed;
            _selecting = props.IsLeftButtonPressed && (RangeSelectionMode || e.KeyModifiers.HasFlag(KeyModifiers.Shift));
            if (_selecting)
            {
                _selX0 = _selX1 = XAt(pos, PlotRect);
            }
            e.Pointer.Capture(this);
            e.Handled = true;
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
            if (Math.Abs(dx) > 3) _dragged = true;
            if (_selecting)
            {
                _selX1 = XAt(pos, plot);
                RaiseEvent(new ChartRangeEventArgs(RangeSelectedEvent, Math.Min(_selX0, _selX1), Math.Max(_selX0, _selX1), false));
            }
            else if (_panning)
            {
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
        }
        _hover = pos;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (_dragStart is { } start)
        {
            var plot = PlotRect;
            var pos = e.GetPosition(this);
            if (_selecting)
            {
                _selX1 = XAt(pos, plot);
                var s0 = Math.Min(_selX0, _selX1);
                var s1 = Math.Max(_selX0, _selX1);
                if (_dragged)
                {
                    SelectionStart = s0;
                    SelectionEnd = s1;
                    RaiseEvent(new ChartRangeEventArgs(RangeSelectedEvent, s0, s1, true));
                }
            }
            else if (!_panning && _dragged)
            {
                // rubber-band zoom on X
                var x0 = XAt(new Point(Math.Min(start.X, pos.X), 0), plot);
                var x1 = XAt(new Point(Math.Max(start.X, pos.X), 0), plot);
                var (dataMin, dataMax) = DataXExtent();
                x0 = Math.Max(dataMin, x0);
                x1 = Math.Min(dataMax, x1);
                if (x1 - x0 > (dataMax - dataMin) * 1e-4)
                {
                    ViewXMin = x0;
                    ViewXMax = x1;
                }
            }
            else if (!_dragged && !_panning)
            {
                var (xMin, xMax) = CurrentXRange();
                var (yMin, yMax) = DataYExtent(xMin, xMax);
                var x = XAt(pos, plot);
                var y = yMin + (plot.Bottom - pos.Y) / plot.Height * (yMax - yMin);
                RaiseEvent(new ChartPointEventArgs(PointClickedEvent, x, y));
            }
            _dragStart = null;
            _selecting = false;
            _panning = false;
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
