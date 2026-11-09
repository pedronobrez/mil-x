using System.Globalization;
using System.Text.Json.Nodes;
using MilX.Pipeline.Results;
using MilX.Pipeline.Vendor;

namespace MilX.Cli.Commands;

/// <summary>The same XIC summary over many files: one row per (file, sample, m/z); how blanks and distribution are checked.</summary>
internal static class BatchXic
{
    public static Document Run(Args args, RawStore store, Action<string> log, CancellationToken ct)
    {
        var paths = args.Positional.Concat(args.All("file")).Concat(args.All("files")).ToList();
        if (paths.Count == 0) throw new CliError(ErrorCode.BadArguments, "batch-xic needs files or folders: milx batch-xic PATHS… --mz LIST");
        var masses = Xic.Masses(args);
        var tol = args.Double("tol", 0.01);
        var unit = args.Get("unit", "Da");
        var smooth = args.Double("smooth", 0);
        var rt = args.Range("rt");
        if (args.Get("rt-window") is { } w) rt = Window(w);
        var report = FolderCheck.Check(paths);
        var doc = new Document("batch-xic", new JsonObject { ["paths"] = new JsonArray(paths.Select(p => (JsonNode)Path.GetFullPath(p)).ToArray()), ["mz"] = new JsonArray(masses.Select(m => (JsonNode)Num.Mz(m)).ToArray()), ["tol"] = tol, ["unit"] = unit, ["source"] = args.Get("source", "ms1") });
        if (rt is { } r) doc.Root["rt"] = new JsonArray(Num.Rt(r.Lo), Num.Rt(r.Hi));
        doc.Root["skipped"] = new JsonArray(report.Findings.Select(f => (JsonNode)new JsonObject { ["path"] = f.Path, ["kind"] = Common.KindName(f.Kind), ["finding"] = f.Finding, ["action"] = f.Action }).ToArray());
        doc.Root["definitions"] = new JsonObject { ["bounds"] = PeakSummary.BoundsRule, ["snr"] = PeakSummary.SnrMethod };
        var rows = new List<JsonObject>();
        var anyFound = false;
        var filesRead = 0;
        foreach (var path in report.Paths)
        {
            ct.ThrowIfCancellationRequested();
            var samples = RawStore.SampleNames(path);
            for (var sample = 0; sample < samples.Count; sample++)
            {
                OpenFile file;
                try { file = store.Open(path, sample, false, ct); }
                catch (CliError e)
                {
                    ((JsonArray)doc.Root["skipped"]!).Add(new JsonObject { ["path"] = path, ["sample"] = sample, ["kind"] = "unreadable", ["finding"] = e.Message });
                    continue;
                }
                if (sample == 0) filesRead++;
                var (source, indices) = Common.Source(args, file);
                var window = Common.InRange(file, indices, rt);
                foreach (var mz in masses)
                {
                    var tolDa = unit == "ppm" ? RawExplorer.PpmToDa(mz, tol) : tol;
                    var summary = PeakSummary.Of(Xic.Smooth(RawExplorer.Xic(file.Raw, window, mz, tolDa).Points, smooth));
                    anyFound |= summary.Found;
                    rows.Add(new JsonObject
                    {
                        ["file"] = file.Path, ["sample"] = sample, ["sample_name"] = file.SampleName, ["source"] = source.ToString(), ["mz"] = Num.Mz(mz),
                        ["found"] = summary.Found,
                        ["apex_rt"] = summary.Found ? Num.Rt(summary.ApexRt) : null,
                        ["apex_height"] = summary.Found ? Num.Intensity(summary.ApexHeight) : null,
                        ["area"] = summary.Found ? Num.Intensity(Math.Round(summary.Area, 6)) : null,
                        ["fwhm"] = summary.Fwhm is { } f ? Num.Rt(f) : null,
                        ["points_across_peak"] = summary.Found ? summary.Points : null,
                        ["snr"] = summary.Snr is { } s ? Num.Ratio(s) : null,
                    });
                }
                log($"[batch-xic] {Path.GetFileName(path)} sample {sample}: {masses.Count} mass(es), {window.Count} scans");
            }
        }
        if (!anyFound && args.Flag("require")) throw new CliError(ErrorCode.NothingFound, "no peak found in any file");
        doc.Root["files_read"] = filesRead;
        doc.Root["rows"] = new JsonArray(rows.Select(r => (JsonNode)r.DeepClone()).ToArray());
        doc.Rows = rows;
        return doc;
    }

    /// <summary>"C±W" (or "C+-W", "C:W") as a window around a centre.</summary>
    public static (double Lo, double Hi) Window(string text)
    {
        var t = text.Replace("+-", "±").Replace("+/-", "±");
        var parts = t.Split(new[] { '±', ':' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var c) && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var w) && w >= 0)
            return (c - w, c + w);
        throw new CliError(ErrorCode.BadArguments, $"--rt-window needs C±W (minutes), not '{text}'");
    }
}
