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
    }

    private AnalyticsViewModel? Vm => DataContext as AnalyticsViewModel;

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
