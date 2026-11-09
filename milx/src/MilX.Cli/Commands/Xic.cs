using System.Globalization;
using System.Text.Json.Nodes;
using MilX.Pipeline.Results;

namespace MilX.Cli.Commands;

/// <summary>Extracted ion chromatograms: a summary per m/z by default, the trace on request, and a comparison against a reference ion.</summary>
internal static class Xic
{
    public static Document Run(Args args, RawStore store, CancellationToken ct)
    {
        var file = Common.OpenTarget(args, store, "xic", headersOnly: false, ct);
        var masses = Masses(args);
        var (source, indices) = Common.Source(args, file);
        var rt = args.Range("rt");
        var window = Common.InRange(file, indices, rt);
        var tol = args.Double("tol", 0.01);
        var unit = args.Get("unit", "Da");
        if (unit is not ("Da" or "da" or "ppm")) throw new CliError(ErrorCode.BadArguments, "--unit is Da or ppm");
        var smooth = args.Double("smooth", 0);
        var trace = args.Flag("trace");

        var doc = new Document("xic", Common.Input(file, args));
        doc.Root["source"] = source.ToString();
        doc.Root["scans_in_source"] = indices.Count;
        doc.Root["scans_in_window"] = window.Count;
        doc.Root["definitions"] = new JsonObject
        {
            ["tolerance"] = $"m/z ± {tol.ToString("R", CultureInfo.InvariantCulture)} {(unit == "ppm" ? "ppm" : "Da")}, points inside the window summed",
            ["smoothing"] = smooth > 0 ? $"Gaussian, sigma {smooth.ToString("R", CultureInfo.InvariantCulture)} scans" : "none",
            ["bounds"] = PeakSummary.BoundsRule,
            ["snr"] = PeakSummary.SnrMethod,
            ["area"] = "trapezoid between the bounds, intensity × minutes",
        };
        if (window.Count == 0 && args.Flag("require")) throw new CliError(ErrorCode.NothingFound, "no scans in the source and window");

        PeakSummary? reference = null;
        if (args.Get("reference") is { } refText)
        {
            var (refMz, refSource) = Reference(refText);
            var refIndices = Common.InRange(file, Common.Resolve(refSource, file), rt);
            var refTolDa = unit == "ppm" ? RawExplorer.PpmToDa(refMz, tol) : tol;
            var refTrace = Smooth(RawExplorer.Xic(file.Raw, refIndices, refMz, refTolDa).Points, smooth);
            reference = PeakSummary.Of(refTrace);
            var refJson = reference.ToJson();
            refJson["mz"] = Num.Mz(refMz);
            refJson["source"] = refSource.ToString();
            doc.Root["reference"] = refJson;
        }

        var results = new JsonArray();
        var rows = new List<JsonObject>();
        var anyFound = false;
        foreach (var mz in masses)
        {
            ct.ThrowIfCancellationRequested();
            var tolDa = unit == "ppm" ? RawExplorer.PpmToDa(mz, tol) : tol;
            var points = Smooth(RawExplorer.Xic(file.Raw, window, mz, tolDa).Points, smooth);
            var summary = PeakSummary.Of(points);
            anyFound |= summary.Found;
            var entry = new JsonObject { ["mz"] = Num.Mz(mz), ["tolerance_da"] = Num.TheoryMz(tolDa) };
            foreach (var (k, node) in summary.ToJson()) entry[k] = node?.DeepClone();
            if (reference is not null)
            {
                var vs = new JsonObject();
                if (summary.Found && reference.Found)
                {
                    var r = reference.CorrelationWith(points);
                    vs["pearson_r"] = r is { } rr ? Num.Ratio(rr) : null;
                    vs["apex_delta_rt"] = Num.Rt(summary.ApexRt - reference.ApexRt);
                    vs["height_ratio"] = Num.Ratio(summary.ApexHeight / reference.ApexHeight);
                }
                else
                {
                    vs["pearson_r"] = null; vs["apex_delta_rt"] = null; vs["height_ratio"] = null;
                }
                entry["vs_reference"] = vs;
            }
            if (trace)
            {
                var pts = new JsonArray();
                foreach (var p in points) pts.Add(new JsonArray(Num.Rt(p.Rt), Num.Intensity(p.Intensity) ?? 0));
                entry["trace"] = pts;
            }
            results.Add(entry);
            var row = new JsonObject
            {
                ["mz"] = Num.Mz(mz), ["found"] = summary.Found,
                ["apex_rt"] = summary.Found ? Num.Rt(summary.ApexRt) : null, ["apex_height"] = summary.Found ? Num.Intensity(summary.ApexHeight) : null,
                ["area"] = summary.Found ? Num.Intensity(Math.Round(summary.Area, 6)) : null, ["fwhm"] = summary.Fwhm is { } f ? Num.Rt(f) : null,
                ["points_across_peak"] = summary.Found ? summary.Points : null, ["snr"] = summary.Snr is { } s ? Num.Ratio(s) : null,
            };
            if (reference is not null && entry["vs_reference"] is JsonObject v)
            {
                row["pearson_r"] = v["pearson_r"]?.DeepClone(); row["apex_delta_rt"] = v["apex_delta_rt"]?.DeepClone(); row["height_ratio"] = v["height_ratio"]?.DeepClone();
            }
            rows.Add(row);
        }
        if (!anyFound && args.Flag("require")) throw new CliError(ErrorCode.NothingFound, "no peak found for any of the masses");
        doc.Root["xics"] = results;
        doc.Rows = trace ? TraceRows(masses, results) : rows;
        return doc;
    }

