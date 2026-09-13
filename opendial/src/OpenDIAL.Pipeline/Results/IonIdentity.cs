namespace OpenDIAL.Pipeline.Results;

/// <summary>One feature as the grouping sees it: when it eluted, its mass, and its height per injection.</summary>
public sealed record IonCandidate(int Id, double Rt, double Mz, double Height, IReadOnlyList<double> Heights, string Adduct, int IsotopeWeight);

/// <summary>
/// What a feature turned out to be: the compound it belongs to, and why it was put there.
/// </summary>
public sealed record IonGroup(int Id, int RepresentativeId, string Relation, string Explanation)
{
    public bool IsRepresentative => Id == RepresentativeId;
}

/// <summary>
/// Adducts, isotopes and in-source fragments of one compound, gathered into one group.
///
/// An untargeted run does not report compounds, it reports ions: the protonated molecule, its
/// sodium and ammonium adducts, its isotopes, whatever fell apart in the source, each as a feature
/// of its own. A liver batch of two and a half thousand features is a few hundred compounds seen
/// several times over, and until now the only way to know that was to notice it by eye and write
/// Overannotation in the margin.
///
/// Three things have to agree before two features are called the same compound: they elute
/// together, their heights rise and fall together across the injections, and the distance between
/// their masses is one a real adduct pair would give. Correlation alone groups everything in a
/// crowded region; mass alone groups coincidences.
/// </summary>
public static class IonIdentity
{
    /// <summary>The mass differences an adduct pair of the same neutral gives, in the positive mode.</summary>
    private static readonly (string From, string To, double Delta)[] PositivePairs =
    {
        ("[M+H]+", "[M+Na]+", 21.981944),
        ("[M+H]+", "[M+NH4]+", 17.026549),
        ("[M+H]+", "[M+K]+", 37.955882),
        ("[M+NH4]+", "[M+Na]+", 4.955395),
        ("[M+H]+", "[M+CH3OH+H]+", 32.026215),
        ("[M+H]+", "[M+ACN+H]+", 41.026549),
        ("[M+H]+", "[2M+H]+", double.NaN),      // handled by the multimer rule
    };

    /// <summary>The same for the negative mode.</summary>
    private static readonly (string From, string To, double Delta)[] NegativePairs =
    {
        ("[M-H]-", "[M+Cl]-", 35.976678),
        ("[M-H]-", "[M+FA-H]-", 46.005479),
        ("[M-H]-", "[M+Hac-H]-", 60.021129),
        ("[M-H]-", "[M+Na-2H]-", 21.981944),
    };

    /// <summary>
    /// Groups the features. <paramref name="rtTolerance"/> is how close together two ions of one
    /// compound must elute, <paramref name="mzTolerance"/> how exactly the adduct distance must
    /// hold, and <paramref name="minimumCorrelation"/> how alike their heights must be across the
    /// injections — a compound's adducts rise and fall together because they are the same molecule.
    /// </summary>
    public static IReadOnlyList<IonGroup> Group(
        IReadOnlyList<IonCandidate> features,
        bool positive,
        double rtTolerance = 0.05,
        double mzTolerance = 0.01,
        double minimumCorrelation = 0.8)
    {
        var groups = new Dictionary<int, IonGroup>();
        if (features.Count == 0) return Array.Empty<IonGroup>();

        // strongest first: a compound is represented by the ion the run measured best
        var order = features.OrderByDescending(f => f.Height).ToList();
        var taken = new Dictionary<int, int>();   // feature id -> representative id
        var byRt = features.OrderBy(f => f.Rt).ToList();
        var pairs = positive ? PositivePairs : NegativePairs;

        foreach (var lead in order)
        {
            if (taken.ContainsKey(lead.Id)) continue;
            taken[lead.Id] = lead.Id;
            groups[lead.Id] = new IonGroup(lead.Id, lead.Id, "representative", string.Empty);

            foreach (var other in Near(byRt, lead.Rt, rtTolerance))
            {
                if (other.Id == lead.Id || taken.ContainsKey(other.Id)) continue;
                var correlation = Correlation(lead.Heights, other.Heights);
                if (double.IsNaN(correlation) || correlation < minimumCorrelation) continue;

                var (relation, why) = Relate(lead, other, pairs, mzTolerance);
                if (relation is null) continue;
                taken[other.Id] = lead.Id;
                groups[other.Id] = new IonGroup(other.Id, lead.Id, relation, why!);
            }
        }
        foreach (var f in features)
        {
            if (!groups.ContainsKey(f.Id)) groups[f.Id] = new IonGroup(f.Id, f.Id, "representative", string.Empty);
        }
        return features.Select(f => groups[f.Id]).ToList();
    }

