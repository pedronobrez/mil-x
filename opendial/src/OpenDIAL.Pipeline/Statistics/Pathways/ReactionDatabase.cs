using System.Reflection;

namespace OpenDIAL.Pipeline.Statistics.Pathways;

/// <summary>How a reaction maps the molecular species of its reactant onto those of its product.</summary>
public enum ReactionKind
{
    /// <summary>The chains are kept: PE 34:1 → PC 34:1.</summary>
    Preserving,
    /// <summary>One chain is lost: PC 16:0_18:1 → LPC 16:0 or LPC 18:1.</summary>
    RemovesChain,
    /// <summary>One chain is gained: LPC 16:0 → PC 16:0_18:1.</summary>
    AddsChain,
    /// <summary>Only the whole classes relate: Chol → CE.</summary>
    ClassOnly,
    /// <summary>A step between fatty acids: FA 16:0 → FA 18:0.</summary>
    FattyAcid,
}

/// <summary>One enzymatic step between two lipid classes (or two fatty acids), with the genes behind it.</summary>
public sealed record LipidReaction(string Id, string Reactant, string Product, ReactionKind Kind, IReadOnlyList<string> Genes, string Note, string Source)
{
    public string Label => $"{Reactant} → {Product}";
    /// <summary>Transcribed from BioPAN's database, as opposed to a step OpenDIAL adds.</summary>
    public bool IsBioPan => string.Equals(Source, "biopan", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// The mammalian lipid reaction network — BioPAN's, read from the table shipped with the
/// application, plus the steps OpenDIAL adds marked as such — and the map from MS-DIAL's ontology
/// names to the network's classes: "Cer_NS" and "Cer_AS" are "Cer" here, "Cer_NDS" is "dhCer",
/// "Sph" is "SPB", "EtherPC" is "O-PC" or "P-PC" by what the name says.
/// </summary>
public static class ReactionDatabase
{
    private static readonly Lazy<IReadOnlyList<LipidReaction>> Loaded = new(Load);

    public static IReadOnlyList<LipidReaction> Reactions => Loaded.Value;

    /// <summary>The classes the network knows, in the order they first appear; the fatty acids are one class, "FA".</summary>
    public static IReadOnlyList<string> Classes => Reactions
        .Where(r => r.Kind != ReactionKind.FattyAcid)
        .SelectMany(r => new[] { r.Reactant, r.Product })
        .Concat(new[] { "FA" })
        .Distinct()
        .ToList();

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Hex2Cer"] = "LacCer", ["LacCer"] = "LacCer",
        ["CerP"] = "Cer1P", ["Cer1P"] = "Cer1P",
        ["Sph"] = "SPB", ["SPB"] = "SPB", ["DHSph"] = "dhSPB", ["dhSPB"] = "dhSPB",
        ["SPBP"] = "SPBP", ["S1P"] = "SPBP", ["dhSPBP"] = "dhSPBP",
        ["Cholesterol"] = "Chol", ["Chol"] = "Chol", ["ST"] = "Chol",
        ["LSM"] = "LysoSM", ["SPC"] = "LysoSM", ["LysoSM"] = "LysoSM",
        ["ACar"] = "CAR", ["CAR"] = "CAR", ["FA"] = "FA",
        ["EtherDG"] = "O-DG", ["EtherLPA"] = "O-LPA",
    };

    /// <summary>
    /// The network class a feature belongs to, from its ontology and, where the ontology does not
    /// decide, its name: the ether classes split into alkyl ("O-") and alkenyl ("P-") by the name,
    /// the ceramides and sphingomyelins into the sphingosine and the sphinganine ("dh") forms by
    /// the subclass or by a saturated composition. Null when the class is outside the network.
    /// </summary>
    public static string? NetworkClass(string? ontology, string? name = null, LipidIdentity? identity = null)
    {
        var cls = string.IsNullOrWhiteSpace(ontology) ? (identity?.Class ?? LipidNames.Parse(name ?? string.Empty).Class) : ontology.Trim();
        if (string.IsNullOrWhiteSpace(cls)) return null;
        // MS-DIAL's deuterated standard classes ("PC_d5") are their class
        var standardSuffix = System.Text.RegularExpressions.Regex.Match(cls, @"_d\d+$");
        if (standardSuffix.Success) cls = cls[..standardSuffix.Index];
        identity ??= LipidNames.Parse(name ?? string.Empty, cls);
        var saturated = identity.Carbons > 0 && identity.DoubleBonds == 0;

        if (Aliases.TryGetValue(cls, out var alias))
        {
            return alias switch
            {
                "SPB" when saturated => "dhSPB",
                "SPBP" when saturated => "dhSPBP",
                _ => alias,
            };
        }
        // the ether classes: alkyl or alkenyl, which only the name says
        if (cls.StartsWith("Ether", StringComparison.OrdinalIgnoreCase))
        {
            var rest = cls[5..];
            var plasmenyl = name is not null && (name.Contains(" P-", StringComparison.Ordinal) || name.Contains("P-" + identity.Carbons, StringComparison.Ordinal));
            var known = rest.ToUpperInvariant() switch { "PC" => "PC", "PE" => "PE", "LPC" => "LPC", "LPE" => "LPE", "DG" => "DG", "LPA" => "LPA", _ => null };
            if (known is null) return null;
            if (known is "DG") return "O-DG";
            if (known is "LPA") return plasmenyl ? "P-LPA" : "O-LPA";
            return (plasmenyl ? "P-" : "O-") + known;
        }
        var underscore = cls.IndexOf('_');
        var head = underscore > 0 ? cls[..underscore] : cls;
        var subclass = underscore > 0 ? cls[(underscore + 1)..] : string.Empty;
        // the sphinganine ("DS") ceramide subclasses, and any ceramide with no double bond at all
        var dihydro = subclass.EndsWith("DS", StringComparison.OrdinalIgnoreCase) || subclass.EndsWith("DOS", StringComparison.OrdinalIgnoreCase);
        if (string.Equals(head, "Cer", StringComparison.OrdinalIgnoreCase)) return dihydro || saturated ? "dhCer" : "Cer";
        if (string.Equals(head, "HexCer", StringComparison.OrdinalIgnoreCase)) return "HexCer";
        if (string.Equals(head, "SHexCer", StringComparison.OrdinalIgnoreCase)) return "SHexCer";
        if (string.Equals(head, "SM", StringComparison.OrdinalIgnoreCase)) return saturated ? "dhSM" : "SM";
        return Classes.FirstOrDefault(c => string.Equals(c, head, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<LipidReaction> Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("OpenDIAL.Pipeline.Statistics.Pathways.reactions.tsv")
            ?? throw new InvalidOperationException("the reaction table is not embedded");
        using var reader = new StreamReader(stream);
        var list = new List<LipidReaction>();
        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line.StartsWith('#')) continue;
            var cells = line.Split('\t');
            if (cells.Length < 5) continue;
            var kind = cells[3].Trim().ToLowerInvariant() switch
            {
                "removes" => ReactionKind.RemovesChain,
                "adds" => ReactionKind.AddsChain,
                "class" => ReactionKind.ClassOnly,
                "fa" => ReactionKind.FattyAcid,
                _ => ReactionKind.Preserving,
            };
            var genes = cells[4].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            list.Add(new LipidReaction(cells[0].Trim(), cells[1].Trim(), cells[2].Trim(), kind, genes,
                cells.Length > 5 ? cells[5].Trim() : string.Empty, cells.Length > 6 ? cells[6].Trim() : "biopan"));
        }
        return list;
    }
}
