using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using CompMs.Common.Algorithm.PeakPick;
using CompMs.Common.DataObj;
using CompMs.Common.Enum;
using MilX.Pipeline.Results;

namespace MilX.Cli.Commands;

/// <summary>One spectrum as a peak list: a scan of a source, or the average over a time range, with backgrounds subtracted.</summary>
internal static class Spectrum
{
    public static Document Run(Args args, RawStore store, CancellationToken ct)
    {
        var centroid = args.Get("centroid", "vendor").ToLowerInvariant();
        if (centroid is not ("vendor" or "local" or "none")) throw new CliError(ErrorCode.BadArguments, "--centroid is vendor, local or none");
        using var _ = CentroidMode(centroid);
        var file = Common.OpenTarget(args, store, "spectrum", headersOnly: false, ct);
        var (source, indices) = Common.Source(args, file);
        var raw = file.Raw;
        var doc = new Document("spectrum", Common.Input(file, args));
        doc.Root["source"] = source.ToString();
        doc.Root["centroid"] = centroid;

        MsSpectrum spectrum;
        var used = new List<int>();
        if (args.Get("scan") is { } scanText)
        {
            if (!int.TryParse(scanText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < 0) throw new CliError(ErrorCode.BadArguments, "--scan needs an index within the source");
            if (n >= indices.Count) throw new CliError(ErrorCode.NothingFound, $"the source has {indices.Count} scans; --scan {n} is past the end");
            used.Add(indices[n]);
            spectrum = RawExplorer.Spectrum(raw, indices[n]);
            if (centroid == "local") spectrum = Centroided(spectrum, raw.SpectrumList[indices[n]]);
        }
        else if (args.Range("rt") is { } rt)
        {
            used.AddRange(Common.InRange(file, indices, rt));
            if (used.Count == 0) throw new CliError(ErrorCode.NothingFound, $"no scan of {source} between {rt.Lo} and {rt.Hi} min");
            spectrum = RawExplorer.AverageSpectrum(raw, indices, rt.Lo, rt.Hi);
            doc.Root["averaging"] = "mean over the scans in the window, peaks within 0.01 Da merged at the intensity-weighted m/z";
        }
        else throw new CliError(ErrorCode.BadArguments, "spectrum needs --scan N or --rt A:B");

        var peaks = spectrum.Peaks.Select(p => (p.Mz, p.Intensity)).ToList();
        var subtracted = new JsonArray();
        foreach (var range in args.All("subtract"))
        {
            var parts = range.Split(':');
            if (parts.Length != 2 || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var a) || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var b))
                throw new CliError(ErrorCode.BadArguments, $"--subtract needs A:B, not '{range}'");
            var background = RawExplorer.AverageSpectrum(raw, indices, a, b);
            peaks = Subtract(peaks, background.Peaks);
            subtracted.Add(new JsonObject { ["rt"] = new JsonArray(Num.Rt(Math.Min(a, b)), Num.Rt(Math.Max(a, b))), ["scans"] = Common.InRange(file, indices, (Math.Min(a, b), Math.Max(a, b))).Count });
        }
        doc.Root["subtracted"] = subtracted;
        doc.Root["subtraction"] = subtracted.Count > 0 ? "each background averaged like the spectrum; its peak within 0.01 Da taken off, negatives clipped to 0" : null;

        var minRel = args.Double("min-rel", 0);
        var top = args.Int("top", 0);
        var max = peaks.Count == 0 ? 0 : peaks.Max(p => p.Intensity);
        var kept = peaks.Where(p => p.Intensity > 0 && (minRel <= 0 || max <= 0 || p.Intensity / max >= minRel)).OrderBy(p => p.Mz).ToList();
        if (top > 0) kept = kept.OrderByDescending(p => p.Intensity).Take(top).OrderBy(p => p.Mz).ToList();
        if (kept.Count == 0 && args.Flag("require")) throw new CliError(ErrorCode.NothingFound, "the spectrum is empty");

