using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace OpenDIAL.Pipeline.Results;

/// <summary>Which polarity a paired compound is quantified from.</summary>
public enum PolarityChoice
{
    Positive,
    Negative,
}

/// <summary>
/// One compound offered to the pairing, on one side of the polarity: when it eluted, what was
/// measured, and how it behaved across the injections that both polarities share.
/// </summary>
public sealed record PolarityCandidate(
    int Id,
    double Rt,
    double Mz,
    double Height,
    string Adduct,
    string Name,
    double SignalToNoise,
    IReadOnlyList<double> Heights);

/// <summary>Two features, one in each polarity, found to be the same neutral molecule.</summary>
public sealed record PolarityPair(
    int PositiveId,
    int NegativeId,
    double NeutralMass,
    string PositiveAdduct,
    string NegativeAdduct,
    double RtDifference,
    double MassDifference,
    double Correlation,
    double Score,
    PolarityChoice Quantify,
    string Explanation);

/// <summary>One injection of a sample seen in both polarities, and how the two files were matched.</summary>
public sealed record SamplePairing(
    int PositiveFileId,
    string PositiveFile,
    int NegativeFileId,
    string NegativeFile,
    string How);

/// <summary>What the pairing decided: what is shared, what is not, and which injections it compared.</summary>
public sealed record PolarityLinkResult(
    IReadOnlyList<PolarityPair> Pairs,
    IReadOnlyList<int> PositiveOnly,
    IReadOnlyList<int> NegativeOnly,
    IReadOnlyList<SamplePairing> Samples)
{
    /// <summary>Compounds after the merge: the shared ones counted once.</summary>
    public int Compounds => Pairs.Count + PositiveOnly.Count + NegativeOnly.Count;

    public string Sentence()
    {
        if (Compounds == 0) return "Nothing to pair.";
        return $"{Compounds:N0} compound(s) · {Pairs.Count:N0} in both polarities · " +
               $"{PositiveOnly.Count:N0} positive only · {NegativeOnly.Count:N0} negative only · " +
               $"{Samples.Count:N0} injection(s) compared";
    }
}

/// <summary>How exactly the two polarities have to agree before they are called one compound.</summary>
public sealed record PolarityLinkOptions
{
    /// <summary>How far apart the same compound may elute in the two runs, in minutes.</summary>
    public double RtTolerance { get; init; } = 0.1;

    /// <summary>How exactly the two neutral masses must agree, in daltons.</summary>
    public double MassTolerance { get; init; } = 0.01;

    /// <summary>
    /// Below this, the two height profiles contradict each other and the pair is refused. It is
    /// deliberately permissive: the same molecule ionises with a different efficiency in each
    /// polarity, so the profiles are alike in shape, not in size, and a strict gate throws real
    /// pairs away. It only has to kill the ones that disagree.
    /// </summary>
    public double MinimumCorrelation { get; init; } = 0.5;

    /// <summary>Below this combined score the offer is not made at all.</summary>
    public double MinimumScore { get; init; } = 0.5;

    /// <summary>Pair compounds, not ions: only the representative of each ion identity group is offered.</summary>
    public bool RepresentativesOnly { get; init; } = true;
}

/// <summary>
/// The same samples run twice, once in each polarity, reconciled into one set of compounds.
///
/// A phospholipid answers in positive and a free fatty acid in negative, so the two runs of one
/// batch are two halves of the same picture — and until now they were two projects, two alignments
/// and two reviews, joined by hand in a spreadsheet. What joins them is the neutral molecule: the
/// same compound is <c>[M+H]+</c> here and <c>[M-H]-</c> there, 2.0146 Da apart, at the same
/// retention time when the gradient is the same.
///
/// Three things have to agree, as with the ion identity grouping one polarity over: the neutral
/// masses meet, the compounds elute together, and their heights rise and fall together across the
/// injections. That last one is the strong signal here and it is one the published tools mostly do
/// not have: the two polarities are the <em>same</em> livers, so the profile is a signature.
/// </summary>
public static class PolarityLink
{
    // m/z * charge = neutral * multimer + shift, so neutral = (m/z * charge - shift) / multimer.
    // Charge carries the doubly charged ions and multimer the dimers; both appear in a real
    // alignment and a table that cannot express them silently drops those features.
    private static readonly (string Adduct, double Shift, int Multimer, int Charge)[] PositiveAdducts =
    {
        ("[M+H]+", 1.0072765, 1, 1),
        ("[M+NH4]+", 18.0338255, 1, 1),
        ("[M+Na]+", 22.9892211, 1, 1),
        ("[M+K]+", 38.9631583, 1, 1),
        ("[M]+", -0.0005486, 1, 1),               // the radical cation: an electron short, not a proton up
        ("[M+H-H2O]+", -17.0032881, 1, 1),
        ("[M+CH3OH+H]+", 33.0334912, 1, 1),
        ("[M+ACN+H]+", 42.0338256, 1, 1),
        ("[M+2H]2+", 2.0145530, 1, 2),
        ("[M+H+NH4]2+", 19.0411020, 1, 2),
        ("[M+H+Na]2+", 23.9964976, 1, 2),
        ("[2M+H]+", 1.0072765, 2, 1),
        ("[2M+NH4]+", 18.0338255, 2, 1),
        ("[2M+Na]+", 22.9892211, 2, 1),
    };

