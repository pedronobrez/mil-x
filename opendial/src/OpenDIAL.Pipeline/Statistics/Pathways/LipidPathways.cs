using OpenDIAL.Pipeline.Results;

namespace OpenDIAL.Pipeline.Statistics.Pathways;

/// <summary>At what grain the reactions are scored.</summary>
public enum PathwayLevel
{
    /// <summary>Whole classes: the sum of every confirmed PC against the sum of every confirmed PE.</summary>
    Class,
    /// <summary>Molecular species with their composition kept: PE 34:1 → PC 34:1, PC 16:0_18:1 → LPC 16:0.</summary>
    Species,
    /// <summary>Fatty acids, summed over the species that carry them: 16:0 → 18:0 (elongation), 18:0 → 18:1 (desaturation).</summary>
    FattyAcid,
}

/// <summary>One node of the scored network: a class, a species or a fatty acid, and what it summed.</summary>
public sealed record PathwayNode(string Name, string Class, int Members, double MeanA, double MeanB)
{
    public double Log2Change => MeanA > 0 && MeanB > 0 ? Math.Log2(MeanA / MeanB) : double.NaN;
}

/// <summary>One reaction scored between the two conditions.</summary>
public sealed record ReactionScore(
    string Id,
    string Reactant,
    string Product,
    string ReactantClass,
    string ProductClass,
    IReadOnlyList<double> WeightsA,
    IReadOnlyList<double> WeightsB,
    double Log2Change,
    double P,
    double Z,
    IReadOnlyList<string> Genes,
    string Note)
{
    public string Label => $"{Reactant} → {Product}";
    public string GeneText => string.Join(", ", Genes);
    public bool Tested => !double.IsNaN(Z);
    /// <summary>Active, suppressed or unchanged at the threshold the analysis was run with.</summary>
    public string Status { get; init; } = "untested";
    public double AbsZ => double.IsNaN(Z) ? -1 : Math.Abs(Z);
}

/// <summary>A chain of reactions, scored together.</summary>
public sealed record PathwayScore(IReadOnlyList<string> Nodes, IReadOnlyList<ReactionScore> Reactions, double Z, string Status)
{
    public string Chain => string.Join(" → ", Nodes);
    public int Length => Reactions.Count;
    public string GeneText => string.Join(", ", Reactions.SelectMany(r => r.Genes).Distinct());
    public double AbsZ => Math.Abs(Z);
}

/// <summary>A reaction whose product was measured and whose reactant was not, or the other way round.</summary>
public sealed record PredictedReaction(string Reaction, string Present, string Missing, IReadOnlyList<string> Genes)
{
    public string GeneText => string.Join(", ", Genes);
}

public sealed record PathwayResult(
    PathwayLevel Level,
    string ClassA,
    string ClassB,
    double Threshold,
    IReadOnlyList<PathwayNode> Nodes,
    IReadOnlyList<ReactionScore> Reactions,
    IReadOnlyList<PathwayScore> Pathways,
    IReadOnlyList<PredictedReaction> Predicted,
    string Message)
{
    public IReadOnlyList<ReactionScore> Tested => Reactions.Where(r => r.Tested).ToList();
    public int Active => Reactions.Count(r => r.Status == "active");
    public int Suppressed => Reactions.Count(r => r.Status == "suppressed");
}

