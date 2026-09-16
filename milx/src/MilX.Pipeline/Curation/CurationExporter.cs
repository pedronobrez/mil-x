using System.Globalization;
using System.Text;
using MilX.Pipeline.Results;

namespace MilX.Pipeline.Curation;

/// <summary>Which per-sample number an exported table carries.</summary>
public enum ExportValue
{
    Height,
    Area,
}

public sealed record CurationExportOptions(
    ExportValue Value = ExportValue.Height,
    bool IncludeSampleColumns = true,
    char Separator = '\t');

/// <summary>
/// Writes the reviewed alignment table.
///
/// MS-DIAL's own alignment export has no tag column: it folds the tags into the free-text comment
/// and loses their structure, and it cannot express "reviewed" at all. This writes them as columns
/// of their own, next to the identity and the per-sample values, so a reviewed result can go
/// straight into a spreadsheet or a statistics script without the reviewer's decisions being lost.
/// </summary>
public static class CurationExporter
{
    public static int Write(TextWriter writer, IReadOnlyList<AlignmentSpotRow> spots, IReadOnlyList<SampleInfo> samples, CurationStore? store, CurationExportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(spots);
        options ??= new CurationExportOptions();
        var sep = options.Separator;

        var header = new List<string>
        {
            "Alignment ID", "Average RT(min)", "Average m/z", "Metabolite name", "Annotation level",
            "Adduct", "Ontology", "Formula", "INCHIKEY", "Isotope", "Fill %", "MS/MS assigned",
            "S/N average", "Total score", "Representative file",
            "Reviewed", "Tags", "Comment", "Manually annotated", "Manually quantified",
        };
        if (options.IncludeSampleColumns)
        {
            header.AddRange(samples.Select(s => s.FileName));
        }
        writer.Write(string.Join(sep, header.Select(h => Escape(h, sep))));
        writer.Write('\n');

        // a second header line naming the class of each sample, as MS-DIAL's matrices do
        if (options.IncludeSampleColumns && samples.Count > 0)
        {
            var classes = Enumerable.Repeat(string.Empty, header.Count - samples.Count).ToList();
            classes[0] = "Class";
            classes.AddRange(samples.Select(s => s.Class));
            writer.Write(string.Join(sep, classes.Select(h => Escape(h, sep))));
            writer.Write('\n');
        }

        var written = 0;
        foreach (var spot in spots)
        {
            var curation = store?.Get(spot.Id);
            var tags = store is null ? Array.Empty<PeakSpotTagKind>() : store.TagsOf(spot.Id).OrderBy(t => (int)t).ToArray();
            var name = curation is { ManualName.Length: > 0 } ? curation.ManualName : spot.Name;
            var cells = new List<string>
            {
                spot.Id.ToString(CultureInfo.InvariantCulture),
                F(spot.Rt, "F3"),
                F(spot.Mz, "F5"),
                name,
                Level(name),
                spot.Adduct,
                spot.Ontology,
                spot.Formula,
                spot.InChIKey,
                spot.IsotopeWeight switch { < 0 => string.Empty, 0 => "M", var n => "M+" + n.ToString(CultureInfo.InvariantCulture) },
                F(spot.FillPercent, "F1"),
                spot.MsmsAssigned ? "True" : "False",
                F(spot.SignalToNoiseAverage, "F1"),
                F(spot.Score, "F3"),
                samples.FirstOrDefault(s => s.FileId == spot.RepresentativeFileId)?.FileName ?? string.Empty,
                (curation?.Reviewed ?? false) || tags.Length > 0 ? "True" : "False",
                string.Join("; ", tags.Select(t => t.Label())),
                curation?.Comment ?? spot.Comment,
                spot.IsManuallyAnnotated || (curation is { ManualName.Length: > 0 }) ? "True" : "False",
                spot.IsManuallyQuantified ? "True" : "False",
            };
            if (options.IncludeSampleColumns)
            {
                foreach (var sample in samples)
                {
                    var peak = spot.SamplePeaks.FirstOrDefault(p => p.FileId == sample.FileId);
                    var value = peak is null ? double.NaN : options.Value == ExportValue.Area ? peak.Area : peak.Height;
                    cells.Add(double.IsNaN(value) ? string.Empty : value.ToString("F0", CultureInfo.InvariantCulture));
                }
            }
            writer.Write(string.Join(sep, cells.Select(c => Escape(c, sep))));
            writer.Write('\n');
            written++;
        }
        return written;
    }

    public static async Task<int> WriteFileAsync(string path, IReadOnlyList<AlignmentSpotRow> spots, IReadOnlyList<SampleInfo> samples, CurationStore? store, CurationExportOptions? options = null)
    {
        using var writer = new StringWriter();
        var count = Write(writer, spots, samples, store, options);
        await File.WriteAllTextAsync(path, writer.ToString(), new UTF8Encoding(false)).ConfigureAwait(false);
        return count;
    }

    private static string F(double value, string format) =>
        double.IsNaN(value) ? string.Empty : value.ToString(format, CultureInfo.InvariantCulture);

    private static string Level(string name) =>
        name.StartsWith("low score:", StringComparison.OrdinalIgnoreCase) ? "suggested"
        : name.StartsWith("no MS2:", StringComparison.OrdinalIgnoreCase) ? "m/z only"
        : string.IsNullOrEmpty(name) || name.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase) || name.StartsWith("w/o", StringComparison.OrdinalIgnoreCase) ? string.Empty
        : "confident";

    private static string Escape(string value, char separator)
    {
        value ??= string.Empty;
        if (value.IndexOf(separator) < 0 && value.IndexOf('"') < 0 && value.IndexOf('\n') < 0) return value;
        return '"' + value.Replace("\"", "\"\"") + '"';
    }
}