    /// <summary>Why these two are one compound, or null when they are not.</summary>
    private static (string? Relation, string? Why) Relate(IonCandidate lead, IonCandidate other, (string From, string To, double Delta)[] pairs, double tolerance)
    {
        // an isotope the run already marked, sitting a neutron above its monoisotopic peak
        if (other.IsotopeWeight > 0 && Math.Abs(other.Mz - lead.Mz - 1.00336 * other.IsotopeWeight) < tolerance * 2)
        {
            return ("isotope", $"M+{other.IsotopeWeight} of #{lead.Id}");
        }
        foreach (var (from, to, delta) in pairs)
        {
            if (double.IsNaN(delta)) continue;
            if (Math.Abs(other.Mz - lead.Mz - delta) < tolerance) return ("adduct", $"{to} where #{lead.Id} is {from}");
            if (Math.Abs(lead.Mz - other.Mz - delta) < tolerance) return ("adduct", $"{from} where #{lead.Id} is {to}");
        }
        // a dimer of the same neutral, or the monomer of a dimer
        var monomer = lead.Mz - 1.007276;
        if (Math.Abs(other.Mz - (lead.Mz + monomer)) < tolerance * 2) return ("multimer", $"dimer of #{lead.Id}");
        // a loss of water or ammonia in the source, which is the commonest in-source fragment
        foreach (var (loss, name) in new[] { (18.010565, "water"), (17.026549, "ammonia") })
        {
            if (Math.Abs(lead.Mz - other.Mz - loss) < tolerance) return ("in-source", $"#{lead.Id} less {name}");
        }
        return (null, null);
    }

    private static IEnumerable<IonCandidate> Near(IReadOnlyList<IonCandidate> byRt, double rt, double tolerance)
    {
        // the list is sorted by retention time, so the neighbourhood is a window, not a scan
        var low = 0;
        var high = byRt.Count - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (byRt[middle].Rt < rt - tolerance) low = middle + 1; else high = middle - 1;
        }
        for (var i = low; i < byRt.Count && byRt[i].Rt <= rt + tolerance; i++) yield return byRt[i];
    }

    /// <summary>Pearson correlation of two height profiles, NaN when either is flat.</summary>
    public static double Correlation(IReadOnlyList<double> a, IReadOnlyList<double> b)
    {
        var n = Math.Min(a.Count, b.Count);
        if (n < 3) return double.NaN;
        double sa = 0, sb = 0;
        for (var i = 0; i < n; i++)
        {
            sa += double.IsNaN(a[i]) ? 0 : a[i];
            sb += double.IsNaN(b[i]) ? 0 : b[i];
        }
        var ma = sa / n;
        var mb = sb / n;
        double num = 0, da = 0, db = 0;
        for (var i = 0; i < n; i++)
        {
            var x = (double.IsNaN(a[i]) ? 0 : a[i]) - ma;
            var y = (double.IsNaN(b[i]) ? 0 : b[i]) - mb;
            num += x * y;
            da += x * x;
            db += y * y;
        }
        if (da <= 1e-12 || db <= 1e-12) return double.NaN;
        return num / Math.Sqrt(da * db);
    }
}
