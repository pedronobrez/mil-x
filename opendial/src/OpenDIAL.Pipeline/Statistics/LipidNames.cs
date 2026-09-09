using System.Text.RegularExpressions;

namespace OpenDIAL.Pipeline.Statistics;

/// <summary>What a lipid name says about the molecule: its class, and the chains summed up.</summary>
public sealed record LipidIdentity(string Class, int Carbons, int DoubleBonds, bool IsStandard)
{
    /// <summary>"34:1" style sum composition, or empty when the name carried no chains.</summary>
    public string SumComposition => Carbons > 0 ? $"{Carbons}:{DoubleBonds}" : string.Empty;
}

/// <summary>
/// Reads the chains out of the names MS-DIAL writes — "PC 34:1", "PC 16:0/18:1", "TG 16:0_18:1_18:1",
/// "Cer 18:1;O2/16:0", "PC O-34:1", "SM d18:1/16:0", "AHexCer d(O-14:0)16:1/14:0;O" — so a feature
/// can be placed by its total carbon number and its double bonds, and recognises the deuterated and
/// odd-chain standards a lipidomics run is spiked with.
/// </summary>
public static class LipidNames
{
    private static readonly Regex Chain = new(@"(?<![\d:])(\d{1,2}):(\d{1,2})(?![\d:])", RegexOptions.Compiled);
    private static readonly Regex Deuterium = new(@"\(?\bd\d{1,2}\)?(?![:\d])|\bd\d{1,2}\b(?!:)|-d\d{1,2}\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex Prefix = new(@"^(low score:|no MS2:|w/o MS2:)\s*", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>The identity read off a name; the class falls back to <paramref name="ontology"/> when given.</summary>
    public static LipidIdentity Parse(string name, string? ontology = null)
    {
        name = Prefix.Replace(name ?? string.Empty, string.Empty).Trim();
        var pipe = name.IndexOf('|');
        var species = pipe > 0 ? name[..pipe].Trim() : name;    // "TG 52:2|TG 16:0_18:1_18:1": the sum composition is enough
        var space = species.IndexOf(' ');
        var cls = space > 0 ? species[..space] : species;
        if (!string.IsNullOrWhiteSpace(ontology)) cls = ontology;
        var isStandard = IsStandardName(name);

        // "SM d18:1/16:0": the d before the chain is the sphingoid base, not deuterium; strip that
        // form so the chain regex sees plain pairs, then sum what is left
        var chains = species.Length > space + 1 ? species[(space + 1)..] : string.Empty;
        chains = Regex.Replace(chains, @"\bd(?=\d{1,2}:)", string.Empty);
        chains = Regex.Replace(chains, @"\(O-(\d{1,2}):(\d{1,2})\)", " $1:$2 ");   // AHexCer d(O-14:0)…
        var carbons = 0;
        var doubleBonds = 0;
        foreach (Match m in Chain.Matches(chains))
        {
            carbons += int.Parse(m.Groups[1].Value);
            doubleBonds += int.Parse(m.Groups[2].Value);
        }
        return new LipidIdentity(cls, carbons, doubleBonds, isStandard);
    }

    /// <summary>
    /// A spiked-in standard, by the marks a name carries: a deuterium label ("d7", "(d7)", "-d9"),
    /// "IS" or "SPLASH", or the odd-chain acyls the commercial mixes use where nothing says deuterium.
    /// </summary>
    public static bool IsStandardName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        if (Deuterium.IsMatch(name.Replace("d18:", "x18:").Replace("d17:", "x17:").Replace("d16:", "x16:").Replace("d19:", "x19:").Replace("d20:", "x20:").Replace("d14:", "x14:").Replace("d15:", "x15:").Replace("d9:", "x9:"))) return true;
        if (Regex.IsMatch(name, @"\bIS\b|SPLASH|internal standard", RegexOptions.IgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// How well a feature would serve as the standard for a class: a deuterated standard of the
    /// same class first, then any standard, then an odd-chain species of the class, then nothing.
    /// </summary>
    public static int StandardScore(string name, string ontology, string forClass)
    {
        var identity = Parse(name, ontology);
        var sameClass = string.Equals(identity.Class, forClass, StringComparison.OrdinalIgnoreCase);
        if (identity.IsStandard) return sameClass ? 100 : 20;   // another class's standard is offered, never suggested
        // odd total carbon count: 15:0, 17:0 and 19:0 chains are what the unlabeled standards carry
        if (sameClass && identity.Carbons > 0 && HasOddChain(name)) return 40;
        return 0;
    }

    /// <summary>
    /// The chains a name resolves, as "16:0"-style tokens, taken from the molecular-species part
    /// ("TG 16:0_18:1_18:1" after the bar in "TG 52:2|TG 16:0_18:1_18:1"); a sum composition alone
    /// ("PC 34:1") gives one token, which is only a chain for a one-chain class. Sphingolipid bases
    /// count as chains ("Cer 18:1;O2/16:0" gives 18:1 and 16:0).
    /// </summary>
    public static IReadOnlyList<string> Chains(string name)
    {
        name = Prefix.Replace(name ?? string.Empty, string.Empty).Trim();
        var pipe = name.IndexOf('|');
        var species = pipe > 0 ? name[(pipe + 1)..].Trim() : name;
        var space = species.IndexOf(' ');
        if (space < 0) return Array.Empty<string>();
        var chains = species[(space + 1)..];
        chains = Regex.Replace(chains, @"\bd(?=\d{1,2}:)", string.Empty);
        chains = Regex.Replace(chains, @"\(O-(\d{1,2}):(\d{1,2})\)", " $1:$2 ");
        chains = Regex.Replace(chains, @"\b[OP]-(?=\d)", string.Empty);   // "PC O-16:0_18:1": the ether mark, not a chain
        return Chain.Matches(chains).Select(m => $"{int.Parse(m.Groups[1].Value)}:{int.Parse(m.Groups[2].Value)}").ToList();
    }

    /// <summary>How many chains a class carries, for telling a resolved species from a sum composition.</summary>
    public static int ChainCount(string cls) => cls.ToUpperInvariant() switch
    {
        "TG" => 3,
        "CL" => 4,
        "LPC" or "LPE" or "LPS" or "LPG" or "LPI" or "LPA" or "MG" or "CE" or "FA" or "CAR" or "SPH" or "S1P" or "LPC O-" or "LPE O-" => 1,
        _ => 2,
    };

    private static bool HasOddChain(string name) =>
        Chain.Matches(name).Any(m => int.Parse(m.Groups[1].Value) % 2 == 1);
}