    private static readonly (string Adduct, double Shift, int Multimer, int Charge)[] NegativeAdducts =
    {
        ("[M-H]-", -1.0072765, 1, 1),
        ("[M+Cl]-", 34.9694013, 1, 1),
        ("[M+FA-H]-", 44.9982028, 1, 1),
        ("[M+Hac-H]-", 59.0138529, 1, 1),
        ("[M+Na-2H]-", 20.9746682, 1, 1),
        ("[M]-", 0.0005486, 1, 1),                // the radical anion
        ("[M-H-H2O]-", -19.0178411, 1, 1),
        ("[M-2H]2-", -2.0145530, 1, 2),
        ("[2M-H]-", -1.0072765, 2, 1),
        ("[2M+FA-H]-", 44.9982028, 2, 1),
    };

    /// <summary>What an unannotated feature is assumed to be, and what assuming it costs.</summary>
    private static readonly (string Adduct, double Penalty)[] AssumedPositive =
        { ("[M+H]+", 0), ("[M+NH4]+", 0.10), ("[M+Na]+", 0.10) };

    private static readonly (string Adduct, double Penalty)[] AssumedNegative =
        { ("[M-H]-", 0), ("[M+FA-H]-", 0.10), ("[M+Cl]-", 0.10) };

    private readonly record struct Offer(PolarityCandidate Candidate, double Neutral, string Adduct, double Penalty);

    /// <summary>
    /// Pairs two lists of compounds, one per polarity. The heights of both sides must already be in
    /// the same injection order — <see cref="PairSamples"/> is what puts them there.
    /// </summary>
    public static PolarityLinkResult Link(
        IReadOnlyList<PolarityCandidate> positive,
        IReadOnlyList<PolarityCandidate> negative,
        PolarityLinkOptions? options = null,
        IReadOnlyList<SamplePairing>? samples = null)
    {
        options ??= new PolarityLinkOptions();
        samples ??= Array.Empty<SamplePairing>();

        var left = positive.SelectMany(c => Neutrals(c, positive: true)).ToList();
        var right = negative.SelectMany(c => Neutrals(c, positive: false)).OrderBy(o => o.Neutral).ToList();

        var offers = new List<PolarityPair>();
        foreach (var p in left)
        {
            foreach (var n in Near(right, p.Neutral, options.MassTolerance))
            {
                var rtDifference = p.Candidate.Rt - n.Candidate.Rt;
                if (Math.Abs(rtDifference) > options.RtTolerance) continue;

                var correlation = IonIdentity.Correlation(p.Candidate.Heights, n.Candidate.Heights);
                if (!double.IsNaN(correlation) && correlation < options.MinimumCorrelation) continue;

                var massDifference = p.Neutral - n.Neutral;
                var score = Score(p, n, massDifference, rtDifference, correlation, options);
                if (score < options.MinimumScore) continue;

                var neutral = (p.Neutral + n.Neutral) / 2;
                offers.Add(new PolarityPair(
                    p.Candidate.Id, n.Candidate.Id, neutral, p.Adduct, n.Adduct,
                    rtDifference, massDifference, correlation, score,
                    Quantify(p.Candidate, n.Candidate),
                    Explain(p, n, massDifference, rtDifference, correlation)));
            }
        }

        // strongest first, and each feature is spoken for once: a compound has one partner, and the
        // second-best offer for it is a coincidence, not a second molecule
        var pairs = new List<PolarityPair>();
        var takenPositive = new HashSet<int>();
        var takenNegative = new HashSet<int>();
        foreach (var offer in offers.OrderByDescending(o => o.Score).ThenBy(o => Math.Abs(o.MassDifference)))
        {
            if (!takenPositive.Add(offer.PositiveId)) continue;
            if (!takenNegative.Add(offer.NegativeId)) { takenPositive.Remove(offer.PositiveId); continue; }
            pairs.Add(offer);
        }

        pairs.Sort((a, b) => a.NeutralMass.CompareTo(b.NeutralMass));
        return new PolarityLinkResult(
            pairs,
            positive.Where(c => !takenPositive.Contains(c.Id)).Select(c => c.Id).ToList(),
            negative.Where(c => !takenNegative.Contains(c.Id)).Select(c => c.Id).ToList(),
            samples);
    }

