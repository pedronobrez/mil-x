using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace MilX.Desktop.Charts;

/// <summary>
/// Taking one chart out of the application: ask how it should look, ask where it goes, write it.
///
/// Every chart in every workspace uses this — the chromatogram and the spectrum in Explorer, the
/// peak panels and the mirror in Analytics, the figures in Statistics — so a figure is made the
/// same way wherever it is looked at, and a script can hand over the path and the settings instead
/// of the two dialogs.
/// </summary>
public static class ChartExportFlow
{
    /// <summary>
    /// Runs the export. <paramref name="options"/> skips the settings dialog and
    /// <paramref name="path"/> skips the save panel, which is what the probe's command hands over.
    /// Answers with the file written, or null when either dialog was cancelled.
    /// </summary>
    public static async Task<string?> RunAsync(Control chart, string? heading, ChartExportOptions? options = null, string? path = null)
    {
        ArgumentNullException.ThrowIfNull(chart);
        var owner = TopLevel.GetTopLevel(chart);
        if (options is null)
        {
            if (owner is not Window window) return null;
            var dialog = new Views.ChartExportWindow();
            dialog.Prepare(chart, heading);
            options = await dialog.ShowDialog<ChartExportOptions?>(window);
            if (options is null) return null;
        }
        if (path is null)
        {
            if (owner is null) return null;
            var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save the figure",
                SuggestedFileName = SuggestedName(heading, options.Format),
                DefaultExtension = options.Format,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType(options.Format.ToUpperInvariant()) { Patterns = new[] { "*." + options.Format } },
                },
            });
            path = file?.TryGetLocalPath();
        }
        if (string.IsNullOrEmpty(path)) return null;
        return ChartExport.Save(chart, path, options);
    }

    /// <summary>The file name a panel suggests: its own heading, as a file name.</summary>
    public static string SuggestedName(string? heading, string format)
    {
        var name = string.IsNullOrWhiteSpace(heading) ? "chart" : heading;
        name = new string(name.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
        while (name.Contains("--", StringComparison.Ordinal)) name = name.Replace("--", "-", StringComparison.Ordinal);
        name = name.Trim('-');
        return (name.Length == 0 ? "chart" : name) + "." + format;
    }

    /// <summary>
    /// The menu that exports one chart. Every chart control builds one, so the command is there
    /// wherever a chart is, without a button taking room from a panel that has none to give.
    /// </summary>
    public static MenuFlyout BuildMenu(Control chart, Func<string?> heading)
    {
        var item = new MenuItem { Header = "Export as a picture…" };
        item.Click += async (_, _) => await RunAsync(chart, heading());
        return new MenuFlyout { ItemsSource = new[] { item } };
    }

    /// <summary>The same menu, opened by the system's own context gesture.</summary>
    public static void AttachMenu(Control chart, Func<string?> heading) =>
        chart.ContextFlyout = BuildMenu(chart, heading);
}
