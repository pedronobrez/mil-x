using System.Globalization;
using System.Text.Json.Nodes;
using MilX.Pipeline.Results;

namespace MilX.Cli.Commands;

/// <summary>The scan table of a source, filtered by time, precursor and MS level; --count gives counts only.</summary>
internal static class Scans
{
    public static Document Run(Args args, RawStore store, CancellationToken ct)
    {
        var file = Common.OpenTarget(args, store, "scans", headersOnly: !args.Has("source") || args.Get("source") is "ms1" or "ms2-all" || args.Get("source")!.StartsWith("gated", StringComparison.Ordinal) || args.Get("source")!.StartsWith("channel", StringComparison.Ordinal), ct);
        var (source, indices) = Common.Source(args, file);
        var rt = args.Range("rt");
        indices = Common.InRange(file, indices, rt);
        var level = args.Get("ms-level") is { } lv ? int.Parse(lv, CultureInfo.InvariantCulture) : (int?)null;
        var parent = ParentFilter(args.Get("parent"));
        var raw = file.Raw;
        var kept = new List<int>();
        foreach (var i in indices)
        {
            var s = raw.SpectrumList[i];
            if (level is { } l && Math.Max(1, s.MsLevel) != l) continue;
            if (parent is { } p)
            {
                var mz = XicSources.ParentMz(s);
                if (mz <= 0 || Math.Abs(mz - p.Mz) > p.Tol) continue;
            }
            kept.Add(i);
        }
        var doc = new Document("scans", Common.Input(file, args));
        doc.Root["source"] = source.ToString();
        doc.Root["read_from"] = file.ReadFrom;
        doc.Root["count"] = kept.Count;
        var counts = new JsonObject();
        foreach (var g in kept.GroupBy(i => Math.Max(1, raw.SpectrumList[i].MsLevel)).OrderBy(g => g.Key)) counts["ms" + g.Key] = g.Count();
        doc.Root["count_by_level"] = counts;
        if (kept.Count == 0 && args.Flag("require")) throw new CliError(ErrorCode.NothingFound, "no scan matches");
        if (args.Flag("count"))
        {
            var precursors = XicSources.Precursors(raw, kept);
            doc.Root["distinct_precursors"] = precursors.Count;
            doc.Rows = new List<JsonObject> { new() { ["source"] = source.ToString(), ["count"] = kept.Count, ["distinct_precursors"] = precursors.Count } };
            return doc;
        }
        var rows = new List<JsonObject>(kept.Count);
        var positions = indices.Select((idx, pos) => (idx, pos)).ToDictionary(t => t.idx, t => t.pos);
        foreach (var i in kept) rows.Add(Common.ScanRow(raw, i, positions[i]));
        doc.Root["scans"] = new JsonArray(rows.Select(r => (JsonNode)r.DeepClone()).ToArray());
        doc.Rows = rows;
        return doc;
    }

    public static (double Mz, double Tol)? ParentFilter(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var parts = text.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is 1 or 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var mz))
        {
            var tol = XicSources.DefaultGateTolerance;
            if (parts.Length == 2 && !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out tol)) throw new CliError(ErrorCode.BadArguments, $"--parent {text}: the tolerance must be a number of daltons");
            return (mz, tol);
        }
        throw new CliError(ErrorCode.BadArguments, $"--parent needs MZ or MZ:TOL, not '{text}'");
    }
}
