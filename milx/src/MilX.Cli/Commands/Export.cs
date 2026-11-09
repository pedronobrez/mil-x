using System.Text.Json.Nodes;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using MilX.Interop.OpenQuant;
using MilX.Pipeline.Curation;
using MilX.Pipeline.Results;

namespace MilX.Cli.Commands;

/// <summary>The review exports without the window: the reviewed table, the height/area matrix, the MIL-Q component list.</summary>
internal static class Export
{
    public static async Task<Document> RunAsync(Args args, CancellationToken ct)
    {
        var project = args.Get("project") ?? args.Positional.FirstOrDefault() ?? throw new CliError(ErrorCode.BadArguments, "export needs --project P (a .milx/.mdproject file or a results folder)");
        project = Path.GetFullPath(project);
        if (!File.Exists(project) && !Directory.Exists(project)) throw new CliError(ErrorCode.Unreadable, $"{project} is not there");
        var kinds = new[] { "reviewed", "matrix", "milq" }.Where(args.Has).ToList();
        if (kinds.Count == 0) throw new CliError(ErrorCode.BadArguments, "export needs one of --reviewed OUT, --matrix OUT, --milq OUT");

        OpenedProject opened;
        try { opened = await ProjectOpener.OpenAsync(project, ct); }
        catch (Exception e) when (e is not CliError) { throw new CliError(ErrorCode.Unreadable, $"the project could not be opened: {e.Message}"); }
        if (opened.AlignmentFile is null) throw new CliError(ErrorCode.NothingFound, "the project has no alignment result to export");
        var table = await ResultLoader.LoadAlignmentTableAsync(opened.AlignmentFile, opened.AnalysisFiles, ct);
        var curation = CurationStore.Load(opened.AlignmentFile.FilePath);
        var spots = table.Spots;
        if (args.Flag("annotated-only")) spots = spots.Where(s => s.IsAnnotated).ToList();

        var doc = new Document("export", new JsonObject { ["project"] = project, ["source"] = opened.Source, ["folder"] = opened.Folder });
        doc.Root["features"] = table.Spots.Count;
        doc.Root["exported_features"] = spots.Count;
        doc.Root["samples"] = table.Samples.Count;
        var written = new JsonArray();
        foreach (var kind in kinds)
        {
            var path = Path.GetFullPath(args.Get(kind)!);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            int count;
            switch (kind)
            {
                case "reviewed":
                    count = await CurationExporter.WriteFileAsync(path, spots, table.Samples, curation, new CurationExportOptions(args.Flag("areas") ? ExportValue.Area : ExportValue.Height));
                    break;
                case "matrix":
                    count = MatrixExporter.Write(path, spots, table.Samples);
                    break;
                default:
                    count = WriteMilq(path, spots, opened, args);
                    break;
            }
            written.Add(new JsonObject { ["kind"] = kind, ["path"] = path, ["rows"] = count });
            doc.Files.Add(path);
        }
        doc.Root["written"] = written;
        return doc;
    }

    private static int WriteMilq(string path, IReadOnlyList<AlignmentSpotRow> spots, OpenedProject opened, Args args)
    {
        var options = new OpenQuantExportOptions
        {
            AnnotatedOnly = args.Flag("annotated-only"),
            UseFragment = !args.Flag("no-fragment"),
            RtHalfWidthMinutes = args.Double("rt-half-window", 0.3),
            Tolerance = args.Double("tol", 0.02),
            Unit = args.Get("unit", "Da"),
        };
        var props = new List<AlignmentSpotProperty>(spots.Count);
        var decs = new List<MSDecResult>(spots.Count);
        foreach (var row in spots)
        {
            props.Add(row.Spot ?? new AlignmentSpotProperty { MasterAlignmentID = row.Id, AlignmentID = row.Id, Name = row.Name, MassCenter = row.Mz, TimesCenter = new CompMs.Common.Components.ChromXs(row.Rt), Ontology = row.Ontology });
            MSDecResult? dec = null;
            if (options.UseFragment && opened.AlignmentFile is not null)
            {
                try { dec = ResultLoader.LoadAlignmentMsDec(opened.AlignmentFile, row.Id); } catch { dec = null; }
            }
            decs.Add(dec!);
        }
        var components = OpenQuantComponentCsv.FromAlignmentSpots(props, decs, options);
        OpenQuantComponentCsv.Write(path, components);
        return components.Count;
    }
}
