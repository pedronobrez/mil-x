using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
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
/// It also reports where a few controls are inside the window, because a script that clicks at a
/// coordinate worked out from a screenshot breaks the moment the window moves or the layout shifts.
/// Asking the window where its own tabs are is both robust and honest.
/// </summary>
public sealed class UiProbe
{
    private readonly string _path;
    private bool _scheduled;

    private UiProbe(string path) => _path = path;

    /// <summary>The probe, or null when the environment did not ask for one.</summary>
    public static UiProbe? FromEnvironment() =>
        Environment.GetEnvironmentVariable("OPENDIAL_UI_PROBE") is { Length: > 0 } path ? new UiProbe(path) : null;

    /// <summary>
    /// Writes after the current burst of changes rather than on each one: loading a project moves a
    /// dozen properties, and the reader only ever wants the state they settled into.
    /// </summary>
    public void Schedule(Window window, MainWindowViewModel vm)
    {
        if (_scheduled) return;
        _scheduled = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _scheduled = false;
            Write(window, vm);
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    public void Write(Window window, MainWindowViewModel vm)
    {
        try
        {
            var snapshot = new Dictionary<string, object?>
            {
                ["writtenAt"] = DateTimeOffset.Now.ToString("O"),
                ["title"] = vm.Title,
                ["status"] = vm.Status,
                ["workspace"] = vm.SelectedWorkspace,
                ["workspaceName"] = WorkspaceName(vm.SelectedWorkspace),
                ["projectName"] = vm.ProjectName,
                ["hasProject"] = vm.HasProject,
                ["hasResults"] = vm.HasResults,
                ["ionRows"] = vm.Analytics.IonRows.Count,
                ["samples"] = vm.Samples.Samples.Count,
                ["statistics"] = new Dictionary<string, object?>
                {
                    ["hasResults"] = vm.Statistics.HasResults,
                    ["hasDiscriminant"] = vm.Statistics.HasPls,
                    ["hasOrthogonal"] = vm.Statistics.HasOpls,
                    ["hasCorrection"] = vm.Statistics.HasCorrection,
                },
                ["client"] = new Dictionary<string, object?>
                {
                    ["width"] = Math.Round(window.ClientSize.Width, 1),
                    ["height"] = Math.Round(window.ClientSize.Height, 1),
                    ["scaling"] = window.RenderScaling,
                },
                ["controls"] = Controls(window),
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
    /// Where the workspace tabs are, relative to the top left of the window's own content and in
    /// the units layout is done in.
    ///
    /// Deliberately not in screen coordinates. Turning these into a place to click means knowing
    /// where the window frame is and how tall its title bar is, and both of those are the window
    /// manager's business, not the application's: the one platform-specific conversion belongs in
    /// the script that already has to know about the platform. The client size is reported with them
    /// so that conversion has everything it needs.
    /// </summary>
    private static Dictionary<string, object?> Controls(Window window)
    {
        var found = new Dictionary<string, object?>();
        var tabs = window.GetVisualDescendants().OfType<TabControl>().FirstOrDefault(t => t.Name == "WorkspaceTabs");
        if (tabs is null) return found;

        foreach (var item in tabs.GetVisualDescendants().OfType<TabItem>())
        {
            var header = item.Header?.ToString();
            if (string.IsNullOrEmpty(header) || item.Bounds.Width <= 0) continue;
            var corner = item.TranslatePoint(new Point(0, 0), window);
            if (corner is null) continue;
            found["workspace." + header] = new Dictionary<string, object?>
            {
                ["x"] = Math.Round(corner.Value.X + item.Bounds.Width / 2, 1),
                ["y"] = Math.Round(corner.Value.Y + item.Bounds.Height / 2, 1),
                ["width"] = Math.Round(item.Bounds.Width, 1),
                ["height"] = Math.Round(item.Bounds.Height, 1),
            };
        }
        return found;
    }
}
