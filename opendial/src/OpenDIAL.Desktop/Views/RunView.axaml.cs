using System.Collections.Specialized;
using Avalonia.Controls;
using OpenDIAL.Desktop.ViewModels;

namespace OpenDIAL.Desktop.Views;

public partial class RunView : UserControl
{
    private RunViewModel? _vm;

    public RunView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Attach();
    }

    private void Attach()
    {
        if (_vm is not null)
        {
            _vm.LogLines.CollectionChanged -= OnLogChanged;
        }
        _vm = DataContext as RunViewModel;
        if (_vm is not null)
        {
            _vm.LogLines.CollectionChanged += OnLogChanged;
        }
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && _vm is { LogLines.Count: > 0 })
        {
            LogList.ScrollIntoView(_vm.LogLines[^1]);
        }
    }
}
