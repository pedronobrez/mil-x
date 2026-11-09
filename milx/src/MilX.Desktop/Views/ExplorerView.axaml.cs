using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using MilX.Desktop.Controls;
using MilX.Desktop.ViewModels;

namespace MilX.Desktop.Views;

public partial class ExplorerView : UserControl
{
    public static readonly IValueConverter WeightForNode = new FuncValueConverter<bool, FontWeight>(isLeaf => isLeaf ? FontWeight.Normal : FontWeight.SemiBold);
    public static readonly IValueConverter YLabelFor = new FuncValueConverter<bool, string>(normalized => normalized ? "Relative intensity (%)" : "Intensity");

    private bool _syncingScan;

    public ExplorerView()
    {
        InitializeComponent();
        Chromatogram.PointClicked += (_, e) =>
        {
            if (DataContext is ExplorerViewModel vm) vm.ShowScanAt(e.X);
        };
        Contour.PointClicked += (_, e) =>
        {
            if (DataContext is ExplorerViewModel vm) vm.ContourClicked(e.X);
        };
    }

    private void OnSpectrumPeakDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        if (sender is DataGrid grid && grid.SelectedItem is SpectrumPeakRow row) Vm?.XicOfSpectrumPeak(row);
    }

    private async void OnRebuildContour(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Vm is null) return;
        var (lo, hi) = Contour.ViewWindow;
        await Vm.RebuildContourAsync(double.IsNaN(lo) ? null : lo, double.IsNaN(hi) ? null : hi);
    }

    private async void OnExtractContourView(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Vm is null) return;
        var (lo, hi) = Contour.ViewWindow;
        if (double.IsNaN(lo) || double.IsNaN(hi)) return;
        await Vm.ExtractContourViewAsync(lo, hi);
    }

    private ExplorerViewModel? Vm => DataContext as ExplorerViewModel;

    private void OnOverview(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Chromatogram.ResetView();

    private void OnScanChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_syncingScan || Vm is null || e.NewValue is null) return;
        var n = (int)e.NewValue.Value;
        if (n != Vm.ScanNumber && n >= 1)
        {
            _syncingScan = true;
            try { Vm.GoToScan(n); } finally { _syncingScan = false; }
        }
    }
}
