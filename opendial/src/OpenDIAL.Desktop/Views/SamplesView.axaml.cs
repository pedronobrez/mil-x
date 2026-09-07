using Avalonia.Controls;
using OpenDIAL.Desktop.ViewModels;

namespace OpenDIAL.Desktop.Views;

public partial class SamplesView : UserControl
{
    public SamplesView()
    {
        InitializeComponent();
    }

    private void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not SamplesViewModel vm) return;
        vm.SelectedSamples.Clear();
        foreach (var item in Grid.SelectedItems)
        {
            if (item is InputFileViewModel row) vm.SelectedSamples.Add(row);
        }
    }
}
