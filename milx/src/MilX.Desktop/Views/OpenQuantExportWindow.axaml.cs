using Avalonia.Controls;
using Avalonia.Interactivity;
using MilX.Interop.OpenQuant;

namespace MilX.Desktop.Views;

/// <summary>Small options dialog for the OpenQuant component export; closes with the options or null.</summary>
public partial class OpenQuantExportWindow : Window
{
    public OpenQuantExportWindow()
    {
        InitializeComponent();
    }

    private void OnExport(object? sender, RoutedEventArgs e)
    {
        var unit = (Unit.SelectedItem as ComboBoxItem)?.Content as string ?? "Da";
        var options = new OpenQuantExportOptions
        {
            AnnotatedOnly = AnnotatedOnly.IsChecked == true,
            UseFragment = UseFragment.IsChecked == true,
            RtHalfWidthMinutes = (double)(RtHalfWidth.Value ?? 0.3m),
            Tolerance = (double)(Tolerance.Value ?? 0.02m),
            Unit = unit,
        };
        Close(options);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
