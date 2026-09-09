using System.Globalization;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenDIAL.Desktop.ViewModels;

namespace OpenDIAL.Desktop.Services;

/// <summary>
/// Says what the window is showing, to a file, so something outside the process can check it.
///
/// The headless tests drive view models and the visual ones compare frames; neither of them runs
/// the packaged application, and a defect that only appears there — an asset that did not make it
/// into the bundle, a shortcut that never fires, a plugin that fails to load — passes both. The
/// smoke test drives the installed build for real, and to assert anything it has to be able to see
/// what happened. Reading that off a screenshot is guesswork; this says it plainly.
///
/// It is off unless OPENDIAL_UI_PROBE names a file, so the shipped application writes nothing.
///
/// It also reports where the named controls are inside the window, because a script that clicks at
/// a coordinate worked out from a screenshot breaks the moment the window moves or the layout
/// shifts. Asking the window where its own buttons are is both robust and honest.
///
/// One thing flows the other way. Beside the probe file the application watches for a
/// "&lt;probe&gt;.commands" file and runs what it finds through the same view-model commands the
/// buttons use. The smoke test needs that for exactly one thing: an export goes through the
/// operating system's own save panel, which is not this application's code to drive, so the
/// script hands the path over here instead and everything after the panel runs for real.
/// </summary>
public sealed class UiProbe
{
    private readonly string _path;
    private bool _scheduled;
    private DispatcherTimer? _timer;
    private Dictionary<string, object?>? _lastCommand;

    private UiProbe(string path) => _path = path;

    /// <summary>The probe, or null when the environment did not ask for one.</summary>
    public static UiProbe? FromEnvironment() =>
        Environment.GetEnvironmentVariable("OPENDIAL_UI_PROBE") is { Length: > 0 } path ? new UiProbe(path) : null;

    public string CommandPath => _path + ".commands";

    /// <summary>
    /// Runs one command from the command file and returns a message for the probe. Set by the
    /// window, which is what has the view models.
    /// </summary>
    public Func<JsonElement, Task<string>>? CommandHandler { get; set; }

