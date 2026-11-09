using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MilX.Pipeline.Chemistry;

/// <summary>A formula that cannot be read, or names an element the table does not have.</summary>
public sealed class FormulaException : FormatException
{
    public FormulaException(string message) : base(message) { }
}

/// <summary>
/// Elemental composition: formula parsing, exact masses, the ring-and-double-bond equivalent, and the
/// isotope pattern. The element table and the arithmetic are MIL-Q's (chemistry.py), so the two
/// applications give the same numbers for the same formula; a test holds them to a fixed list.
/// Masses are monoisotopic unless stated; abundances are the IUPAC 2013 representative compositions.
/// </summary>
public static class Formula
{
    public const double ElectronMass = 0.000548579909;
    public const double ProtonMass = 1.007276466879;

    /// <summary>element → (isotope mass, natural abundance), most abundant first. D is deuterium.</summary>
    private static readonly Dictionary<string, (double Mass, double Abundance)[]> Isotopes = new(StringComparer.Ordinal)
    {
        ["H"] = new[] { (1.00782503207, 0.999885), (2.01410177785, 0.000115) },
        ["D"] = new[] { (2.01410177785, 1.0) },
        ["C"] = new[] { (12.0, 0.9893), (13.00335483778, 0.0107) },
        ["N"] = new[] { (14.0030740048, 0.99636), (15.0001088982, 0.00364) },
        ["O"] = new[] { (15.9949146196, 0.99757), (16.9991317012, 0.00038), (17.9991596129, 0.00205) },
        ["F"] = new[] { (18.99840322, 1.0) },
        ["Na"] = new[] { (22.9897692809, 1.0) },
        ["Si"] = new[] { (27.9769265325, 0.92223), (28.976494700, 0.04685), (29.973770171, 0.03092) },
        ["P"] = new[] { (30.97376163, 1.0) },
        ["S"] = new[] { (31.97207100, 0.9499), (32.97145876, 0.0075), (33.96786690, 0.0425), (35.96708076, 0.0001) },
        ["Cl"] = new[] { (34.96885268, 0.7576), (36.96590259, 0.2424) },
        ["K"] = new[] { (38.96370668, 0.932581), (39.96399848, 0.000117), (40.96182576, 0.067302) },
        ["Br"] = new[] { (78.9183371, 0.5069), (80.9162906, 0.4931) },
        ["I"] = new[] { (126.904473, 1.0) },
        ["Se"] = new[] { (79.9165213, 0.4961), (77.9173091, 0.2377), (81.9166994, 0.0873), (76.919914, 0.0763), (75.9192134, 0.0937), (73.9224764, 0.0089) },
    };

    /// <summary>Valence used for the ring-plus-double-bond equivalent.</summary>
    private static readonly Dictionary<string, int> Valence = new(StringComparer.Ordinal)
    {
        ["H"] = 1, ["D"] = 1, ["F"] = 1, ["Cl"] = 1, ["Br"] = 1, ["I"] = 1, ["Na"] = 1, ["K"] = 1,
        ["O"] = 2, ["S"] = 2, ["Se"] = 2,
        ["N"] = 3, ["P"] = 3,
        ["C"] = 4, ["Si"] = 4,
    };

    private static readonly Regex Token = new(@"\G(\[\d+[A-Z][a-z]?\]|[A-Z][a-z]?)(\d*)", RegexOptions.Compiled);
    private static readonly Regex Bracket = new(@"^\[(\d+)([A-Z][a-z]?)\]$", RegexOptions.Compiled);

    public static IReadOnlyCollection<string> Elements => Isotopes.Keys;

    /// <summary>The isotopes of an element or of a bracketed isotope token; the key of a single isotope is the token itself.</summary>
    private static (double Mass, double Abundance)[] IsotopesOf(string key)
    {
        if (Isotopes.TryGetValue(key, out var table)) return table;
        var m = Bracket.Match(key);
        if (!m.Success) throw new FormulaException($"unknown element: {key}");
        var nucleons = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
        var element = m.Groups[2].Value;
        if (!Isotopes.TryGetValue(element, out var parent)) throw new FormulaException($"unknown element: {element}");
        foreach (var (mass, _) in parent)
        {
            if ((int)Math.Round(mass) == nucleons) return new[] { (mass, 1.0) };
        }
        throw new FormulaException($"{element} has no isotope with mass number {nucleons}");
    }

    private static int ValenceOf(string key)
    {
        if (Valence.TryGetValue(key, out var v)) return v;
        var m = Bracket.Match(key);
        return m.Success && Valence.TryGetValue(m.Groups[2].Value, out v) ? v : 2;
    }