/// <summary>
/// BioPAN's pathway analysis on the reviewed lipids, as BioPAN's own code does it: each reaction of
/// the network is weighted per injection by the ratio of its product to its reactant, the weights
/// of the two conditions are compared by Welch's t-test, the one-sided p in the direction of the
/// change becomes Z = Φ⁻¹(1 − p), and a pathway — a chain of reactions — scores Σ Z_i / √k over its
/// k reactions (BioPAN writes it 1/√(n−1) · Σ Z_i over its n lipids, the same number). Nothing here
/// is annotated anew: the classes and the chains come from the names MS-DIAL gave the features.
/// </summary>
public static class LipidPathways
{
    /// <summary>
    /// Scores the network on <paramref name="table"/> — linear values, one row per injection — between
    /// the injections of <paramref name="classA"/> and those of <paramref name="classB"/>.
    /// </summary>
    public static PathwayResult Compute(AnalysisTable table, string classA, string classB, PathwayLevel level = PathwayLevel.Class,
        double threshold = 1.645, int maxPathLength = 3, int minimumReplicates = 2, bool includeExtensions = false)
    {
        var network = ReactionDatabase.Reactions.Where(r => includeExtensions || r.IsBioPan).ToList();
        var groups = Univariate.GroupIndices(table);
        if (!groups.TryGetValue(classA, out var a) || !groups.TryGetValue(classB, out var b) || a.Count < minimumReplicates || b.Count < minimumReplicates)
        {
            return new PathwayResult(level, classA, classB, threshold, Array.Empty<PathwayNode>(), Array.Empty<ReactionScore>(), Array.Empty<PathwayScore>(),
                Array.Empty<PredictedReaction>(), $"Both classes need at least {minimumReplicates} injections; the reaction weights are compared between them.");
        }

        // every feature placed in the network: its class, its species key, its chains
        var placed = new List<(int Column, string Class, string Species, IReadOnlyList<string> Chains, bool Resolved)>();
        for (var j = 0; j < table.FeatureCount; j++)
        {
            var f = table.Features[j];
            var identity = LipidNames.Parse(f.Name, f.Ontology);
            var cls = ReactionDatabase.NetworkClass(f.Ontology, f.Name, identity) ?? ReactionDatabase.NetworkClass(null, f.Name);
            if (cls is null) continue;
            var chains = LipidNames.Chains(f.Name);
            var expected = LipidNames.ChainCount(cls);
            var resolved = chains.Count == expected && expected > 0;
            var species = identity.Carbons > 0 ? $"{cls} {identity.SumComposition}" : cls;
            placed.Add((j, cls, species, chains, resolved));
        }
        if (placed.Count == 0)
        {
            return new PathwayResult(level, classA, classB, threshold, Array.Empty<PathwayNode>(), Array.Empty<ReactionScore>(), Array.Empty<PathwayScore>(),
                Array.Empty<PredictedReaction>(), "No feature belongs to a class of the network: the names carry no lipid class MS-DIAL and BioPAN both know.");
        }

        // node abundances per injection
        var abundance = new Dictionary<string, double[]>(StringComparer.Ordinal);
        var members = new Dictionary<string, int>(StringComparer.Ordinal);
        var nodeClass = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string node, string cls, int column)
        {
            if (!abundance.TryGetValue(node, out var row))
            {
                row = new double[table.SampleCount];
                abundance[node] = row;
                members[node] = 0;
                nodeClass[node] = cls;
            }
            members[node]++;
            for (var i = 0; i < table.SampleCount; i++)
            {
                var v = table.Values[i, column];
                if (!double.IsNaN(v) && v > 0) row[i] += v;
            }
        }
        switch (level)
        {
            case PathwayLevel.Class:
                foreach (var p in placed) Add(p.Class, p.Class, p.Column);
                break;
            case PathwayLevel.Species:
                foreach (var p in placed) Add(p.Species, p.Class, p.Column);
                break;
            case PathwayLevel.FattyAcid:
                // a fatty acid is as abundant as the species that carry it, counted once per chain;
                // sphingoid bases are not fatty acids and stay out
                foreach (var p in placed.Where(p => p.Resolved && !IsSphingolipid(p.Class)))
                {
                    foreach (var chain in p.Chains) Add("FA " + chain, "FA", p.Column);
                }
                break;
        }

