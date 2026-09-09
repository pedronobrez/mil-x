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
    private HelpWindow? _helpWindow;

    /// <summary>
    /// One help window for the session, beside the main one rather than on top of it, so a page can
    /// stay open while the thing it describes is being used.
    /// </summary>
    private void ShowHelp()
    {
        if (_vm is null) return;
        var help = _vm.EnsureHelp();
        if (_helpWindow is null)
        {
            _helpWindow = new HelpWindow { DataContext = help };
            _helpWindow.Closed += (_, _) => { _helpWindow = null; help.IsOpen = false; _probe?.Schedule(this, _vm); };
            _helpWindow.Show(this);
        }
        else
        {
            _helpWindow.Activate();
        }
        help.IsOpen = true;
        _probe?.Schedule(this, _vm);
    }
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
        _vm.ShowHelpWindow = ShowHelp;
        _vm.Statistics.Analysis.ShowStandardsDialog = async (confirmed, current) =>
            await new StandardsWindow(confirmed, current).ShowDialog<IReadOnlyList<OpenDIAL.Pipeline.Statistics.StandardAssignment>?>(this);
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
        vm.Statistics.Analysis.PropertyChanged += Report;   // the heatmap, the enrichment and the rest of the one-factor pages
        vm.Statistics.Pathways.PropertyChanged += Report;
        vm.Statistics.TwoFactor.PropertyChanged += Report;
        vm.Analytics.IonRows.CollectionChanged += (_, _) => _probe.Schedule(this, vm);
        vm.Run.PropertyChanged += Report;
        vm.Run.LogLines.CollectionChanged += (_, _) => _probe.Schedule(this, vm);
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MainWindowViewModel.Help) && vm.Help is not null) vm.Help.PropertyChanged += Report; };
        _probe.CommandHandler = RunProbeCommandAsync;
        _probe.Listen(this, vm);
        // once at the start, so the reader knows the window is up before anything has changed
        Dispatcher.UIThread.Post(() => _probe.Write(this, vm), DispatcherPriority.Background);
    }

    /// <summary>
    /// The few things a script cannot reach through the keyboard and the mouse, because they go
    /// through the operating system's own dialogs: each runs the same view-model method the menu
    /// runs once its dialog has closed.
    /// </summary>
    private async Task<string> RunProbeCommandAsync(System.Text.Json.JsonElement command)
    {
        if (_vm is null) return "no view model";
        var action = command.TryGetProperty("action", out var a) ? a.GetString() ?? string.Empty : string.Empty;
        string Path() => command.TryGetProperty("path", out var p) && p.GetString() is { Length: > 0 } s ? s : throw new ArgumentException("the command needs a path");
        switch (action)
        {
            case "exportReviewed":
            {
                var n = await _vm.Analytics.ExportReviewedTableAsync(Path(), areas: false);
                return $"{n} feature(s) written";
            }
            case "exportOpenQuant":
            {
                var n = await _vm.Analytics.ExportOpenQuantAsync(Path(), new OpenDIAL.Interop.OpenQuant.OpenQuantExportOptions { AnnotatedOnly = true });
                return $"{n} component(s) written";
            }
            case "reexport":
                await _vm.Analytics.ReexportAsync(Path());
                return "matrix written";
            case "openHelp":
            {
                var page = command.TryGetProperty("page", out var pg) ? pg.GetString() : null;
                _vm.OpenHelpCommand.Execute(page);
                if (command.TryGetProperty("query", out var q) && _vm.Help is not null) _vm.Help.Query = q.GetString() ?? string.Empty;
                return "help shown";
            }
            case "closeHelp":
                _helpWindow?.Close();
                return "help closed";
            case "selectStatisticsPage":
            {
                // the page by its header, the way a person picks it; the workspace is shown first
                var header = command.TryGetProperty("page", out var pg) ? pg.GetString() : null;
                _vm.SelectedWorkspace = 4;
                var tabs = this.GetVisualDescendants().OfType<TabControl>().FirstOrDefault(t => t.Name == "StatsTabs") ?? throw new InvalidOperationException("the statistics workspace is not built");
                var item = tabs.Items.OfType<TabItem>().FirstOrDefault(t => string.Equals(t.Header as string, header, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException($"no page '{header}'");
                tabs.SelectedItem = item;
                UpdateLayout();
                return $"{header} shown";
            }
            case "exportChart":
            {
                // a chart of a statistics page, by its index on the page, written without the save panel
                var header = command.TryGetProperty("page", out var pg) ? pg.GetString() : null;
                if (header is not null) await RunProbeCommandAsync(System.Text.Json.JsonDocument.Parse($"{{\"action\":\"selectStatisticsPage\",\"page\":{System.Text.Json.JsonSerializer.Serialize(header)}}}").RootElement);
                var index = command.TryGetProperty("index", out var ix) ? ix.GetInt32() : 0;
                var format = command.TryGetProperty("format", out var f) ? f.GetString() ?? "svg" : "svg";
                var scale = command.TryGetProperty("scale", out var sc) ? sc.GetDouble() : 3;
                var frames = this.GetVisualDescendants().OfType<Controls.ChartFrame>().Where(c => c.IsEffectivelyVisible).ToList();
                if (index < 0 || index >= frames.Count) throw new ArgumentException($"the page has {frames.Count} chart(s)");
                var frame = frames[index];
                frame.ExportPathOverride = Path();
                try
                {
                    var written = await frame.ExportAsync(format, scale);
                    return written is null ? "nothing written" : $"{frame.HeadingText} written to {written}";
                }
                finally
                {
                    frame.ExportPathOverride = null;
                }
            }
            default:
                throw new ArgumentException($"unknown action '{action}'");
        }
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