        var scans = new JsonArray();
        var parents = new HashSet<double>();
        foreach (var i in used)
        {
            var s = raw.SpectrumList[i];
            var parent = XicSources.ParentMz(s);
            if (parent > 0) parents.Add(Num.Mz(parent));
            scans.Add(new JsonObject { ["index"] = i, ["rt"] = Num.Rt(RawExplorer.RtMinutes(s)), ["ms_level"] = Math.Max(1, s.MsLevel), ["parent_mz"] = parent > 0 ? Num.Mz(parent) : null, ["channel"] = s.ExperimentID });
        }
        doc.Root["scans_used"] = scans;
        doc.Root["parent_mz"] = parents.Count == 1 ? parents.First() : parents.Count == 0 ? null : new JsonArray(parents.OrderBy(p => p).Select(p => (JsonNode)p).ToArray());
        doc.Root["base_peak"] = max > 0 ? Num.Intensity(max) : null;
        doc.Root["peak_count"] = kept.Count;
        var list = new JsonArray();
        var rows = new List<JsonObject>();
        foreach (var (mz, intensity) in kept)
        {
            var row = new JsonObject { ["mz"] = Num.Mz(mz), ["intensity"] = Num.Intensity(intensity), ["relative"] = max > 0 ? Num.Ratio(100.0 * intensity / max) : 0 };
            list.Add(row);
            rows.Add((JsonObject)row.DeepClone());
        }
        doc.Root["peaks"] = list;
        doc.Rows = rows;

        if (args.Get("msp") is { } msp)
        {
            var name = args.Get("name") ?? $"{Path.GetFileNameWithoutExtension(file.Path)} {source} " + (args.Get("scan") is { } sc ? "scan " + sc : "RT " + args.Get("rt"));
            WriteMsp(msp, name, parents.Count == 1 ? parents.First() : null, kept, Common.Polarity(raw, used));
            doc.Root["msp"] = Path.GetFullPath(msp);
            doc.Files.Add(Path.GetFullPath(msp));
        }
        return doc;
    }

    /// <summary>Maps --centroid onto MILX_WIFF_CENTROID for the duration of the read: vendor = sciex, local = msdial, none = 0.</summary>
    private static IDisposable CentroidMode(string centroid)
    {
        var before = Environment.GetEnvironmentVariable("MILX_WIFF_CENTROID");
        Environment.SetEnvironmentVariable("MILX_WIFF_CENTROID", centroid switch { "local" => "msdial", "none" => "0", _ => "sciex" });
        return new Restore(() => Environment.SetEnvironmentVariable("MILX_WIFF_CENTROID", before));
    }

    private sealed class Restore : IDisposable { private readonly Action _a; public Restore(Action a) => _a = a; public void Dispose() => _a(); }

    /// <summary>MS-DIAL's local-maximum centroiding applied to a profile spectrum read as it is (an mzML in profile mode).</summary>
    private static MsSpectrum Centroided(MsSpectrum spectrum, RawSpectrum s)
    {
        if (s.SpectrumRepresentation == SpectrumRepresentation.Centroid) return spectrum;
        var peaks = spectrum.Peaks.Select(p => new RawPeakElement { Mz = p.Mz, Intensity = p.Intensity }).ToArray();
        var centroids = SpectralCentroiding.CentroidByLocalMaximumMethod(peaks, absThreshold: 0);
        return new MsSpectrum(spectrum.Label, spectrum.PrecursorMz, centroids.Select(p => new SpectrumPeakPoint(p.Mz, p.Intensity)).ToList());
    }

    private static List<(double Mz, double Intensity)> Subtract(List<(double Mz, double Intensity)> peaks, IReadOnlyList<SpectrumPeakPoint> background)
    {
        var bg = background.OrderBy(p => p.Mz).ToList();
        var result = new List<(double, double)>(peaks.Count);
        foreach (var (mz, intensity) in peaks)
        {
            var lo = mz - 0.01;
            var hi = mz + 0.01;
            double taken = 0;
            foreach (var b in bg)
            {
                if (b.Mz < lo) continue;
                if (b.Mz > hi) break;
                taken += b.Intensity;
            }
            result.Add((mz, Math.Max(0, intensity - taken)));
        }
        return result;
    }

    private static void WriteMsp(string path, string name, double? precursor, List<(double Mz, double Intensity)> peaks, string polarity)
    {
        var sb = new StringBuilder();
        sb.Append("NAME: ").Append(name).Append('\n');
        if (precursor is { } p) sb.Append("PRECURSORMZ: ").Append(p.ToString("F4", CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("IONMODE: ").Append(polarity == "negative" ? "Negative" : "Positive").Append('\n');
        sb.Append("COMMENT: written by milx ").Append(Cli.Version).Append('\n');
        sb.Append("Num Peaks: ").Append(peaks.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (var (mz, intensity) in peaks) sb.Append(mz.ToString("F4", CultureInfo.InvariantCulture)).Append('\t').Append(intensity.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        sb.Append('\n');
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }
}