    /// <summary>
    /// Pairs two alignment results: matches the injections, reduces each side to its compounds, and
    /// links them. The caller says which table is which polarity — nothing here guesses it.
    /// </summary>
    public static PolarityLinkResult Link(AlignmentTable positive, AlignmentTable negative, PolarityLinkOptions? options = null)
    {
        options ??= new PolarityLinkOptions();
        var samples = PairSamples(positive.Samples, negative.Samples);
        return Link(
            Describe(positive, samples.Select(s => s.PositiveFileId).ToList(), wanted: true, options),
            Describe(negative, samples.Select(s => s.NegativeFileId).ToList(), wanted: false, options),
            options,
            samples);
    }

    /// <summary>The compounds of one alignment, with their heights in the order the pairing compares.</summary>
    private static IReadOnlyList<PolarityCandidate> Describe(AlignmentTable table, IReadOnlyList<int> fileOrder, bool wanted, PolarityLinkOptions options)
    {
        var spots = table.Spots;
        if (options.RepresentativesOnly && spots.Count > 0)
        {
            var candidates = spots.Select(s => new IonCandidate(
                s.Id, s.Rt, s.Mz, s.AverageHeight,
                HeightsInFileOrder(s), s.Adduct, Math.Max(0, s.IsotopeWeight))).ToList();
            var groups = IonIdentity.Group(candidates, positive: wanted);
            var representatives = groups.Where(g => g.IsRepresentative).Select(g => g.Id).ToHashSet();
            spots = spots.Where(s => representatives.Contains(s.Id)).ToList();
        }
        return spots.Select(s => new PolarityCandidate(
            s.Id, s.Rt, s.Mz, s.AverageHeight, s.Adduct, s.Name, s.SignalToNoiseAverage,
            HeightsFor(s, fileOrder))).ToList();
    }

    private static List<double> HeightsInFileOrder(AlignmentSpotRow spot)
    {
        if (spot.SamplePeaks.Count > 0) return spot.SamplePeaks.Select(p => double.IsNaN(p.Height) ? 0 : p.Height).ToList();
        return spot.SampleHeights.Select(h => double.IsNaN(h.Height) ? 0 : h.Height).ToList();
    }

    private static IReadOnlyList<double> HeightsFor(AlignmentSpotRow spot, IReadOnlyList<int> fileOrder)
    {
        var byFile = new Dictionary<int, double>();
        foreach (var h in spot.SampleHeights) byFile[h.FileId] = double.IsNaN(h.Height) ? 0 : h.Height;
        foreach (var p in spot.SamplePeaks) byFile[p.FileId] = double.IsNaN(p.Height) ? 0 : p.Height;
        return fileOrder.Select(id => byFile.TryGetValue(id, out var v) ? v : 0).ToList();
    }

