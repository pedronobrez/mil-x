using System.Globalization;
using System.Text;

namespace MilX.Pipeline.Results;

/// <summary>The height and area matrix of every feature × sample as TSV: what Re-export alignment matrix… writes, shared by the window and the command line.</summary>
public static class MatrixExporter
{
    public static string Build(IReadOnlyList<AlignmentSpotRow> spots, IReadOnlyList<SampleInfo> samples)
    {
        var sb = new StringBuilder();
        sb.Append("Alignment ID\tName\tRT (min)\tm/z\tAdduct\tFormula\tOntology\tScore\tFill %");
        foreach (var s in samples) sb.Append('\t').Append(s.FileName).Append(" height");
        foreach (var s in samples) sb.Append('\t').Append(s.FileName).Append(" area");
        sb.Append('\n');
        foreach (var spot in spots)
        {
            sb.Append(spot.Id).Append('\t').Append(spot.Name).Append('\t').Append(spot.Rt.ToString("F3", CultureInfo.InvariantCulture)).Append('\t')
              .Append(spot.Mz.ToString("F5", CultureInfo.InvariantCulture)).Append('\t').Append(spot.Adduct).Append('\t').Append(spot.Formula).Append('\t').Append(spot.Ontology).Append('\t')
              .Append(spot.Score.ToString("F3", CultureInfo.InvariantCulture)).Append('\t').Append(spot.FillPercent.ToString("F0", CultureInfo.InvariantCulture));
            foreach (var s in samples)
            {
                var p = spot.SamplePeaks.FirstOrDefault(q => q.FileId == s.FileId);
                sb.Append('\t').Append((p?.Height ?? 0).ToString("F0", CultureInfo.InvariantCulture));
            }
            foreach (var s in samples)
            {
                var p = spot.SamplePeaks.FirstOrDefault(q => q.FileId == s.FileId);
                sb.Append('\t').Append((p?.Area ?? 0).ToString("F0", CultureInfo.InvariantCulture));
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }

    /// <summary>Writes the matrix; returns the number of feature rows.</summary>
    public static int Write(string path, IReadOnlyList<AlignmentSpotRow> spots, IReadOnlyList<SampleInfo> samples)
    {
        File.WriteAllText(path, Build(spots, samples), new UTF8Encoding(false));
        return spots.Count;
    }
}
