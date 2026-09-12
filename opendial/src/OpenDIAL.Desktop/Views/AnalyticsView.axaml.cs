using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using OpenDIAL.Desktop.Controls;
using OpenDIAL.Desktop.ViewModels;

namespace OpenDIAL.Desktop.Views;

public partial class AnalyticsView : UserControl
{
    public AnalyticsView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => WatchLayout();
        ReviewGrid.SizeChanged += (_, _) => SizeDockPreview();
    }

    private AnalyticsViewModel? Vm => DataContext as AnalyticsViewModel;

    private AnalyticsViewModel? _watched;
    private GridLength _tableWidth = new(1.15, GridUnitType.Star);
    private GridLength _splitterWidth = new(6);

    private void WatchLayout()
    {
        if (_watched is not null) _watched.PropertyChanged -= OnLayoutPropertyChanged;
        _watched = Vm;
        if (_watched is not null) _watched.PropertyChanged += OnLayoutPropertyChanged;
        LayoutTableColumn();
    }

    private void OnLayoutPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AnalyticsViewModel.IonTableDetached)) LayoutTableColumn();
        else if (e.PropertyName == nameof(AnalyticsViewModel.IonTableDockPreview)) SizeDockPreview();
    }

    /// <summary>
    /// The table's column and the splitter beside it collapse while the table is in its own window,
    /// so the peaks and the spectrum take the width; they come back at the width the reviewer had
    /// dragged them to. Hiding the control alone would leave its column standing empty.
    /// </summary>
    private void LayoutTableColumn()
    {
        var columns = ReviewGrid.ColumnDefinitions;
        if (columns.Count < 3) return;
        var detached = Vm?.IonTableDetached == true;
        if (detached)
        {
            if (columns[0].Width.Value > 0) _tableWidth = columns[0].Width;
            if (columns[1].Width.Value > 0) _splitterWidth = columns[1].Width;
            columns[0].MinWidth = 0;
            columns[0].Width = new GridLength(0);
            columns[1].Width = new GridLength(0);
        }
        else if (columns[0].Width.Value == 0)
        {
            columns[0].Width = _tableWidth;
            columns[1].Width = _splitterWidth;
        }
        SizeDockPreview();
    }

    /// <summary>The preview is as wide as the table will be once it is back: its share of the grid.</summary>
    private void SizeDockPreview()
    {
        var columns = ReviewGrid.ColumnDefinitions;
        if (columns.Count < 3 || ReviewGrid.Bounds.Width <= 0) return;
        var table = _tableWidth.IsStar ? _tableWidth.Value : 1.15;
        var rest = columns[2].Width.IsStar ? columns[2].Width.Value : 1.35;
        var share = table / (table + rest);
        var splitter = _splitterWidth.IsAbsolute ? _splitterWidth.Value : 6;
        DockPreview.Width = Math.Max(120, (ReviewGrid.Bounds.Width - splitter) * share);
    }

    /// <summary>The widths the review grid gives the table and the splitter right now; for the tests.</summary>
    internal (double Table, double Splitter) TableColumnWidths =>
        (ReviewGrid.ColumnDefinitions[0].ActualWidth, ReviewGrid.ColumnDefinitions[1].ActualWidth);

    private void OnPanelClicked(object? sender, ChartPointEventArgs e)
    {
        if (sender is Control { DataContext: ReviewPanelViewModel panel }) Vm?.SelectSample(panel.Sample.FileId);
    }

    private void OnPanelDoubleClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ReviewPanelViewModel panel }) Vm?.Magnify(panel);
    }

    /// <summary>
    /// Shift-dragging across a review panel is how the reviewer draws an integration window: the
    /// drag fills the boxes of the integrate strip, and the panel it came from becomes the sample
    /// in focus so "This sample" means the one under the cursor.
    /// </summary>
    private void OnRangeSelected(object? sender, ChartRangeEventArgs e)
    {
        if (Vm is null || !e.IsFinal) return;
        if (sender is Control { DataContext: ReviewPanelViewModel panel }) Vm.SelectSample(panel.Sample.FileId);
        Vm.SetIntegrationWindow(e.Start, e.End);
    }

    private void OnMagnifySelected(object? sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        var selected = Vm.SelectedSampleRow;
        var panel = Vm.Panels.FirstOrDefault(p => selected is not null && p.Sample.FileId == selected.Peak.FileId) ?? Vm.Panels.FirstOrDefault();
        Vm.Magnify(panel);
    }
}