    /// <summary>
    /// Matches the injections of the two polarities. The same liver is a different file in each run
    /// — "liver_01_pos" and "liver_01_neg" — so the names are compared with the polarity marker
    /// taken out of them, and whatever is left over is matched by class and injection order.
    /// </summary>
    public static IReadOnlyList<SamplePairing> PairSamples(IReadOnlyList<SampleInfo> positive, IReadOnlyList<SampleInfo> negative)
    {
        var pairs = new List<SamplePairing>();
        var free = negative.ToList();

        foreach (var p in positive)
        {
            var key = Normalise(p.FileName);
            if (key.Length == 0) continue;
            var match = free.FirstOrDefault(n => Normalise(n.FileName) == key);
            if (match is null) continue;
            free.Remove(match);
            pairs.Add(new SamplePairing(p.FileId, p.FileName, match.FileId, match.FileName, "name"));
        }

        var unmatched = positive.Where(p => pairs.All(x => x.PositiveFileId != p.FileId)).ToList();
        foreach (var p in unmatched)
        {
            var match = free.FirstOrDefault(n =>
                string.Equals(n.Class, p.Class, StringComparison.OrdinalIgnoreCase) &&
                n.InjectionOrder == p.InjectionOrder);
            if (match is null) continue;
            free.Remove(match);
            pairs.Add(new SamplePairing(p.FileId, p.FileName, match.FileId, match.FileName, "class and injection order"));
        }

        return pairs.OrderBy(x => x.PositiveFileId).ToList();
    }

    private static readonly Regex Marker = new(@"(?<![a-z0-9])(positive|negative|pos|neg)(?![a-z0-9])", RegexOptions.Compiled);

    /// <summary>A file name with the polarity marker and the punctuation taken out of it.</summary>
    internal static string Normalise(string fileName)
    {
        var text = Path.GetFileNameWithoutExtension(fileName ?? string.Empty).ToLowerInvariant();
        text = Marker.Replace(text, string.Empty);
        return new string(text.Where(char.IsLetterOrDigit).ToArray());
    }

    /// <summary>
    /// The neutral molecule behind an ion, when the run said which adduct it is. Answers false for
    /// an adduct nothing here knows, which is the caller's cue that the m/z is all there is.
    /// </summary>
    public static bool TryNeutral(double mz, string? adduct, out double neutral)
    {
        neutral = 0;
        var trimmed = adduct?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return false;
        foreach (var table in new[] { PositiveAdducts, NegativeAdducts })
        {
            foreach (var entry in table)
            {
                if (!string.Equals(entry.Adduct, trimmed, StringComparison.OrdinalIgnoreCase)) continue;
                neutral = (mz * entry.Charge - entry.Shift) / entry.Multimer;
                return true;
            }
        }
        return false;
    }

