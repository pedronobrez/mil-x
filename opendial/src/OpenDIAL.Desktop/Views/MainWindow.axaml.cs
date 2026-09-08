using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenDIAL.Desktop.ViewModels;
using OpenDIAL.Pipeline.Project;

namespace OpenDIAL.Desktop.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _vm;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        DataContextChanged += (_, _) => Attach();
        Closing += async (_, e) =>
        {
            if (_vm is null || _closeConfirmed) return;
            e.Cancel = true;
            if (await _vm.ConfirmDiscardAsync("Quit anyway?"))
            {
                _closeConfirmed = true;
                Close();
            }
        };
    }

    private bool _closeConfirmed;
    private IonTableWindow? _ionTableWindow;
    private readonly Services.UiProbe? _probe = Services.UiProbe.FromEnvironment();

    private void Attach()
    {
        _vm = DataContext as MainWindowViewModel;
        if (_vm is null) return;
        _vm.ShowNewProjectWizard = async () =>
        {
            var wizard = new NewProjectWindow(new NewProjectViewModel(_vm.Settings, new Services.FileDialogService(this)));
            return await wizard.ShowDialog<OpenDialProject?>(this);
        };
        _vm.ShowSettings = async () => await new SettingsWindow(_vm.Settings).ShowDialog(this);
        _vm.ShowAbout = async () => await new AboutWindow().ShowDialog(this);
        _vm.ShowOpenQuantExport = async () => await new OpenQuantExportWindow().ShowDialog<OpenDIAL.Interop.OpenQuant.OpenQuantExportOptions?>(this);
        _vm.LogLines.CollectionChanged += OnLogChanged;
        _vm.Analytics.RequestDetachIonTable = DetachIonTable;
        // the column order the reviewer arranged is theirs, and should survive a restart
        RestoreColumnOrder(InlineIonTable());
        if (_vm.Settings.Current.IonTableDetached) DetachIonTable(true);
        AttachProbe(_vm);
    }

    private IonTableView? InlineIonTable() =>
        this.GetVisualDescendants().OfType<IonTableView>().FirstOrDefault(v => v.Name == "InlineIonTable");

    /// <summary>
    /// Moves the ion table between its place beside the evidence and a window of its own. Both host
    /// the same control bound to the same view model, so nothing about the review state moves with it.
    /// </summary>
    private void DetachIonTable(bool detached)
    {
        if (_vm is null) return;
        if (detached)
        {
            SaveColumnOrder(InlineIonTable());
            _vm.Analytics.IonTableDetached = true;
            if (_ionTableWindow is null)
            {
                _ionTableWindow = new IonTableWindow { DataContext = _vm.Analytics };
                _ionTableWindow.Closing += (_, _) =>
                {
                    SaveColumnOrder(_ionTableWindow?.TableView);
                    _ionTableWindow = null;
                    if (_vm is not null) _vm.Analytics.IonTableDetached = false;
                    Dispatcher.UIThread.Post(() => RestoreColumnOrder(InlineIonTable()));
                };
                _ionTableWindow.Show(this);
                Dispatcher.UIThread.Post(() => RestoreColumnOrder(_ionTableWindow?.TableView));
            }
            else
            {
                _ionTableWindow.Activate();
            }
        }
        else
        {
            _ionTableWindow?.Close();
        }
        _vm.Settings.Current.IonTableDetached = detached;
        _ = _vm.Settings.SaveAsync();
    }

    private void SaveColumnOrder(IonTableView? view)
    {
        if (_vm is null || view is null) return;
        var grid = view.Table;
        if (grid.Columns.Count == 0) return;
        _vm.Settings.Current.IonTableColumnOrder = grid.Columns
            .OrderBy(c => c.DisplayIndex)
            .Select(c => c.Header?.ToString() ?? string.Empty)
            .ToList();
        _ = _vm.Settings.SaveAsync();
    }

    private void RestoreColumnOrder(IonTableView? view)
    {
        var order = _vm?.Settings.Current.IonTableColumnOrder;
        if (view is null || order is null || order.Count == 0) return;
        var grid = view.Table;
        var index = 0;
        foreach (var header in order)
        {
            var column = grid.Columns.FirstOrDefault(c => (c.Header?.ToString() ?? string.Empty) == header);
            if (column is null) continue;
            if (index < grid.Columns.Count) column.DisplayIndex = index;
            index++;
        }
    }

    /// <summary>
    /// Reports what the window is showing to a file, when the environment asked for one. Nothing
    /// happens otherwise, which is every run but a smoke test.
    /// </summary>
    private void AttachProbe(MainWindowViewModel vm)
    {
        if (_probe is null) return;
        void Report(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => _probe.Schedule(this, vm);
        vm.PropertyChanged += Report;
        vm.Analytics.PropertyChanged += Report;
        vm.Statistics.PropertyChanged += Report;
        vm.Analytics.IonRows.CollectionChanged += (_, _) => _probe.Schedule(this, vm);
        // once at the start, so the reader knows the window is up before anything has changed
        Dispatcher.UIThread.Post(() => _probe.Write(this, vm), DispatcherPriority.Background);
    }

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && _vm is { LogLines.Count: > 0 } && LogList.IsVisible)
        {
            LogList.ScrollIntoView(_vm.LogLines[^1]);
        }
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();
}
