using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using OpenDIAL.Desktop.Controls;
using OpenDIAL.Desktop.ViewModels;

namespace OpenDIAL.Desktop.Views;

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
