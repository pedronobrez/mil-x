using System.Text.Json.Nodes;

namespace MilX.Cli.Commands;

/// <summary>Writes mzML that keeps per-scan precursors; the path of the file written is the answer.</summary>
internal static class Convert
{
    public static Document Run(Args args, RawStore store, CancellationToken ct)
    {
        var centroid = args.Get("centroid", "vendor").ToLowerInvariant();
        if (centroid is not ("vendor" or "local" or "none")) throw new CliError(ErrorCode.BadArguments, "--centroid is vendor, local or none");
        var before = Environment.GetEnvironmentVariable("MILX_WIFF_CENTROID");
        Environment.SetEnvironmentVariable("MILX_WIFF_CENTROID", centroid switch { "local" => "msdial", "none" => "0", _ => "sciex" });
        OpenFile file;
        try { file = Common.OpenTarget(args, store, "convert", headersOnly: false, ct); }
        finally { Environment.SetEnvironmentVariable("MILX_WIFF_CENTROID", before); }
        var outPath = args.Get("out") ?? args.Get("o");
        if (string.IsNullOrWhiteSpace(outPath))
        {
            var stem = Path.GetFileNameWithoutExtension(file.Path) + (file.Sample > 0 ? $".s{file.Sample + 1}" : string.Empty);
            outPath = Path.Combine(Path.GetDirectoryName(file.Path)!, stem + ".mzML");
        }
        outPath = Path.GetFullPath(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        MzmlWriter.Write(outPath, file.Raw, Path.GetFileNameWithoutExtension(outPath));
        var doc = new Document("convert", Common.Input(file, args));
        doc.Root["out"] = outPath;
        doc.Root["spectra"] = file.Spectra;
        doc.Root["ms2_with_precursor"] = file.Raw.SpectrumList.Count(s => s.MsLevel > 1 && MilX.Pipeline.Results.XicSources.ParentMz(s) > 0);
        doc.Root["centroid"] = centroid;
        doc.Root["bytes"] = new FileInfo(outPath).Length;
        doc.Root["write_seconds"] = Math.Round(watch.Elapsed.TotalSeconds, 2);
        doc.Files.Add(outPath);
        return doc;
    }
}
