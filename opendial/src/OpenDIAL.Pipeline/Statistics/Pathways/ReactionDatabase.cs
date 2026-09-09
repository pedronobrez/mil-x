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
}

/// <summary>One enzymatic step between two lipid classes, with the genes behind it.</summary>
public sealed record LipidReaction(string Id, string Reactant, string Product, ReactionKind Kind, IReadOnlyList<string> Genes, string Note)
{
    public string Label => $"{Reactant} → {Product}";
}

/// <summary>
/// The mammalian lipid reaction network, read from the table shipped with the application, and the
/// map from MS-DIAL's ontology names to the network's classes ("Cer_NS", "Cer_AS" and the rest are
/// all "Cer" here; "SPB" is "Sph"; "EtherPC" is "PC O-").
/// </summary>
public static class ReactionDatabase
{
    private static readonly Lazy<IReadOnlyList<LipidReaction>> Loaded = new(Load);

    public static IReadOnlyList<LipidReaction> Reactions => Loaded.Value;

    /// <summary>The classes the network knows, in the order they first appear.</summary>
    public static IReadOnlyList<string> Classes => Reactions.SelectMany(r => new[] { r.Reactant, r.Product }).Distinct().ToList();

    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["EtherPC"] = "PC O-", ["PC O-"] = "PC O-", ["PC P-"] = "PC O-",
        ["EtherPE"] = "PE O-", ["PE O-"] = "PE O-", ["PE P-"] = "PE O-",
        ["EtherLPC"] = "LPC O-", ["LPC O-"] = "LPC O-",
        ["EtherLPE"] = "LPE O-", ["LPE O-"] = "LPE O-",
        ["Hex2Cer"] = "LacCer", ["LacCer"] = "LacCer",
        ["CerP"] = "Cer1P", ["Cer1P"] = "Cer1P",
        ["SPB"] = "Sph", ["Sph"] = "Sph", ["SPBP"] = "S1P", ["S1P"] = "S1P",
        ["Cholesterol"] = "Chol", ["Chol"] = "Chol", ["ST"] = "Chol",
        ["CDPDG"] = "CDP-DG", ["CDP-DG"] = "CDP-DG",
        ["PIP"] = "PIP", ["PIP2"] = "PIP2", ["PIP3"] = "PIP3",
        ["ACar"] = "CAR", ["CAR"] = "CAR", ["FA"] = "FA", ["FAHFA"] = "FA",
    };

    /// <summary>The network class an ontology (or a parsed class) belongs to, or null when it is outside the network.</summary>
    public static string? NetworkClass(string? cls)
    {
        if (string.IsNullOrWhiteSpace(cls)) return null;
        cls = cls.Trim();
        if (Aliases.TryGetValue(cls, out var alias)) return alias;
        // the ceramide and hexosylceramide subclasses ("Cer_NS", "HexCer_AP") fold into their class
        var underscore = cls.IndexOf('_');
        var head = underscore > 0 ? cls[..underscore] : cls;
        if (string.Equals(head, "Cer", StringComparison.OrdinalIgnoreCase)) return "Cer";
        if (string.Equals(head, "HexCer", StringComparison.OrdinalIgnoreCase)) return "HexCer";
        if (string.Equals(head, "SHexCer", StringComparison.OrdinalIgnoreCase)) return "SHexCer";
        if (string.Equals(head, "SM", StringComparison.OrdinalIgnoreCase)) return "SM";
        var known = Classes.FirstOrDefault(c => string.Equals(c, head, StringComparison.OrdinalIgnoreCase));
        return known;
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
                _ => ReactionKind.Preserving,
            };
            var genes = cells[4].Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            list.Add(new LipidReaction(cells[0].Trim(), cells[1].Trim(), cells[2].Trim(), kind, genes, cells.Length > 5 ? cells[5].Trim() : string.Empty));
        }
        return list;
    }
}