    /// <summary>
    /// Writes after the current burst of changes rather than on each one: loading a project moves a
    /// dozen properties, and the reader only ever wants the state they settled into.
    /// </summary>
    public void Schedule(Window window, MainWindowViewModel vm)
    {
        if (_scheduled) return;
        _scheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _scheduled = false;
            Write(window, vm);
        }, DispatcherPriority.Background);
    }

    /// <summary>Starts watching the command file. Harmless to call twice.</summary>
    public void Listen(Window window, MainWindowViewModel vm)
    {
        if (_timer is not null) return;
        _timer = new DispatcherTimer(TimeSpan.FromMilliseconds(400), DispatcherPriority.Background, (_, _) => _ = PollAsync(window, vm));
        _timer.Start();
    }

    private bool _polling;

    private async Task PollAsync(Window window, MainWindowViewModel vm)
    {
        if (_polling || CommandHandler is null || !File.Exists(CommandPath)) return;
        _polling = true;
        try
        {
            string text;
            try
            {
                text = File.ReadAllText(CommandPath);
                File.Delete(CommandPath);
            }
            catch (IOException)
            {
                return;   // still being written; next tick
            }
            using var document = JsonDocument.Parse(text);
            var commands = document.RootElement.ValueKind == JsonValueKind.Array
                ? document.RootElement.EnumerateArray().ToList()
                : new List<JsonElement> { document.RootElement };
            foreach (var command in commands)
            {
                var id = command.TryGetProperty("id", out var i) ? i.ToString() : string.Empty;
                var action = command.TryGetProperty("action", out var a) ? a.GetString() ?? string.Empty : string.Empty;
                string message;
                var ok = true;
                try
                {
                    message = await CommandHandler(command);
                }
                catch (Exception ex)
                {
                    ok = false;
                    message = ex.Message;
                }
                _lastCommand = new Dictionary<string, object?>
                {
                    ["id"] = id,
                    ["action"] = action,
                    ["ok"] = ok,
                    ["message"] = message,
                    ["completedAt"] = DateTimeOffset.Now.ToString("O"),
                };
                Write(window, vm);
            }
        }
        catch (Exception ex)
        {
            _lastCommand = new Dictionary<string, object?> { ["ok"] = false, ["message"] = "unreadable command file: " + ex.Message };
            Write(window, vm);
        }
        finally
        {
            _polling = false;
        }
    }

    public void Write(Window window, MainWindowViewModel vm)
    {
        try
        {
            var analytics = vm.Analytics;
            var selected = analytics.SelectedRow;
            var snapshot = new Dictionary<string, object?>
            {
                ["writtenAt"] = DateTimeOffset.Now.ToString("O"),
                ["version"] = AppInfo.Version,
                ["title"] = vm.Title,
                ["status"] = vm.Status,
                ["workspace"] = vm.SelectedWorkspace,
                ["workspaceName"] = WorkspaceName(vm.SelectedWorkspace),
                ["projectName"] = vm.ProjectName,
                ["projectPath"] = vm.ProjectPath,
                ["outputFolder"] = vm.OutputFolder,
                ["hasProject"] = vm.HasProject,
                ["hasResults"] = vm.HasResults,
                ["isDirty"] = vm.IsDirty,
                ["ionRows"] = analytics.IonRows.Count,
                ["samples"] = vm.Samples.Samples.Count,
                ["run"] = new Dictionary<string, object?>
                {
                    ["running"] = vm.Run.IsRunning,
                    ["stage"] = vm.Run.Stage,
                    ["progress"] = Math.Round(vm.Run.Progress, 1),
                    ["file"] = vm.Run.CurrentFile,
                    ["error"] = vm.Run.LastError,
                    ["log"] = vm.Run.LogLines.TakeLast(12).ToList(),
                    // how many files the raw-file plugin opened itself, which is the one fact about
                    // the bundle the log tail cannot keep once the run is long
                    ["nativeReads"] = vm.Run.LogLines.Count(l => l.Contains("read natively", StringComparison.Ordinal)),
                    ["conversions"] = vm.Run.LogLines.Count(l => l.Contains("Converting vendor format", StringComparison.Ordinal)),
                },
                ["review"] = new Dictionary<string, object?>
                {
                    ["listed"] = analytics.IonRows.Count,
                    ["confirmed"] = analytics.ConfirmedCount,
                    ["rejected"] = analytics.RejectedCount,
                    ["reviewed"] = analytics.ReviewedCount,
                    ["peaksEdited"] = analytics.PeaksEdited,
                    ["curationDirty"] = analytics.CurationDirty,
                    ["filter"] = analytics.FilterText,
                    ["integrationFrom"] = analytics.IntegrationFrom,
                    ["integrationTo"] = analytics.IntegrationTo,
                    ["summary"] = analytics.Summary,
                },
                ["selected"] = selected is null ? null : new Dictionary<string, object?>
                {
                    ["id"] = selected.Id,
                    ["name"] = selected.DisplayName,
                    ["rt"] = Math.Round(selected.Rt, 4),
                    ["mz"] = Math.Round(selected.Mz, 5),
                    // the m/z as the ion table prints it, which is what the filter box matches on
                    ["mzText"] = selected.Mz.ToString("F4", CultureInfo.InvariantCulture),
                    ["meanHeight"] = Math.Round(selected.Height, 1),
                    ["fill"] = Math.Round(selected.Fill, 1),
                    ["manuallyQuantified"] = selected.Spot.IsManuallyQuantified,
                    ["tags"] = selected.TagText,
                    ["samples"] = selected.Spot.SamplePeaks.Select(p => new Dictionary<string, object?>
                    {
                        ["file"] = p.FileName,
                        ["height"] = double.IsNaN(p.Height) ? null : Math.Round(p.Height, 1),
                        ["area"] = double.IsNaN(p.Area) ? null : Math.Round(p.Area, 1),
                        ["rt"] = double.IsNaN(p.Rt) ? null : Math.Round(p.Rt, 4),
                        ["left"] = double.IsNaN(p.RtLeft) ? null : Math.Round(p.RtLeft, 4),
                        ["right"] = double.IsNaN(p.RtRight) ? null : Math.Round(p.RtRight, 4),
                        ["gapFilled"] = p.IsGapFilled,
                    }).ToList(),
                },
                ["statistics"] = new Dictionary<string, object?>
                {
                    ["hasResults"] = vm.Statistics.HasResults,
                    ["hasDiscriminant"] = vm.Statistics.HasPls,
                    ["hasOrthogonal"] = vm.Statistics.HasOpls,
                    ["hasCorrection"] = vm.Statistics.HasCorrection,
                    ["page"] = StatisticsPage(window),
                    ["source"] = vm.Statistics.Analysis.SourceMode,
                    ["confirmed"] = vm.Statistics.Analysis.ConfirmedCount,
                    ["features"] = vm.Statistics.Analysis.Data?.Scaled.FeatureCount,
                    ["standards"] = vm.Statistics.Analysis.StandardsSummary,
                    ["comparison"] = vm.Statistics.Analysis.ComparisonCounts,
                    ["heatmapRows"] = vm.Statistics.Analysis.Heatmap?.RowLabels.Count ?? 0,
                    ["heatmap"] = vm.Statistics.Analysis.HeatmapMessage,
                    ["enrichmentSets"] = vm.Statistics.Analysis.EnrichmentRows.Count,
                    ["enrichment"] = vm.Statistics.Analysis.EnrichmentMessage,
                    ["reactions"] = vm.Statistics.Pathways.Reactions.Count(r => r.Tested),
                    ["pathways"] = vm.Statistics.Pathways.Message,
                    ["twoFactor"] = vm.Statistics.TwoFactor.Message,
                    ["summary"] = vm.Statistics.Summary,
                },
                ["help"] = vm.Help is null ? null : new Dictionary<string, object?>
                {
                    ["open"] = vm.Help.IsOpen,
                    ["page"] = vm.Help.Current?.Slug,
                    ["language"] = vm.Help.Language,
                    ["title"] = vm.Help.Current?.Title,
                    ["query"] = vm.Help.Query,
                    ["results"] = vm.Help.Results.Count,
                },
                ["client"] = new Dictionary<string, object?>
                {
                    ["width"] = Math.Round(window.ClientSize.Width, 1),
                    ["height"] = Math.Round(window.ClientSize.Height, 1),
                    ["scaling"] = window.RenderScaling,
                },
                ["controls"] = Controls(window),
                ["lastCommand"] = _lastCommand,
            };

            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            // written whole, so a reader never catches it half finished
            var temporary = _path + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, _path, overwrite: true);
        }
        catch
        {
            // a probe that breaks the application it is watching is worse than no probe
        }
    }

    private static string WorkspaceName(int index) => index switch
    {
        0 => "Explorer",
        1 => "Analytics",
        2 => "Method",
        3 => "Samples",
        4 => "Statistics",
        _ => "?",
    };

    /// <summary>
    /// Where the workspace tabs, the evidence tabs and every named control are, relative to the top
    /// left of the window's own content and in the units layout is done in.
    ///
    /// Deliberately not in screen coordinates. Turning these into a place to click means knowing
    /// where the window frame is and how tall its title bar is, and both of those are the window
    /// manager's business, not the application's: the one platform-specific conversion belongs in
    /// the script that already has to know about the platform. The client size is reported with them
    /// so that conversion has everything it needs.
    /// </summary>
    /// <summary>The header of the statistics page showing, or null when the workspace is not built.</summary>
    private static string? StatisticsPage(Window window) =>
        (window.GetVisualDescendants().OfType<TabControl>().FirstOrDefault(t => t.Name == "StatsTabs")?.SelectedItem as TabItem)?.Header?.ToString();

    private static Dictionary<string, object?> Controls(Window window)
    {
        var found = new Dictionary<string, object?>();
        foreach (var tabs in window.GetVisualDescendants().OfType<TabControl>())
        {
            var prefix = tabs.Name switch { "WorkspaceTabs" => "workspace.", "ResultTabs" => "evidence.", "StatsTabs" => "statistics.", _ => null };
            if (prefix is null) continue;
            foreach (var item in tabs.GetVisualDescendants().OfType<TabItem>())
            {
                var header = item.Header?.ToString();
                if (string.IsNullOrEmpty(header)) continue;
                Add(found, prefix + header, item, window);
            }
        }
        foreach (var control in window.GetVisualDescendants().OfType<Control>())
        {
            if (string.IsNullOrEmpty(control.Name) || control is TabControl or TabItem) continue;
            Add(found, "control." + control.Name, control, window);
        }
        // the first row of the ion table, which is what a script clicks to prove a row answers
        var table = window.GetVisualDescendants().OfType<DataGrid>().FirstOrDefault(g => g.Name == "IonTable");
        var firstRow = table?.GetVisualDescendants().OfType<DataGridRow>().OrderBy(r => r.Bounds.Y).FirstOrDefault();
        if (firstRow is not null) Add(found, "control.IonTable.firstRow", firstRow, window);
        return found;
    }

    private static void Add(Dictionary<string, object?> found, string key, Control control, Window window)
    {
        if (!control.IsEffectivelyVisible || control.Bounds.Width <= 0 || control.Bounds.Height <= 0) return;
        var corner = control.TranslatePoint(new Point(0, 0), window);
        if (corner is null) return;
        found[key] = new Dictionary<string, object?>
        {
            ["x"] = Math.Round(corner.Value.X + control.Bounds.Width / 2, 1),
            ["y"] = Math.Round(corner.Value.Y + control.Bounds.Height / 2, 1),
            ["width"] = Math.Round(control.Bounds.Width, 1),
            ["height"] = Math.Round(control.Bounds.Height, 1),
        };
    }

    /// <summary>The number the command file is allowed to carry, parsed the invariant way.</summary>
    public static double Number(JsonElement element, string name, double fallback) =>
        element.TryGetProperty(name, out var p)
            ? p.ValueKind == JsonValueKind.Number ? p.GetDouble()
              : double.TryParse(p.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback
            : fallback;
}