    /// <summary>
    /// Parses a molecular formula into element counts, in the order the elements first appear. Groups
    /// with multipliers (<c>Ca(NO3)2</c>) and single isotopes in brackets (<c>[13C]</c>, or <c>D</c> for
    /// deuterium) are accepted; counts of zero are dropped.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, int>> Parse(string formula)
    {
        var text = (formula ?? string.Empty).Trim().Replace(" ", string.Empty);
        var counts = new List<KeyValuePair<string, int>>();
        if (text.Length == 0) return counts;
        var stack = new Stack<List<KeyValuePair<string, int>>>();
        var index = 0;
        while (index < text.Length)
        {
            var c = text[index];
            if (c == '(')
            {
                stack.Push(counts);
                counts = new List<KeyValuePair<string, int>>();
                index++;
                continue;
            }
            if (c == ')')
            {
                if (stack.Count == 0) throw new FormulaException("unbalanced parentheses");
                index++;
                var digits = new StringBuilder();
                while (index < text.Length && char.IsDigit(text[index])) digits.Append(text[index++]);
                var multiplier = digits.Length > 0 ? int.Parse(digits.ToString(), CultureInfo.InvariantCulture) : 1;
                var inner = counts;
                counts = stack.Pop();
                foreach (var (element, n) in inner) Add(counts, element, n * multiplier);
                continue;
            }
            var m = Token.Match(text, index);
            if (!m.Success) throw new FormulaException($"cannot read '{formula}' at position {index}");
            var key = m.Groups[1].Value;
            _ = IsotopesOf(key);   // validates
            var count = m.Groups[2].Value.Length > 0 ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) : 1;
            Add(counts, key, count);
            index = m.Length + index;
        }
        if (stack.Count > 0) throw new FormulaException("unbalanced parentheses");
        return counts.Where(kv => kv.Value != 0).ToList();
    }

    private static void Add(List<KeyValuePair<string, int>> counts, string element, int n)
    {
        var i = counts.FindIndex(kv => kv.Key == element);
        if (i < 0) counts.Add(new KeyValuePair<string, int>(element, n));
        else counts[i] = new KeyValuePair<string, int>(element, counts[i].Value + n);
    }

    /// <summary>Hill order: C, H, then the rest alphabetically (ordinal, so a bracketed isotope sorts last).</summary>
    public static string Format(IReadOnlyList<KeyValuePair<string, int>> counts)
    {
        var sb = new StringBuilder();
        var remaining = counts.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.Ordinal);
        foreach (var element in new[] { "C", "H" })
        {
            if (remaining.Remove(element, out var n) && n != 0) sb.Append(n == 1 ? element : element + n.ToString(CultureInfo.InvariantCulture));
        }
        foreach (var element in remaining.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var n = remaining[element];
            if (n != 0) sb.Append(n == 1 ? element : element + n.ToString(CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    /// <summary>Mass built from the most abundant isotope of each element.</summary>
    public static double MonoisotopicMass(IReadOnlyList<KeyValuePair<string, int>> counts)
    {
        double total = 0;
        foreach (var (element, n) in counts) total += IsotopesOf(element)[0].Mass * n;
        return total;
    }

    /// <summary>Mass weighted by natural abundance, as used for large molecules.</summary>
    public static double AverageMass(IReadOnlyList<KeyValuePair<string, int>> counts)
    {
        double total = 0;
        foreach (var (element, n) in counts)
        {
            var isotopes = IsotopesOf(element);
            var weight = isotopes.Sum(i => i.Abundance);
            if (weight == 0) weight = 1;
            total += n * isotopes.Sum(i => i.Mass * i.Abundance) / weight;
        }
        return total;
    }

    /// <summary>Rings plus double bonds. A half-integer means an odd-electron species, which for an even-electron ion signals an impossible formula.</summary>
    public static double Rdbe(IReadOnlyList<KeyValuePair<string, int>> counts)
    {
        var total = 2.0;
        foreach (var (element, n) in counts) total += n * (ValenceOf(element) - 2);
        return total / 2.0;
    }

    public static double MassErrorPpm(double measured, double theoretical) => theoretical == 0 ? 0 : (measured - theoretical) / theoretical * 1e6;
    public static double MassErrorMda(double measured, double theoretical) => (measured - theoretical) * 1000.0;

    /// <summary>
    /// The theoretical isotope pattern as (m/z, relative abundance), the base peak scaled to 1. The
    /// elements are convolved one by one with binary exponentiation; <paramref name="mergeTolerance"/>
    /// collapses the fine structure a given resolving power cannot separate (at 0.01 Da the 13C and 15N
    /// peaks of one nominal mass come out as one, which is what a TOF records). With an adduct the
    /// masses are the ion's m/z.
    /// </summary>
    public static IReadOnlyList<(double Mz, double Abundance)> IsotopePattern(IReadOnlyList<KeyValuePair<string, int>> counts, Adduct? adduct = null,
        double minAbundance = 0.001, int maxPeaks = 12, double mergeTolerance = 0.01)
    {
        if (counts.Count == 0) return Array.Empty<(double, double)>();
        var prune = minAbundance * 1e-3;
        var distribution = new List<(double Mass, double Abundance)> { (0.0, 1.0) };
        foreach (var (element, n) in counts)
        {
            var isotopes = IsotopesOf(element);
            if (isotopes.Length == 1)
            {
                distribution = distribution.Select(d => (d.Mass + isotopes[0].Mass * n, d.Abundance)).ToList();
                continue;
            }
            var total = isotopes.Sum(i => i.Abundance);
            var normalised = isotopes.Select(i => (i.Mass, i.Abundance / total)).ToList();
            var power = normalised;
            var remaining = n;
            var result = new List<(double Mass, double Abundance)> { (0.0, 1.0) };
            while (remaining != 0)
            {
                if ((remaining & 1) == 1) result = Merge(Convolve(result, power, prune), mergeTolerance / 4);
                remaining >>= 1;
                if (remaining != 0) power = Merge(Convolve(power, power, prune), mergeTolerance / 4);
            }
            distribution = Merge(Convolve(distribution, result, prune), mergeTolerance / 4);
        }
        var peaks = Merge(distribution, mergeTolerance);
        var top = peaks.Max(p => p.Abundance);
        var scaled = peaks.Select(p => (p.Mass, p.Abundance / top)).Where(p => p.Item2 >= minAbundance).OrderBy(p => p.Mass).Take(maxPeaks).ToList();
        if (adduct is not null) scaled = scaled.Select(p => (adduct.Mz(p.Mass), p.Item2)).ToList();
        return scaled;
    }

    private static List<(double Mass, double Abundance)> Convolve(List<(double Mass, double Abundance)> a, List<(double Mass, double Abundance)> b, double prune)
    {
        var outList = new List<(double, double)>(a.Count * b.Count);
        foreach (var (ma, aa) in a)
        {
            foreach (var (mb, ab) in b)
            {
                var abundance = aa * ab;
                if (abundance >= prune) outList.Add((ma + mb, abundance));
            }
        }
        return outList;
    }

    /// <summary>Collapses peaks closer than the tolerance into their weighted centroid.</summary>
    private static List<(double Mass, double Abundance)> Merge(List<(double Mass, double Abundance)> peaks, double tolerance)
    {
        if (peaks.Count == 0) return peaks;
        peaks.Sort((x, y) => x.Mass != y.Mass ? x.Mass.CompareTo(y.Mass) : x.Abundance.CompareTo(y.Abundance));
        var merged = new List<(double Mass, double Abundance)> { peaks[0] };
        for (var i = 1; i < peaks.Count; i++)
        {
            var (mass, abundance) = peaks[i];
            var last = merged[^1];
            if (mass - last.Mass <= tolerance)
            {
                var total = last.Abundance + abundance;
                merged[^1] = (total != 0 ? (last.Mass * last.Abundance + mass * abundance) / total : mass, total);
            }
            else merged.Add((mass, abundance));
        }
        return merged;
    }
}

/// <summary>An ionisation form: m/z = (M + delta) / |charge|. The table is MIL-Q's, both polarities, and the electron is accounted for.</summary>
public sealed record Adduct(string Name, int Charge, double Delta)
{
    public double Mz(double neutralMass) => (neutralMass + Delta) / Math.Abs(Charge);
    public double NeutralMass(double mz) => mz * Math.Abs(Charge) - Delta;
    public int Polarity => Charge > 0 ? 1 : -1;
    public override string ToString() => Name;

    public static readonly IReadOnlyList<Adduct> All = new[]
    {
        new Adduct("[M-H]-", -1, -Formula.ProtonMass),
        new Adduct("[M+Cl]-", -1, 34.96885268 + Formula.ElectronMass),
        new Adduct("[M+HCOO]-", -1, 44.99765396),
        new Adduct("[M+CH3COO]-", -1, 59.01330402),
        new Adduct("[M-2H]2-", -2, -2 * Formula.ProtonMass),
        new Adduct("[M+H]+", 1, Formula.ProtonMass),
        new Adduct("[M+NH4]+", 1, 18.03382555),
        new Adduct("[M+Na]+", 1, 22.98922421),
        new Adduct("[M+K]+", 1, 38.96315810),
        new Adduct("[M+2H]2+", 2, 2 * Formula.ProtonMass),
    };

    public static IReadOnlyList<string> Names { get; } = All.Select(a => a.Name).ToList();

    /// <summary>By name, tolerant of spaces, missing brackets and a "1" before the sign; null when unknown.</summary>
    public static Adduct? ByName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var key = Normalise(name);
        return All.FirstOrDefault(a => Normalise(a.Name) == key);
    }

    private static string Normalise(string text)
    {
        var t = text.Replace(" ", string.Empty).Replace("[", string.Empty).Replace("]", string.Empty).ToUpperInvariant();
        if (t.EndsWith("1+", StringComparison.Ordinal) || t.EndsWith("1-", StringComparison.Ordinal)) t = t[..^2] + t[^1];
        return t;
    }
}
