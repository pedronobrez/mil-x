using System.Text.Json.Nodes;
using MilX.Pipeline.Results;

namespace MilX.Cli.Commands;

/// <summary>Samples and channels of a file.</summary>
internal static class Info
{
    public static Document Run(Args args, RawStore store, CancellationToken ct)
    {
        var file = Common.OpenTarget(args, store, "info", headersOnly: true, ct);
        var doc = new Document("info", Common.Input(file));
        var names = RawStore.SampleNames(file.Path);
        doc.Root["samples"] = new JsonArray(names.Select((n, i) => (JsonNode)new JsonObject { ["index"] = i, ["name"] = n }).ToArray());
        doc.Root["spectra"] = file.Spectra;
        doc.Root["read_from"] = file.ReadFrom;
        doc.Root["read_seconds"] = Math.Round(file.ReadTime.TotalSeconds, 3);
        var rts = file.Raw.SpectrumList.Select(RawExplorer.RtMinutes).ToList();
        doc.Root["rt_range"] = rts.Count == 0 ? null : new JsonObject { ["low"] = Num.Rt(rts.Min()), ["high"] = Num.Rt(rts.Max()) };
        var anyMs2 = file.Channels.Any(c => c.Kind != RawChannelKind.Ms1);
        doc.Root["acquisition"] = !anyMs2 ? "ms1-only" : file.Channels.Any(c => c.Kind == RawChannelKind.Ms2Window) ? "dia" : RawExplorer.HasExperiments(file.Raw) ? "ida" : "dda";
        var channels = new JsonArray();
        var rows = new List<JsonObject>();
        for (var i = 0; i < file.Channels.Count; i++)
        {
            var c = file.Channels[i];
            var (lo, hi) = Common.MassRange(file.Raw, c.SpectrumIndices);
            var row = new JsonObject
            {
                ["index"] = i,
                ["kind"] = Common.KindName(c.Kind, c, file.Raw),
                ["label"] = c.Label,
                ["experiment"] = c.ExperimentId,
                ["scans"] = c.Count,
                ["ms_level"] = c.Count > 0 ? Math.Max(1, file.Raw.SpectrumList[c.SpectrumIndices[0]].MsLevel) : 1,
                ["mass_range"] = hi > lo ? new JsonObject { ["low"] = Num.Mz(lo), ["high"] = Num.Mz(hi) } : null,
                ["precursor_mz"] = c.PrecursorMz > 0 ? Num.Mz(c.PrecursorMz) : null,
                ["ce"] = c.CollisionEnergy != 0 ? Math.Round(c.CollisionEnergy, 2) : null,
                ["polarity"] = Common.Polarity(file.Raw, c.SpectrumIndices),
            };
            if (c.Kind == RawChannelKind.Ms2Events)
            {
                row["distinct_precursors"] = XicSources.Precursors(file.Raw, c.SpectrumIndices).Count;
            }
            channels.Add(row);
            rows.Add((JsonObject)row.DeepClone());
        }
        doc.Root["channels"] = channels;
        doc.Rows = rows;
        return doc;
    }
}
