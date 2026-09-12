using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using OpenDIAL.Desktop.Charts;

namespace OpenDIAL.Desktop.Views;

/// <summary>
/// Chooses how a chart leaves the application, and shows it drawn that way before it does.
///
/// Closes with the <see cref="ChartExportOptions"/> chosen, or null when it is cancelled. The
/// choice is kept for the next figure, and the application writes it into the settings through
/// <see cref="Remember"/>, so a reviewer who exports light figures from a dark window says so once.
/// </summary>
public partial class ChartExportWindow : Window
{
    private static readonly double[] Scales = { 2, 3, 4, 6 };

    private Control? _chart;
    private bool _loading;
    private IDisposable? _preview;

    /// <summary>What the next export dialog opens with.</summary>
    public static ChartExportOptions Defaults { get; set; } = new();

    /// <summary>Called with the options the person settled on, for whoever keeps the settings.</summary>
    public static Action<ChartExportOptions>? Remember { get; set; }

    public ChartExportWindow()
    {
        InitializeComponent();
        FormatBox.SelectionChanged += (_, _) => Refresh();
        ScaleBox.SelectionChanged += (_, _) => Refresh();
        ThemeBox.SelectionChanged += (_, _) => Refresh();
        BackgroundBox.SelectionChanged += (_, _) => Refresh();
        FontScaleSlider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) Refresh(); };
    }

    /// <summary>The chart the figure is made of, and the name of the panel it came from.</summary>
    public void Prepare(Control chart, string? heading, ChartExportOptions? start = null)
    {
        _chart = chart;
        PreviewLabel.Text = string.IsNullOrWhiteSpace(heading) ? "PREVIEW" : "PREVIEW · " + heading.ToUpperInvariant();
        var options = start ?? Defaults;
        _loading = true;
        FormatBox.SelectedIndex = string.Equals(options.Format, "png", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        ScaleBox.SelectedIndex = Math.Max(0, Array.FindIndex(Scales, s => Math.Abs(s - options.Scale) < 0.01));
        ThemeBox.SelectedIndex = Math.Max(0, Array.IndexOf(ChartExportOptions.Themes, options.Theme));
        BackgroundBox.SelectedIndex = Math.Max(0, Array.IndexOf(ChartExportOptions.Backgrounds, options.Background));
        FontScaleSlider.Value = options.FontScale;
        _loading = false;
        Refresh();
    }

    /// <summary>The options the controls are showing.</summary>
    public ChartExportOptions Options => new()
    {
        Format = FormatBox.SelectedIndex == 1 ? "png" : "svg",
        Scale = Scales[Math.Clamp(ScaleBox.SelectedIndex, 0, Scales.Length - 1)],
        Theme = ChartExportOptions.Themes[Math.Clamp(ThemeBox.SelectedIndex, 0, ChartExportOptions.Themes.Length - 1)],
        Background = ChartExportOptions.Backgrounds[Math.Clamp(BackgroundBox.SelectedIndex, 0, ChartExportOptions.Backgrounds.Length - 1)],
        FontScale = Math.Round(FontScaleSlider.Value, 2),
    };

    private void Refresh()
    {
        if (_loading || _chart is null) return;
        var options = Options;
        var png = string.Equals(options.Format, "png", StringComparison.OrdinalIgnoreCase);
        ScaleBox.IsEnabled = png;
        var width = _chart.Bounds.Width;
        var height = _chart.Bounds.Height;
        SizeLine.Text = png
            ? $"{Math.Ceiling(width * options.Scale):0} × {Math.Ceiling(height * options.Scale):0} pixels"
            : $"{width:0} × {height:0} points, and any size after that";
        try
        {
            // the preview is the export: the same chart, drawn with the same settings
            var drawn = ChartExport.Render(_chart, options with { Scale = 1 },
                chart => ChartExport.RenderBitmap(chart, 1));
            Preview.Source = drawn;
            _preview?.Dispose();
            _preview = drawn;
        }
        catch (Exception)
        {
            Preview.Source = null;   // a chart with nothing in it is not worth a dialog that falls over
        }
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        var options = Options;
        Defaults = options;
        Remember?.Invoke(options);
        Close(options);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        Preview.Source = null;
        _preview?.Dispose();
        _preview = null;
    }
}