        // the edges the level allows
        var edges = new List<(string Id, string From, string To, string FromClass, string ToClass, IReadOnlyList<string> Genes, string Note)>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        void Edge(string id, string from, string to, string fromClass, string toClass, IReadOnlyList<string> genes, string note)
        {
            if (!abundance.ContainsKey(from) || !abundance.ContainsKey(to) || from == to) return;
            if (seen.Add(from + "" + to)) edges.Add((id, from, to, fromClass, toClass, genes, note));
        }
        switch (level)
        {
            case PathwayLevel.Class:
                foreach (var r in network.Where(r => r.Kind != ReactionKind.FattyAcid)) Edge(r.Id, r.Reactant, r.Product, r.Reactant, r.Product, r.Genes, r.Note);
                break;
            case PathwayLevel.Species:
                foreach (var r in network.Where(r => r.Kind != ReactionKind.FattyAcid))
                {
                    switch (r.Kind)
                    {
                        case ReactionKind.Preserving:
                            foreach (var species in placed.Where(p => p.Class == r.Reactant).Select(p => p.Species).Distinct())
                            {
                                var composition = species.Length > r.Reactant.Length ? species[(r.Reactant.Length + 1)..] : string.Empty;
                                if (composition.Length == 0) continue;
                                Edge(r.Id, species, $"{r.Product} {composition}", r.Reactant, r.Product, r.Genes, r.Note);
                            }
                            break;
                        case ReactionKind.RemovesChain:
                            foreach (var p in placed.Where(p => p.Class == r.Reactant && p.Resolved))
                            {
                                foreach (var remaining in WithoutOne(p.Chains))
                                    Edge(r.Id, p.Species, $"{r.Product} {Sum(remaining)}", r.Reactant, r.Product, r.Genes, r.Note);
                            }
                            break;
                        case ReactionKind.AddsChain:
                            foreach (var p in placed.Where(p => p.Class == r.Product && p.Resolved))
                            {
                                foreach (var remaining in WithoutOne(p.Chains))
                                    Edge(r.Id, $"{r.Reactant} {Sum(remaining)}", p.Species, r.Reactant, r.Product, r.Genes, r.Note);
                            }
                            break;
                    }
                }
                break;
            case PathwayLevel.FattyAcid:
                // BioPAN's thirty steps, and no others: a chain pair the table does not name is not an edge
                foreach (var r in network.Where(r => r.Kind == ReactionKind.FattyAcid)) Edge(r.Id, r.Reactant, r.Product, "FA", "FA", r.Genes, r.Note);
                break;
        }

        // the weights and the test
        var scores = new List<ReactionScore>();
        foreach (var e in edges)
        {
            var from = abundance[e.From];
            var to = abundance[e.To];
            // an injection without the reactant has no weight; one without the product has weight zero
            var wa = a.Select(i => from[i] > 0 ? to[i] / from[i] : double.NaN).ToList();
            var wb = b.Select(i => from[i] > 0 ? to[i] / from[i] : double.NaN).ToList();
            var ra = wa.Where(w => !double.IsNaN(w)).ToArray();
            var rb = wb.Where(w => !double.IsNaN(w)).ToArray();
            double p = double.NaN, z = double.NaN, change = double.NaN;
            var status = "untested";
            if (ra.Length >= minimumReplicates && rb.Length >= minimumReplicates)
            {
                var meanA = ra.Average();
                var meanB = rb.Average();
                change = meanA > 0 && meanB > 0 ? Math.Log2(meanA / meanB) : meanA > meanB ? double.PositiveInfinity : meanA < meanB ? double.NegativeInfinity : 0;
                (_, p) = Univariate.TTest(ra, rb, equalVariance: false);
                z = SignedZ(p, meanA - meanB);
                status = z >= threshold ? "active" : z <= -threshold ? "suppressed" : "unchanged";
            }
            scores.Add(new ReactionScore(e.Id, e.From, e.To, e.FromClass, e.ToClass, wa, wb, change, p, z, e.Genes, e.Note) { Status = status });
        }

        // the pathways: simple chains over the tested reactions, combined by Stouffer's method
        var tested = scores.Where(s => s.Tested).ToList();
        var outgoing = tested.GroupBy(s => s.Reactant).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var pathways = new List<PathwayScore>();
        var budget = 20000;
        foreach (var start in outgoing.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            Walk(start, new List<string> { start }, new List<ReactionScore>());
        }
        void Walk(string node, List<string> nodes, List<ReactionScore> chain)
        {
            if (chain.Count >= maxPathLength || budget <= 0) return;
            if (!outgoing.TryGetValue(node, out var next)) return;
            foreach (var step in next)
            {
                if (nodes.Contains(step.Product)) continue;
                budget--;
                nodes.Add(step.Product);
                chain.Add(step);
                var z = chain.Sum(s => s.Z) / Math.Sqrt(chain.Count);
                pathways.Add(new PathwayScore(nodes.ToList(), chain.ToList(), z, z >= threshold ? "active" : z <= -threshold ? "suppressed" : "unchanged"));
                Walk(step.Product, nodes, chain);
                nodes.RemoveAt(nodes.Count - 1);
                chain.RemoveAt(chain.Count - 1);
            }
        }
        var ranked = pathways.OrderByDescending(p => Math.Abs(p.Z)).ThenByDescending(p => p.Length).Take(500).ToList();

