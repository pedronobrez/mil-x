using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using OpenDIAL.Desktop.Charts;

namespace OpenDIAL.Desktop.Controls;

/// <summary>
/// Wraps a chart with an options flyout and an export menu. Set <see cref="Content"/> to the chart
/// (or to a panel holding one); the frame finds the first chart inside and works on it. The
/// heading is the eyebrow label the panels of the application use, and doubles as the file name
/// the export suggests.
/// </summary>
public partial class ChartFrame : UserControl
{
    public static readonly StyledProperty<string?> HeadingProperty = AvaloniaProperty.Register<ChartFrame, string?>(nameof(HeadingText));
    public static readonly StyledProperty<object?> ChartContentProperty = AvaloniaProperty.Register<ChartFrame, object?>(nameof(ChartContent));

    private bool _syncing;

    public ChartFrame()
    {
        InitializeComponent();
        Heading.Text = HeadingText;
        Host.Content = ChartContent;
        OptionsButton.Click += (_, _) => LoadOptions();
        TitleBox.TextChanged += (_, _) => Apply(c => c.Title = TitleBox.Text, h => h.Title = TitleBox.Text, r => r.Title = TitleBox.Text, g => g.Title = TitleBox.Text);
        XLabelBox.TextChanged += (_, _) => Apply(c => c.XLabel = XLabelBox.Text, null, r => r.XLabel = XLabelBox.Text);
        YLabelBox.TextChanged += (_, _) => Apply(c => c.YLabel = YLabelBox.Text, null, null);
        PointSizeSlider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) Apply(c => c.PointSize = PointSizeSlider.Value, null, null); };
        FontScaleSlider.PropertyChanged += (_, e) => { if (e.Property == RangeBase.ValueProperty) Apply(c => c.FontScale = FontScaleSlider.Value, h => h.FontScale = FontScaleSlider.Value, r => r.FontScale = FontScaleSlider.Value, g => g.FontScale = FontScaleSlider.Value); };
        PaletteBox.SelectionChanged += (_, _) => Apply(c => c.Palette = (PaletteBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Tableau", null, null);
        ColorScaleBox.SelectionChanged += (_, _) => Apply(null, h => h.ColorScale = (ColorScaleBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Blue–white–red", null);
        GridBox.IsCheckedChanged += (_, _) => Apply(c => c.ShowGrid = GridBox.IsChecked == true, null, null);
        LegendBox.IsCheckedChanged += (_, _) => Apply(c => { if (c is ScatterChart s) s.ShowLegend = LegendBox.IsChecked == true; if (c is LineChart l) l.ShowLegend = LegendBox.IsChecked == true; }, null, null);
        LabelsBox.IsCheckedChanged += (_, _) => Apply(c => c.ShowPointLabels = LabelsBox.IsChecked == true, h => h.ShowRowLabels = LabelsBox.IsChecked == true, null);
        EllipsesBox.IsCheckedChanged += (_, _) => Apply(c => { if (c is ScatterChart s) s.ShowEllipses = EllipsesBox.IsChecked == true; }, null, null);
        TreesBox.IsCheckedChanged += (_, _) => Apply(null, h => h.ShowTrees = TreesBox.IsChecked == true, null);
        ValuesBox.IsCheckedChanged += (_, _) => Apply(null, h => h.ShowValues = ValuesBox.IsChecked == true, null);
    }

    /// <summary>The eyebrow above the chart, and the name an export suggests.</summary>
    public string? HeadingText
    {
        get => GetValue(HeadingProperty);
        set => SetValue(HeadingProperty, value);
    }

    /// <summary>The chart, or a panel holding one.</summary>
    [Avalonia.Metadata.Content]
    public object? ChartContent
    {
        get => GetValue(ChartContentProperty);
        set => SetValue(ChartContentProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == HeadingProperty && Heading is not null) Heading.Text = HeadingText;
        if (change.Property == ChartContentProperty && Host is not null) Host.Content = ChartContent;
    }

    /// <summary>The chart inside the frame: the first of the chart controls found in the content.</summary>
    public Control? Chart =>
        Host.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c is ChartBase or HeatmapChart or RankChart or Dendrogram or NetworkGraph or PathwayGraph);

    private void LoadOptions()
    {
        _syncing = true;
        try
        {
            var chart = Chart;
            var isBase = chart is ChartBase;
            var isHeat = chart is HeatmapChart;
            var isRank = chart is RankChart;
            TitleBox.Text = chart switch { ChartBase b => b.Title, HeatmapChart h => h.Title, RankChart r => r.Title, PathwayGraph g => g.Title, _ => string.Empty };
            XLabelBox.Text = chart switch { ChartBase b => b.XLabel, RankChart r => r.XLabel, _ => string.Empty };
            YLabelBox.Text = chart is ChartBase yb ? yb.YLabel : string.Empty;
            XLabelBox.IsEnabled = isBase || isRank;
            YLabelBox.IsEnabled = isBase;
            PointSizeSlider.IsEnabled = isBase;
            PaletteBox.IsEnabled = isBase;
            ColorScaleBox.IsEnabled = isHeat;
            GridBox.IsEnabled = isBase;
            LegendBox.IsEnabled = chart is ScatterChart or LineChart;
            LabelsBox.IsEnabled = isBase || isHeat;
            LabelsBox.Content = isHeat ? "Row labels" : "Point labels";
            EllipsesBox.IsVisible = chart is ScatterChart;
            TreesBox.IsVisible = isHeat;
            ValuesBox.IsVisible = isHeat;
            if (chart is ChartBase b2)
            {
                PointSizeSlider.Value = b2.PointSize;
                FontScaleSlider.Value = b2.FontScale;
                GridBox.IsChecked = b2.ShowGrid;
                LabelsBox.IsChecked = b2.ShowPointLabels;
                PaletteBox.SelectedIndex = Math.Max(0, new[] { "Tableau", "Okabe-Ito", "Grey", "Accent" }.ToList().IndexOf(b2.Palette));
                LegendBox.IsChecked = b2 is ScatterChart s ? s.ShowLegend : b2 is LineChart l ? l.ShowLegend : true;
                EllipsesBox.IsChecked = b2 is ScatterChart s2 && s2.ShowEllipses;
            }
            else if (chart is HeatmapChart h2)
            {
                FontScaleSlider.Value = h2.FontScale;
                ColorScaleBox.SelectedIndex = Math.Max(0, HeatmapChart.ColorScales.ToList().IndexOf(h2.ColorScale));
                LabelsBox.IsChecked = h2.ShowRowLabels;
                TreesBox.IsChecked = h2.ShowTrees;
                ValuesBox.IsChecked = h2.ShowValues;
            }
            else if (chart is RankChart r2)
            {
                FontScaleSlider.Value = r2.FontScale;
            }
            else if (chart is PathwayGraph g2)
            {
                FontScaleSlider.Value = g2.FontScale;
            }
        }
        finally
        {
            _syncing = false;
        }
    }

    private void Apply(Action<ChartBase>? onBase, Action<HeatmapChart>? onHeat, Action<RankChart>? onRank, Action<PathwayGraph>? onGraph = null)
    {
        if (_syncing) return;
        switch (Chart)
        {
            case ChartBase b: onBase?.Invoke(b); break;
            case HeatmapChart h: onHeat?.Invoke(h); break;
            case RankChart r: onRank?.Invoke(r); break;
            case PathwayGraph g: onGraph?.Invoke(g); break;
        }
    }

    private void OnExportSvg(object? sender, RoutedEventArgs e) => _ = ExportAsync("svg", 0);
    private void OnExportPng2(object? sender, RoutedEventArgs e) => _ = ExportAsync("png", 2);
    private void OnExportPng4(object? sender, RoutedEventArgs e) => _ = ExportAsync("png", 4);
    private void OnExportPng6(object? sender, RoutedEventArgs e) => _ = ExportAsync("png", 6);

    /// <summary>What a script hands over instead of the save panel; null asks the person.</summary>
    public string? ExportPathOverride { get; set; }

    public async Task<string?> ExportAsync(string format, double scale)
    {
        var chart = Chart;
        if (chart is null) return null;
        var suggested = (string.IsNullOrWhiteSpace(HeadingText) ? "chart" : HeadingText.ToLowerInvariant().Replace(' ', '-').Replace("·", "").Replace("--", "-")) + "." + format;
        string? path = ExportPathOverride;
        if (path is null)
        {
            var top = TopLevel.GetTopLevel(this);
            if (top is null) return null;
            var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save the chart",
                SuggestedFileName = suggested,
                DefaultExtension = format,
                FileTypeChoices = new[] { new FilePickerFileType(format.ToUpperInvariant()) { Patterns = new[] { "*." + format } } },
            });
            path = file?.TryGetLocalPath();
        }
        if (string.IsNullOrEmpty(path)) return null;
        if (format == "svg")
        {
            ChartExport.SaveSvg(chart, path, ChartPalette.Surface((Application.Current?.ActualThemeVariant ?? Avalonia.Styling.ThemeVariant.Light) == Avalonia.Styling.ThemeVariant.Dark));
        }
        else
        {
            ChartExport.SavePng(chart, path, scale);
        }
        return path;
    }
}
