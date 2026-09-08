using OpenDIAL.Pipeline.Results;

namespace OpenDIAL.Pipeline.Statistics;

/// <summary>How one set fared in the over-representation test.</summary>
public sealed record EnrichmentRow(
    string Set,
    string Kind,
    int SetSize,
    int Hits,
    double Expected,
    double EnrichmentRatio,
    double P,
    double AdjustedP,
    IReadOnlyList<string> Members)
{
    public double NegativeLog10P => P <= 0 ? 300 : -Math.Log10(P);
}

public sealed record EnrichmentResult(IReadOnlyList<EnrichmentRow> Rows, int Significant, int Background, string Message);

/// <summary>How a class as a whole moved between two conditions.</summary>
public sealed record ClassChange(string Class, int Members, double MeanLog2FoldChange, double MedianLog2FoldChange, int Up, int Down, double P);

/// <summary>A cell of the chain map: how many features, and how they changed, at one carbon count and double-bond count.</summary>
public sealed record ChainCell(int Carbons, int DoubleBonds, int Count, double MeanLog2FoldChange, double MinP);

/// <summary>
/// Enrichment for lipids, where the sets are what a name carries: the lipid class, the total chain
/// length, the number of double bonds. Over-representation asks whether the features that changed
/// fall into a set more often than the features tested would by chance — a hypergeometric test,
/// the one MetaboAnalyst's ORA runs — and the class summaries ask which way each set moved.
/// </summary>
public static class Enrichment
{
    /// <summary>The sets a feature belongs to, by its name.</summary>
    public static IReadOnlyList<(string Set, string Kind)> SetsOf(AlignmentSpotRow feature)
    {
        var identity = LipidNames.Parse(feature.Name, feature.Ontology);
        var sets = new List<(string, string)>();
        if (!string.IsNullOrWhiteSpace(identity.Class) && !identity.Class.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase)) sets.Add((identity.Class, "lipid class"));
        if (identity.Carbons > 0)
        {
            sets.Add(($"{identity.Carbons} carbons", "chain length"));
            sets.Add((identity.DoubleBonds == 0 ? "saturated" : identity.DoubleBonds == 1 ? "monounsaturated" : $"{identity.DoubleBonds} double bonds", "unsaturation"));
            sets.Add(($"{identity.Class} {identity.Carbons}:{identity.DoubleBonds}", "species"));
        }
        return sets;
    }

    /// <summary>
    /// Over-representation of the significant features in every set with at least
    /// <paramref name="minimumSetSize"/> members among the tested ones.
    /// </summary>
    public static EnrichmentResult OverRepresentation(
        IReadOnlyList<AlignmentSpotRow> tested,
        ISet<int> significantIds,
        int minimumSetSize = 3,
        PAdjustment adjustment = PAdjustment.FalseDiscoveryRate,
        bool includeSpecies = false)
    {
        var members = new Dictionary<(string Set, string Kind), List<AlignmentSpotRow>>();
        foreach (var feature in tested)
        {
            foreach (var set in SetsOf(feature))
            {
                if (!includeSpecies && set.Kind == "species") continue;
                if (!members.TryGetValue(set, out var list)) members[set] = list = new List<AlignmentSpotRow>();
                list.Add(feature);
            }
        }
        var total = tested.Count;
        var significant = tested.Count(f => significantIds.Contains(f.Id));
        var rows = new List<EnrichmentRow>();
        foreach (var (set, list) in members)
        {
            if (list.Count < minimumSetSize) continue;
            var hits = list.Count(f => significantIds.Contains(f.Id));
            var expected = total == 0 ? 0 : (double)significant * list.Count / total;
            var p = Distributions.HypergeometricUpper(hits, significant, list.Count, total);
            rows.Add(new EnrichmentRow(set.Set, set.Kind, list.Count, hits, expected, expected > 0 ? hits / expected : 0, p, double.NaN,
                list.Where(f => significantIds.Contains(f.Id)).Select(AnalysisTable.LabelOf).ToList()));
        }
        var adjusted = MultipleTesting.Adjust(rows.Select(r => r.P).ToList(), adjustment);
        rows = rows.Select((r, i) => r with { AdjustedP = adjusted[i] }).OrderBy(r => r.P).ThenByDescending(r => r.EnrichmentRatio).ToList();
        var message = significant == 0
            ? "No significant feature to enrich: loosen the threshold or the filter."
            : $"{significant} of {total} features significant · {rows.Count} sets tested · {rows.Count(r => r.AdjustedP <= 0.05)} enriched at {Univariate.AdjustName(adjustment)} ≤ 0.05.";
        return new EnrichmentResult(rows, significant, total, message);
    }

    /// <summary>
    /// Each class's shift between two conditions, from the per-feature log2 fold changes: the mean
    /// and the median, how many rose and fell, and a one-sample t-test of the log fold changes
    /// against zero.
    /// </summary>
    public static IReadOnlyList<ClassChange> ClassChanges(IReadOnlyList<FeatureComparison> comparisons, IReadOnlyList<AlignmentSpotRow> features)
    {
        var byId = features.ToDictionary(f => f.Id);
        var rows = new List<ClassChange>();
        foreach (var group in comparisons.Where(c => !double.IsNaN(c.Log2FoldChange) && byId.ContainsKey(c.FeatureId))
                     .GroupBy(c => LipidNames.Parse(byId[c.FeatureId].Name, byId[c.FeatureId].Ontology).Class))
        {
            var values = group.Select(c => c.Log2FoldChange).ToList();
            var mean = values.Average();
            var sd = Preprocessing.StandardDeviation(values);
            var t = values.Count > 1 && sd > 0 ? mean / (sd / Math.Sqrt(values.Count)) : 0;
            var p = values.Count > 1 && sd > 0 ? Distributions.StudentTwoSided(t, values.Count - 1) : double.NaN;
            rows.Add(new ClassChange(group.Key, values.Count, mean, Preprocessing.Median(values), values.Count(v => v > 0), values.Count(v => v < 0), p));
        }
        return rows.OrderByDescending(r => r.Members).ToList();
    }

    /// <summary>The chain map: the comparisons laid out by total carbons and double bonds, for one class or all.</summary>
    public static IReadOnlyList<ChainCell> ChainMap(IReadOnlyList<FeatureComparison> comparisons, IReadOnlyList<AlignmentSpotRow> features, string? lipidClass)
    {
        var byId = features.ToDictionary(f => f.Id);
        var cells = new Dictionary<(int, int), List<FeatureComparison>>();
        foreach (var c in comparisons)
        {
            if (!byId.TryGetValue(c.FeatureId, out var feature)) continue;
            var identity = LipidNames.Parse(feature.Name, feature.Ontology);
            if (identity.Carbons == 0) continue;
            if (lipidClass is not null && !string.Equals(identity.Class, lipidClass, StringComparison.OrdinalIgnoreCase)) continue;
            var key = (identity.Carbons, identity.DoubleBonds);
            if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<FeatureComparison>();
            list.Add(c);
        }
        return cells.Select(kv => new ChainCell(kv.Key.Item1, kv.Key.Item2, kv.Value.Count,
                kv.Value.Where(c => !double.IsNaN(c.Log2FoldChange)).Select(c => c.Log2FoldChange).DefaultIfEmpty(0).Average(),
                kv.Value.Where(c => !double.IsNaN(c.P)).Select(c => c.P).DefaultIfEmpty(1).Min()))
            .OrderBy(c => c.Carbons).ThenBy(c => c.DoubleBonds).ToList();
    }
}