    private static List<JsonObject> TraceRows(IReadOnlyList<double> masses, JsonArray results)
    {
        // TSV of a trace: one row per point, with the m/z it belongs to
        var rows = new List<JsonObject>();
        for (var i = 0; i < results.Count; i++)
        {
            if (results[i]!["trace"] is not JsonArray pts) continue;
            foreach (var p in pts) rows.Add(new JsonObject { ["mz"] = Num.Mz(masses[i]), ["rt"] = p![0]!.GetValue<double>(), ["intensity"] = p[1]!.GetValue<double>() });
        }
        return rows;
    }

    public static IReadOnlyList<double> Masses(Args args)
    {
        var text = string.Join(" ", args.All("mz"));
        if (text.Length == 0 && args.Positional.Count > 1) text = string.Join(" ", args.Positional.Skip(1));
        var masses = XicSources.ParseMzList(text, out var rejected);
        if (rejected.Count > 0) throw new CliError(ErrorCode.BadArguments, $"--mz: not a mass: {string.Join(", ", rejected)}");
        if (masses.Count == 0) throw new CliError(ErrorCode.BadArguments, "--mz needs one or more masses, comma or space separated");
        return masses;
    }

    public static IReadOnlyList<ChromatogramPoint> Smooth(IReadOnlyList<ChromatogramPoint> points, double sigma) => sigma > 0 ? ChromatogramMath.Smooth(points, sigma) : points;

    /// <summary>"MZ@SOURCE": the reference ion and where it is read from.</summary>
    public static (double Mz, XicSource Source) Reference(string text)
    {
        var at = text.IndexOf('@');
        var mzText = at < 0 ? text : text[..at];
        if (!double.TryParse(mzText, NumberStyles.Float, CultureInfo.InvariantCulture, out var mz) || mz <= 0) throw new CliError(ErrorCode.BadArguments, $"--reference needs MZ@SOURCE, not '{text}'");
        try { return (mz, at < 0 ? XicSource.Ms1 : XicSource.Parse(text[(at + 1)..])); }
        catch (FormatException e) { throw new CliError(ErrorCode.BadArguments, "--reference: " + e.Message); }
    }
}