        // what could be tested with one more class confirmed
        var predicted = new List<PredictedReaction>();
        if (level == PathwayLevel.Class)
        {
            foreach (var r in network.Where(r => r.Kind != ReactionKind.FattyAcid))
            {
                var hasReactant = abundance.ContainsKey(r.Reactant);
                var hasProduct = abundance.ContainsKey(r.Product);
                if (hasReactant == hasProduct) continue;
                predicted.Add(new PredictedReaction(r.Label, hasReactant ? r.Reactant : r.Product, hasReactant ? r.Product : r.Reactant, r.Genes));
            }
        }

        var nodes = abundance.Keys.OrderBy(k => k, StringComparer.Ordinal)
            .Select(k => new PathwayNode(k, nodeClass[k], members[k], Mean(abundance[k], a), Mean(abundance[k], b)))
            .ToList();
        var levelName = level switch { PathwayLevel.Species => "species", PathwayLevel.FattyAcid => "fatty-acid", _ => "class" };
        var active = scores.Count(s => s.Status == "active");
        var suppressed = scores.Count(s => s.Status == "suppressed");
        var message = tested.Count == 0
            ? $"No reaction of the network has both ends measured at the {levelName} level in both classes; {abundance.Count} node(s) placed, {edges.Count} edge(s) drawn."
            : $"{tested.Count} reaction(s) tested at the {levelName} level, {classA} against {classB} · {active} active, {suppressed} suppressed at |Z| ≥ {threshold:0.###} · "
              + $"{ranked.Count(p => p.Status != "unchanged")} of {ranked.Count} pathway(s) past the threshold"
              + (scores.Count > tested.Count ? $" · {scores.Count - tested.Count} reaction(s) with too few injections to test" : string.Empty);
        return new PathwayResult(level, classA, classB, threshold, nodes, scores, ranked, predicted, message);
    }

    /// <summary>The two-sided p as a Z with the sign of the change; capped where p underflows.</summary>
    public static double SignedZ(double p, double change)
    {
        if (double.IsNaN(p)) return double.NaN;
        var z = Distributions.NormalQuantile(1 - Math.Clamp(p, 1e-15, 1) / 2);
        return change < 0 ? -z : z;
    }

    private static double Mean(double[] row, IReadOnlyList<int> rows)
    {
        var values = rows.Select(i => row[i]).Where(v => v > 0).ToList();
        return values.Count == 0 ? double.NaN : values.Average();
    }

    private static bool IsSphingolipid(string cls) =>
        cls is "Cer" or "dhCer" or "SM" or "dhSM" or "HexCer" or "LacCer" or "SHexCer" or "Cer1P" or "SPB" or "SPBP" or "dhSPB" or "dhSPBP" or "LysoSM";

    private static IEnumerable<IReadOnlyList<string>> WithoutOne(IReadOnlyList<string> chains)
    {
        for (var k = 0; k < chains.Count; k++)
        {
            var rest = chains.Where((_, i) => i != k).ToList();
            if (rest.Count > 0) yield return rest;
        }
    }

    private static string Sum(IReadOnlyList<string> chains)
    {
        var c = 0;
        var d = 0;
        foreach (var chain in chains)
        {
            var (cc, dd) = ParseChain(chain);
            c += cc;
            d += dd;
        }
        return $"{c}:{d}";
    }

    private static (int Carbons, int DoubleBonds) ParseChain(string chain)
    {
        var colon = chain.IndexOf(':');
        if (colon <= 0) return (0, 0);
        return int.TryParse(chain[..colon], out var c) && int.TryParse(chain[(colon + 1)..], out var d) ? (c, d) : (0, 0);
    }
}
