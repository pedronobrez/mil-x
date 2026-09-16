using System.Globalization;
using System.Text;
using Avalonia.Controls;
using MilX.Desktop.Charts;

namespace MilX.Desktop.Services;

/// <summary>One line of the report's tables.</summary>
public sealed record ReportRow(string Label, string Value);

/// <summary>A figure to put in the report, as the vector the chart draws.</summary>
public sealed record ReportFigure(string Title, string Svg);

/// <summary>Everything the report says, gathered by whoever knows it.</summary>
public sealed class ReportContent
{
    public string Project { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public IReadOnlyList<ReportRow> Counts { get; set; } = Array.Empty<ReportRow>();
    public IReadOnlyList<ReportRow> Library { get; set; } = Array.Empty<ReportRow>();
    public IReadOnlyList<(string File, string Class, string Type, string Peaks, string Detected)> Injections { get; set; }
        = Array.Empty<(string, string, string, string, string)>();
    public string Method { get; set; } = string.Empty;
    public IReadOnlyList<ReportFigure> Figures { get; set; } = Array.Empty<ReportFigure>();
    public IReadOnlyList<string> Log { get; set; } = Array.Empty<string>();
}

/// <summary>
/// The run, written down: what was processed, with which method and which library, what came out,
/// what the injections looked like, and the figures the reviewer had on screen.
///
/// It is one HTML file with the figures inlined as vectors — no images to lose, nothing to fetch —
/// which opens in any browser and is saved as a PDF from there. A processing run whose parameters
/// live only in the operator's memory is not reproducible, and the method file alone is not a
/// report: the counts and the quality of the injections are what a reader checks first.
/// </summary>
public static class RunReport
{
    public static string Build(ReportContent content)
    {
        var html = new StringBuilder();
        html.Append("""
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Run report</title>
<style>
  :root { --ink:#1a1d21; --muted:#5f6874; --faint:#8b93a0; --rule:#e3e5ea; --accent:#234b8c; --paper:#ffffff; }
  * { box-sizing:border-box; }
  body { margin:0; background:#f5f6f8; color:var(--ink); font:15px/1.6 "Inter","Helvetica Neue",Arial,sans-serif; }
  .sheet { max-width:900px; margin:0 auto; background:var(--paper); padding:44px 48px 64px; }
  h1 { font-size:30px; margin:0 0 6px; letter-spacing:-0.02em; }
  .sub { color:var(--muted); margin:0 0 28px; }
  h2 { font-size:18px; margin:34px 0 10px; padding-bottom:6px; border-bottom:2px solid var(--ink); letter-spacing:-0.01em; }
  table { border-collapse:collapse; width:100%; font-size:14px; }
  th { text-align:left; font-size:11px; letter-spacing:0.08em; text-transform:uppercase; color:var(--faint); padding:7px 10px; border-bottom:1px solid var(--rule); }
  td { padding:7px 10px; border-bottom:1px solid var(--rule); vertical-align:top; }
  td.n { font-variant-numeric:tabular-nums; }
  .pairs { display:grid; grid-template-columns:repeat(auto-fit,minmax(220px,1fr)); gap:1px; background:var(--rule); border:1px solid var(--rule); }
  .pair { background:var(--paper); padding:10px 12px; }
  .pair dt { font-size:11px; letter-spacing:0.08em; text-transform:uppercase; color:var(--faint); margin:0 0 2px; }
  .pair dd { margin:0; font-size:16px; font-variant-numeric:tabular-nums; }
  pre { background:#f7f8fa; border:1px solid var(--rule); padding:12px 14px; overflow-x:auto; font:12px/1.5 ui-monospace,Menlo,monospace; }
  figure { margin:0 0 18px; border:1px solid var(--rule); padding:12px; }
  figure svg { display:block; width:100%; height:auto; }
  figcaption { font-size:12px; color:var(--faint); margin-top:8px; }
  footer { margin-top:40px; border-top:1px solid var(--rule); padding-top:12px; font-size:12px; color:var(--faint); }
  @media print { body { background:var(--paper); } .sheet { max-width:none; padding:0; } h2 { break-after:avoid; } figure { break-inside:avoid; } }
</style></head><body><div class="sheet">
""");
        html.Append("<h1>").Append(Escape(content.Project)).Append("</h1>\n");
        html.Append("<p class=\"sub\">Run report · ").Append(DateTime.Now.ToString("d MMMM yyyy, HH:mm", CultureInfo.InvariantCulture))
            .Append(" · MIL-X ").Append(Escape(content.Version)).Append("</p>\n");

        if (content.Counts.Count > 0)
        {
            html.Append("<h2>What came out</h2>\n<div class=\"pairs\">\n");
            foreach (var row in content.Counts)
            {
                html.Append("<dl class=\"pair\"><dt>").Append(Escape(row.Label)).Append("</dt><dd>").Append(Escape(row.Value)).Append("</dd></dl>\n");
            }
            html.Append("</div>\n");
        }

        if (content.Library.Count > 0)
        {
            html.Append("<h2>The library</h2>\n<table><tbody>\n");
            foreach (var row in content.Library)
            {
                html.Append("<tr><td>").Append(Escape(row.Label)).Append("</td><td>").Append(Escape(row.Value)).Append("</td></tr>\n");
            }
            html.Append("</tbody></table>\n");
        }

        if (content.Injections.Count > 0)
        {
            html.Append("<h2>The injections</h2>\n<table><thead><tr><th>File</th><th>Class</th><th>Type</th><th>Peaks</th><th>Detected</th></tr></thead><tbody>\n");
            foreach (var (file, cls, type, peaks, detected) in content.Injections)
            {
                html.Append("<tr><td>").Append(Escape(file)).Append("</td><td>").Append(Escape(cls))
                    .Append("</td><td>").Append(Escape(type)).Append("</td><td class=\"n\">").Append(Escape(peaks))
                    .Append("</td><td class=\"n\">").Append(Escape(detected)).Append("</td></tr>\n");
            }
            html.Append("</tbody></table>\n");
        }

        if (content.Figures.Count > 0)
        {
            html.Append("<h2>Figures</h2>\n");
            foreach (var figure in content.Figures)
            {
                html.Append("<figure>").Append(figure.Svg)
                    .Append("<figcaption>").Append(Escape(figure.Title)).Append("</figcaption></figure>\n");
            }
        }

        if (!string.IsNullOrWhiteSpace(content.Method))
        {
            html.Append("<h2>The method</h2>\n<pre>").Append(Escape(content.Method)).Append("</pre>\n");
        }

        if (content.Log.Count > 0)
        {
            html.Append("<h2>The run log</h2>\n<pre>").Append(Escape(string.Join("\n", content.Log))).Append("</pre>\n");
        }

        html.Append("<footer>Written by MIL-X from ").Append(Escape(content.OutputFolder))
            .Append(". Print this page to save it as a PDF.</footer>\n</div></body></html>\n");
        return html.ToString();
    }

    /// <summary>The chart as a vector for the report: light, on white, whatever the window.</summary>
    public static ReportFigure? Figure(Control? chart, string title)
    {
        if (chart is null || chart.Bounds.Width < 40 || chart.Bounds.Height < 30) return null;
        try
        {
            var svg = ChartExport.Render(chart, new ChartExportOptions { Theme = "Light", Background = "White" }, c => ChartExport.ToSvg(c));
            return new ReportFigure(title, svg);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string Escape(string? text) => (text ?? string.Empty)
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
