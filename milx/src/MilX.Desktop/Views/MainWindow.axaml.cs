using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MilX.Desktop.ViewModels;
using MilX.Pipeline.Project;

namespace MilX.Desktop.Views;

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
            return await wizard.ShowDialog<MilXProject?>(this);
        };
        _vm.ShowSettings = async () => await new SettingsWindow(_vm.Settings).ShowDialog(this);
        _vm.ShowAbout = async () => await new AboutWindow().ShowDialog(this);
        _vm.ShowOpenQuantExport = async () => await new OpenQuantExportWindow().ShowDialog<MilX.Interop.OpenQuant.OpenQuantExportOptions?>(this);
        _vm.ShowHelpWindow = ShowHelp;
        _vm.Statistics.Analysis.ShowStandardsDialog = async (confirmed, current) =>
            await new StandardsWindow(confirmed, current).ShowDialog<IReadOnlyList<MilX.Pipeline.Statistics.StandardAssignment>?>(this);
        _vm.LogLines.CollectionChanged += OnLogChanged;
        _vm.Analytics.RequestDetachIonTable = DetachIonTable;
        // how the last figure left the application is how the next export dialog opens
        ChartExportWindow.Defaults = _vm.Settings.Current.ChartExport;
        ChartExportWindow.Remember = options =>
        {
            _vm.Settings.Current.ChartExport = options;
            _ = _vm.Settings.SaveAsync();
        };
        // the column order the reviewer arranged is theirs, and should survive a restart
        RestoreColumnOrder(InlineIonTable());
        if (_vm.Settings.Current.IonTableDetached)
        {
            // the table's window is owned by this one, and an owner has to be on screen first: at
            // start-up the data context arrives before the window is shown, so the tear-off waits
            if (IsVisible) DetachIonTable(true);
            else Opened += (_, _) => Dispatcher.UIThread.Post(() => { if (_vm?.Settings.Current.IonTableDetached == true) DetachIonTable(true); });
        }
        _vm.GatherReport = GatherReport;
        AttachProbe(_vm);
    }

    /// <summary>
    /// What the report says. The counts and the method the view models know; the figures only this
    /// window can reach, because a figure is a control that has been laid out.
    /// </summary>
    private Services.ReportContent GatherReport()
    {
        var content = new Services.ReportContent();
        if (_vm is null) return content;
        var analytics = _vm.Analytics;
        var counts = new List<Services.ReportRow>
        {
            new Services.ReportRow("Features", analytics.IonRows.Count.ToString("N0")),
            new Services.ReportRow("Annotated", analytics.AnnotatedCount.ToString("N0")),
            new Services.ReportRow("Confirmed", analytics.ConfirmedCount.ToString("N0")),
            new Services.ReportRow("Misannotation", analytics.RejectedCount.ToString("N0")),
            new Services.ReportRow("Reviewed", analytics.ReviewedCount.ToString("N0")),
            new Services.ReportRow("Injections", _vm.Samples.Samples.Count.ToString("N0")),
            new Services.ReportRow("Ion groups", analytics.GroupSummary),
        };
        // a result read against the other polarity is a different result, and the report must say so
        if (analytics.HasPolarityLink) counts.Add(new Services.ReportRow("Both polarities", analytics.PolaritySummary));
        content.Counts = counts;
        content.Library = new[]
        {
            new Services.ReportRow("File", _vm.Method.Parameters.MspFilePath),
            new Services.ReportRow("What it holds", _vm.Method.LibraryReport),
            new Services.ReportRow("Retention time in the score", _vm.Method.Parameters.UseRetentionInformationForScoring ? "yes" : "no"),
        };
        content.Injections = _vm.Samples.Samples
            .Select(sample => (sample.Name, sample.Class, sample.SampleType.ToString(), string.Empty, string.Empty))
            .ToList();
        content.Method = _vm.Method.Parameters.MethodText;
        content.Log = _vm.Run.LogLines.TakeLast(80).ToList();

        var figures = new List<Services.ReportFigure>();
        foreach (var (chart, title) in NamedCharts())
        {
            if (Services.RunReport.Figure(chart, title) is { } figure) figures.Add(figure);
        }
        content.Figures = figures;
        return content;
    }

    /// <summary>The charts worth putting in a report, in the order a reader wants them.</summary>
    private IEnumerable<(Control Chart, string Title)> NamedCharts()
    {
        var wanted = new[] { "Ms2Mirror", "Chromatogram", "Spectrum" };
        foreach (var name in wanted)
        {
            var chart = this.GetVisualDescendants().OfType<Control>()
                .FirstOrDefault(c => c.Name == name && c is Charts.IChartRenderable && c.IsEffectivelyVisible);
            if (chart is not null) yield return (chart, name switch
            {
                "Ms2Mirror" => "The product spectrum of the selected feature against the library",
                "Chromatogram" => "The chromatogram in the Explorer",
                _ => "The spectrum in the Explorer",
            });
        }
        foreach (var frame in this.GetVisualDescendants().OfType<Controls.ChartFrame>().Where(f => f.IsEffectivelyVisible))
        {
            if (frame.Chart is { } chart) yield return (chart, frame.HeadingText ?? "Statistics");
        }
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
                    StopDockWatch();
                    _ionTableWindow = null;
                    if (_vm is not null)
                    {
                        _vm.Analytics.IonTableDetached = false;
                        _vm.Analytics.IonTableDockPreview = false;
                    }
                    Dispatcher.UIThread.Post(() => RestoreColumnOrder(InlineIonTable()));
                };
                _ionTableWindow.Opened += (_, _) => _ionTableShownAt = _ionTableWindow?.Position;
                _ionTableWindow.PositionChanged += (_, e) => OnIonTableWindowMoved(e.Point);
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

    // ---- docking the table back by dragging its window over the main one ----------------------
    //
    // Dragging the torn-off window by its title bar over the left part of the main window shows,
    // in the main window, the place the table will take; letting go there docks it. The window
    // manager owns the mouse during such a drag, so the drop is seen by polling the button.

    private PixelPoint? _ionTableShownAt;
    private bool _ionTableDragSeen;
    private DispatcherTimer? _dockWatch;

    private PixelRect MainFrame()
    {
        var size = FrameSize ?? ClientSize;
        return new PixelRect(Position, PixelSize.FromSize(size, RenderScaling));
    }

    private bool IonTableWindowIsOverDockZone()
    {
        if (_ionTableWindow is null || !IsVisible || WindowState == WindowState.Minimized) return false;
        var frame = PixelSize.FromSize(_ionTableWindow.FrameSize ?? _ionTableWindow.ClientSize, _ionTableWindow.RenderScaling);
        return Services.DockSnap.IsOverZone(MainFrame(), _ionTableWindow.Position, frame, _ionTableWindow.RenderScaling);
    }

    private void OnIonTableWindowMoved(PixelPoint now)
    {
        if (_vm is null || _ionTableWindow is null) return;
        var down = Services.MouseButtons.LeftIsDown();
        if (down is null) return;   // a platform that cannot say: no snapping, no harm
        if (_ionTableShownAt is { } shownAt && !Services.DockSnap.IsADrag(shownAt, now)) return;
        if (down == true) _ionTableDragSeen = true;
        if (!_ionTableDragSeen) return;   // moved by something other than a hand: the window manager, a script
        _vm.Analytics.IonTableDockPreview = IonTableWindowIsOverDockZone();
        if (_dockWatch is null)
        {
            _dockWatch = new DispatcherTimer(TimeSpan.FromMilliseconds(30), DispatcherPriority.Input, (_, _) => WatchForDrop());
            _dockWatch.Start();
        }
    }

    private void WatchForDrop()
    {
        if (_vm is null || _ionTableWindow is null) { StopDockWatch(); return; }
        if (Services.MouseButtons.LeftIsDown() != false) return;   // still holding it
        var drop = IonTableWindowIsOverDockZone();
        StopDockWatch();
        _vm.Analytics.IonTableDockPreview = false;
        if (drop) DetachIonTable(false);
    }

    private void StopDockWatch()
    {
        _dockWatch?.Stop();
        _dockWatch = null;
        _ionTableDragSeen = false;
        _ionTableShownAt = _ionTableWindow?.Position;
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
                var n = await _vm.Analytics.ExportOpenQuantAsync(Path(), new MilX.Interop.OpenQuant.OpenQuantExportOptions { AnnotatedOnly = true });
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
            case "helpLanguage":
            {
                // the manual in the other language, on the same page — what the language button does
                if (_vm.Help is null) return "no help window";
                var code = command.TryGetProperty("language", out var lg) ? lg.GetString() : null;
                if (string.IsNullOrEmpty(code)) _vm.Help.ToggleLanguageCommand.Execute(null);
                else _vm.Help.SwitchLanguage(code);
                return "help in " + _vm.Help.Language;
            }
            case "closeHelp":
                _helpWindow?.Close();
                return "help closed";
            case "selectStatisticsPage":
            {
                // the page by its header, the way a person picks it; the workspace is shown first
                var header = command.TryGetProperty("page", out var pg) ? pg.GetString() : null;
                _vm.SelectedWorkspace = 4;
                UpdateLayout();   // the workspace is built the first time it is shown, and this may be that time
                var tabs = this.GetVisualDescendants().OfType<TabControl>().FirstOrDefault(t => t.Name == "StatsTabs") ?? throw new InvalidOperationException("the statistics workspace is not built");
                var item = tabs.Items.OfType<TabItem>().FirstOrDefault(t => string.Equals(t.Header as string, header, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException($"no page '{header}'");
                tabs.SelectedItem = item;
                UpdateLayout();
                return $"{header} shown";
            }
            case "selectFeature":
            {
                // the row a reviewer would click, by the alignment id the table shows. The field is
                // "feature" rather than "id": every command carries an "id" of its own already.
                SelectedWorkspaceForReview();
                if (command.TryGetProperty("feature", out var fid))
                {
                    var wanted = fid.GetInt32();
                    _vm.Analytics.SelectFeature(wanted);
                    UpdateLayout();
                    await _vm.Analytics.SpectrumReady;   // the panel's spectrum arrives off the interface thread
                    var row = _vm.Analytics.SelectedRow;
                    if (row is null || row.Id != wanted) throw new ArgumentException($"no feature #{wanted} in the table");
                    return $"#{row.Id} {row.DisplayName} selected";
                }
                // or the first row that is what the script is looking for, since a script cannot know
                // which ids of a given project carry a library match
                var what = command.TryGetProperty("where", out var w) ? w.GetString() : null;
                if (what is null) throw new ArgumentException("the command needs a feature or a where");
                var tried = 0;
                foreach (var candidate in _vm.Analytics.IonRows.Where(r => r.IsConfident))
                {
                    if (++tried > 60) break;
                    _vm.Analytics.SelectFeature(candidate.Id);
                    UpdateLayout();
                    await _vm.Analytics.SpectrumReady;
                    var mirrored = (_vm.Analytics.ReferencePeaks?.Count ?? 0) > 0 && _vm.Analytics.Ms2Peaks.Count > 0;
                    if (!string.Equals(what, "mirrored", StringComparison.OrdinalIgnoreCase) || mirrored)
                    {
                        return $"#{candidate.Id} {candidate.DisplayName} selected";
                    }
                }
                throw new ArgumentException($"no feature of the {_vm.Analytics.IonRows.Count} listed is {what} (tried {tried})");
            }
            case "linkPolarity":
            {
                // the alignment of the other polarity, as the button's file picker would give it
                var sentence = await _vm.Analytics.LinkPolarityAsync(Path());
                if (!_vm.Analytics.HasPolarityLink) throw new InvalidOperationException(sentence);
                return sentence;
            }
            case "unlinkPolarity":
                _vm.Analytics.UnlinkPolarity();
                return "unlinked";
            case "runReport":
            {
                if (_vm.GatherReport is null) throw new InvalidOperationException("the report is not wired");
                var content = _vm.GatherReport();
                content.Version = AppInfo.Version;
                content.Project = _vm.ProjectName;
                content.OutputFolder = _vm.OutputFolder;
                var text = Services.RunReport.Build(content);
                await File.WriteAllTextAsync(Path(), text);
                return $"{content.Figures.Count} figure(s), {text.Length} characters";
            }
            case "exportChart":
            {
                // a chart written without the two dialogs: the settings come with the command, the
                // path with it too. Either a chart of a statistics page by its index, or any named
                // chart of any workspace — the chromatogram and the mirror are named ones.
                var options = ExportOptionsFrom(command);
                if (command.TryGetProperty("control", out var cn) && cn.GetString() is { Length: > 0 } name)
                {
                    var chart = this.GetVisualDescendants().OfType<Control>()
                        .FirstOrDefault(c => c.Name == name && c is Charts.IChartRenderable && c.IsEffectivelyVisible)
                        ?? throw new ArgumentException($"no chart named '{name}' is showing");
                    var written = await Charts.ChartExportFlow.RunAsync(chart, name, options, Path());
                    return written is null ? "nothing written" : $"{name} written to {written}";
                }
                var header = command.TryGetProperty("page", out var pg) ? pg.GetString() : null;
                if (header is not null) await RunProbeCommandAsync(System.Text.Json.JsonDocument.Parse($"{{\"action\":\"selectStatisticsPage\",\"page\":{System.Text.Json.JsonSerializer.Serialize(header)}}}").RootElement);
                var index = command.TryGetProperty("index", out var ix) ? ix.GetInt32() : 0;
                var frames = this.GetVisualDescendants().OfType<Controls.ChartFrame>().Where(c => c.IsEffectivelyVisible).ToList();
                if (index < 0 || index >= frames.Count) throw new ArgumentException($"the page has {frames.Count} chart(s)");
                var frame = frames[index];
                frame.ExportPathOverride = Path();
                try
                {
                    var written = await frame.ExportAsync(options);
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

    /// <summary>The export settings a command asked for; what it left out is what the dialog would default to.</summary>
    private static Charts.ChartExportOptions ExportOptionsFrom(System.Text.Json.JsonElement command)
    {
        var defaults = new Charts.ChartExportOptions();
        return new Charts.ChartExportOptions
        {
            Format = command.TryGetProperty("format", out var f) ? f.GetString() ?? defaults.Format : defaults.Format,
            Scale = command.TryGetProperty("scale", out var sc) ? sc.GetDouble() : defaults.Scale,
            Theme = command.TryGetProperty("theme", out var th) ? th.GetString() ?? defaults.Theme : defaults.Theme,
            Background = command.TryGetProperty("background", out var bg) ? bg.GetString() ?? defaults.Background : defaults.Background,
            FontScale = command.TryGetProperty("fontScale", out var fs) ? fs.GetDouble() : defaults.FontScale,
        };
    }

    /// <summary>The review workspace, or the ion table's own window when the table is out there.</summary>
    private void SelectedWorkspaceForReview()
    {
        if (_vm is null) return;
        if (!_vm.Analytics.IonTableDetached) _vm.SelectedWorkspace = 1;
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
