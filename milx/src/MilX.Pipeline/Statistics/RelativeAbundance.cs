using MilX.Pipeline.Results;

namespace MilX.Pipeline.Statistics;

/// <summary>Which feature stands as the internal standard for a class; null means the class is left as raw responses.</summary>
public sealed record StandardAssignment(string Class, int? StandardFeatureId);

/// <summary>What the ratio table was built from, for the record and for the report.</summary>
public sealed record RelativeAbundanceReport(
    int ConfirmedFeatures,
    int Standards,
    IReadOnlyList<StandardAssignment> Assignments,
    IReadOnlyList<string> ClassesWithoutStandard,
    string Message);

/// <summary>
/// The analytes as ratios to an internal standard — relative abundance, the number a lipidomics
/// result is reported in. Only confirmed analytes take part: a ratio of a misannotation to a
/// standard is a number with no meaning, and the review is what says which ones were checked.
///
/// Each class is divided by its own standard, injection by injection, so a matrix effect or an
/// extraction loss that hit the class as a whole cancels. A class with no standard chosen keeps its
/// raw response, and the report says so.
/// </summary>
public static class RelativeAbundance
{
    /// <summary>The classes that have at least one confirmed analyte, in order of size.</summary>
    public static IReadOnlyList<(string Class, int Confirmed)> ClassesWithConfirmed(IEnumerable<AlignmentSpotRow> confirmed) =>
        confirmed
            .GroupBy(f => ClassOf(f))
            .Select(g => (g.Key, g.Count()))
            .OrderByDescending(x => x.Item2)
            .ThenBy(x => x.Item1, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string ClassOf(AlignmentSpotRow f) =>
        string.IsNullOrWhiteSpace(f.Ontology) ? LipidNames.Parse(f.Name).Class : f.Ontology;

    /// <summary>
    /// A first choice of standard for every class, for the dialog to offer: the best-scoring
    /// confirmed feature of the same class by <see cref="LipidNames.StandardScore"/>, or none when
    /// nothing of the class looks like one. A standard of another class is never suggested; that
    /// is a decision for the person, and the dialog lists it.
    /// </summary>
    public static IReadOnlyList<StandardAssignment> Suggest(IReadOnlyList<AlignmentSpotRow> confirmed)
    {
        var result = new List<StandardAssignment>();
        foreach (var (cls, _) in ClassesWithConfirmed(confirmed))
        {
            var best = confirmed
                .Select(f => (Feature: f, Score: LipidNames.StandardScore(f.Name, f.Ontology, cls)))
                .Where(x => x.Score >= 40)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Feature.AverageHeight)
                .FirstOrDefault();
            result.Add(new StandardAssignment(cls, best.Feature?.Id));
        }
        return result;
    }

    /// <summary>
    /// Builds the ratio table over the confirmed analytes. Standards themselves are left out of the
    /// table (a standard divided by itself is one everywhere), as is any feature with no detected
    /// peak in an injection, which becomes a missing value rather than a zero.
    /// </summary>
    public static (AnalysisTable Table, RelativeAbundanceReport Report) Build(
        IReadOnlyList<AlignmentSpotRow> confirmed,
        IReadOnlyList<SampleInfo> samples,
        IReadOnlyList<StandardAssignment> assignments,
        bool useArea = true)
    {
        ArgumentNullException.ThrowIfNull(confirmed);
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(assignments);

        var byClass = assignments.Where(a => a.StandardFeatureId is not null).ToDictionary(a => a.Class, a => a.StandardFeatureId!.Value, StringComparer.OrdinalIgnoreCase);
        var standardIds = byClass.Values.ToHashSet();
        var standards = confirmed.Where(f => standardIds.Contains(f.Id)).ToDictionary(f => f.Id);
        var analytes = confirmed.Where(f => !standardIds.Contains(f.Id)).ToList();
        var n = samples.Count;
        var values = new double[n, analytes.Count];
        var without = new List<string>();
        for (var j = 0; j < analytes.Count; j++)
        {
            var feature = analytes[j];
            var cls = ClassOf(feature);
            AlignmentSpotRow? standard = byClass.TryGetValue(cls, out var id) && standards.TryGetValue(id, out var s) ? s : null;
            if (standard is null && !without.Contains(cls)) without.Add(cls);
            for (var i = 0; i < n; i++)
            {
                var peak = feature.SamplePeaks.FirstOrDefault(p => p.FileId == samples[i].FileId);
                var value = peak is null ? double.NaN : useArea ? peak.Area : peak.Height;
                if (double.IsNaN(value) || value <= 0) { values[i, j] = double.NaN; continue; }
                if (standard is not null)
                {
                    var reference = standard.SamplePeaks.FirstOrDefault(p => p.FileId == samples[i].FileId);
                    var denominator = reference is null ? double.NaN : useArea ? reference.Area : reference.Height;
                    values[i, j] = double.IsNaN(denominator) || denominator <= 0 ? double.NaN : value / denominator;
                }
                else
                {
                    values[i, j] = value;
                }
            }
        }
        var what = useArea ? "area" : "height";
        var name = byClass.Count == 0 ? $"peak {what} (no standard)" : $"{what} ratio to the class standard";
        var message = analytes.Count == 0
            ? "No confirmed analyte to work with. Confirm features in the review workspace first."
            : $"{analytes.Count} confirmed analyte(s) as {name} over {standards.Count} standard(s)"
              + (without.Count > 0 ? $"; {without.Count} class(es) with no standard keep their raw {what}: {string.Join(", ", without)}" : string.Empty) + ".";
        return (new AnalysisTable(values, samples, analytes, name), new RelativeAbundanceReport(analytes.Count, standards.Count, assignments, without, message));
    }

    /// <summary>The plain table of every feature given, as heights or areas, for the analysis without standards.</summary>
    public static AnalysisTable Raw(IReadOnlyList<AlignmentSpotRow> features, IReadOnlyList<SampleInfo> samples, bool useArea)
    {
        var values = new double[samples.Count, features.Count];
        for (var j = 0; j < features.Count; j++)
        {
            for (var i = 0; i < samples.Count; i++)
            {
                var peak = features[j].SamplePeaks.FirstOrDefault(p => p.FileId == samples[i].FileId);
                var v = peak is null ? double.NaN : useArea ? peak.Area : peak.Height;
                values[i, j] = double.IsNaN(v) || v <= 0 ? double.NaN : v;
            }
        }
        return new AnalysisTable(values, samples, features, useArea ? "peak area" : "peak height");
    }
}