    /// <summary>The neutral masses a feature could stand for, and what each assumption costs.</summary>
    private static IEnumerable<Offer> Neutrals(PolarityCandidate candidate, bool positive)
    {
        var table = positive ? PositiveAdducts : NegativeAdducts;
        var known = table.FirstOrDefault(a => string.Equals(a.Adduct, candidate.Adduct?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (known.Adduct is not null)
        {
            // the run said what this ion is; take it at its word
            yield return new Offer(candidate, (candidate.Mz * known.Charge - known.Shift) / known.Multimer, known.Adduct, 0);
            yield break;
        }
        foreach (var (adduct, penalty) in positive ? AssumedPositive : AssumedNegative)
        {
            var entry = table.First(a => a.Adduct == adduct);
            yield return new Offer(candidate, (candidate.Mz * entry.Charge - entry.Shift) / entry.Multimer, adduct, penalty);
        }
    }

    private static double Score(Offer p, Offer n, double massDifference, double rtDifference, double correlation, PolarityLinkOptions options)
    {
        var mass = 1 - Math.Abs(massDifference) / options.MassTolerance;
        var rt = 1 - Math.Abs(rtDifference) / options.RtTolerance;
        var score = double.IsNaN(correlation)
            ? 0.60 * mass + 0.40 * rt
            : 0.30 * mass + 0.25 * rt + 0.45 * Math.Clamp(correlation, 0, 1);
        if (SameName(p.Candidate.Name, n.Candidate.Name)) score += 0.2;   // both runs named it, and named it the same
        score -= p.Penalty + n.Penalty;
        return Math.Clamp(score, 0, 1.2);
    }

    private static bool SameName(string a, string b) =>
        !string.IsNullOrWhiteSpace(a) && !string.IsNullOrWhiteSpace(b) &&
        !a.StartsWith("Unknown", StringComparison.OrdinalIgnoreCase) &&
        !a.StartsWith("w/o", StringComparison.OrdinalIgnoreCase) &&
        string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Which polarity quantifies the compound: the one that measured it better. Intensities are
    /// never added across polarities — the ionisation efficiencies are different and the sum means
    /// nothing — so one side quantifies and the other confirms.
    /// </summary>
    private static PolarityChoice Quantify(PolarityCandidate positive, PolarityCandidate negative)
    {
        var p = double.IsNaN(positive.SignalToNoise) ? 0 : positive.SignalToNoise;
        var n = double.IsNaN(negative.SignalToNoise) ? 0 : negative.SignalToNoise;
        if (Math.Abs(p - n) > 1e-9) return p > n ? PolarityChoice.Positive : PolarityChoice.Negative;
        return positive.Height >= negative.Height ? PolarityChoice.Positive : PolarityChoice.Negative;
    }

    private static string Explain(Offer p, Offer n, double massDifference, double rtDifference, double correlation)
    {
        var r = double.IsNaN(correlation) ? "no shared injections" : $"r {correlation:0.00}";
        return $"{p.Adduct} {p.Candidate.Mz:0.0000} ↔ {n.Adduct} {n.Candidate.Mz:0.0000} · " +
               $"Δ {massDifference * 1000:0.#} mDa · Δ {rtDifference:0.###} min · {r}";
    }

    /// <summary>The window of the sorted list around a neutral mass.</summary>
    private static IEnumerable<Offer> Near(IReadOnlyList<Offer> byNeutral, double neutral, double tolerance)
    {
        var low = 0;
        var high = byNeutral.Count - 1;
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (byNeutral[middle].Neutral < neutral - tolerance) low = middle + 1; else high = middle - 1;
        }
        for (var i = low; i < byNeutral.Count && byNeutral[i].Neutral <= neutral + tolerance; i++) yield return byNeutral[i];
    }
}

/// <summary>
/// The pairing, written beside the alignment it belongs to.
///
/// It goes to "&lt;positive alignment&gt;_polarity-pairs.json" — the same habit as the curation
/// sidecar: plain JSON, readable, diffable, and never a change to the binary result files. It holds
/// the tolerances it was made with, because a pairing whose parameters are lost is not a result.
/// </summary>
public static class PolarityPairFile
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string FileFor(string positiveAlignmentPath) =>
        Path.Combine(Path.GetDirectoryName(positiveAlignmentPath) ?? string.Empty,
                     Path.GetFileNameWithoutExtension(positiveAlignmentPath) + "_polarity-pairs.json");

    /// <summary>Everything the sidecar holds.</summary>
    public sealed record Document(
        int Version,
        string PositiveAlignment,
        string NegativeAlignment,
        double RtTolerance,
        double MassTolerance,
        double MinimumCorrelation,
        double MinimumScore,
        string Created,
        PolarityLinkResult Result);

    public static void Save(string path, PolarityLinkResult result, string positiveAlignment, string negativeAlignment, PolarityLinkOptions options)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
        var document = new Document(
            1,
            Relative(folder, positiveAlignment),
            Relative(folder, negativeAlignment),
            options.RtTolerance,
            options.MassTolerance,
            options.MinimumCorrelation,
            options.MinimumScore,
            DateTime.UtcNow.ToString("O"),
            result);
        Directory.CreateDirectory(folder);
        using var stream = File.Create(path);
        JsonSerializer.Serialize(stream, document, JsonOptions);
    }

    /// <summary>The pairing as it was saved, or null when there is none to read.</summary>
    public static Document? Load(string path)
    {
        if (!File.Exists(path)) return null;
        try
        {
            using var stream = File.OpenRead(path);
            var document = JsonSerializer.Deserialize<Document>(stream, JsonOptions);
            if (document is null) return null;
            var folder = Path.GetDirectoryName(Path.GetFullPath(path)) ?? string.Empty;
            return document with
            {
                PositiveAlignment = Absolute(folder, document.PositiveAlignment),
                NegativeAlignment = Absolute(folder, document.NegativeAlignment),
            };
        }
        catch (Exception)
        {
            // the sidecar is a convenience; a broken one is worth nothing but must cost nothing either
            return null;
        }
    }

    private static string Relative(string folder, string path) =>
        string.IsNullOrEmpty(path) || string.IsNullOrEmpty(folder) ? path : Path.GetRelativePath(folder, path);

    private static string Absolute(string folder, string path) =>
        string.IsNullOrEmpty(path) || Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(folder, path));
}
