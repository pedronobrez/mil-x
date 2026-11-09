namespace MilX.Desktop.Controls;

/// <summary>
/// Which peaks of a spectrum get their mass written next to them. Picking the tallest peaks and
/// dropping whatever will not fit reads badly on a survey scan: MIL-Q measured 268 maxima above 2 % of
/// the base peak on a ZenoTOF survey, 105 of them in the first eighth of the axis, the twelve tallest
/// all inside 118 Da — twelve labels offered, one drawn, everything above m/z 330 unnamed.
///
/// So the visible axis is cut into <see cref="Regions"/> equal windows and each may claim
/// <see cref="Budget"/> labels, tallest first, in rounds: the tallest of every window, then each
/// window's second, and so on, so a dense window cannot spend three labels before a quiet window's
/// only peak has had its turn. The collision rule that runs afterwards is unchanged: a label that
/// would print on top of one already placed is dropped. The budget decides who asks; the collision
/// rule decides who fits. Shared by the screen and the export, which draw through the same code.
/// Ported from MIL-Q's labels.py with its constants; a drawing rule, nothing is quantified from it.
/// </summary>
public static class LabelBudget
{
    /// <summary>How many equal windows the visible mass axis is cut into: on a 100–2000 axis, eight makes a window 240 Da.</summary>
    public const int Regions = 8;
    /// <summary>How many labels one window may claim; the fourth of a region is what the collision rule drops anyway, and three covers a monoisotope with its M+1 and M+2.</summary>
    public const int Budget = 3;
    /// <summary>A candidate has to reach this fraction of the tallest peak in view — in view, so a zoom into a quiet stretch still names what is on screen.</summary>
    public const double MinRelative = 0.02;

    /// <summary>Which window of [low, high] a position falls in.</summary>
    public static int RegionOf(double x, double low, double high, int regions = Regions)
    {
        var span = high - low;
        if (span <= 0 || regions <= 1) return 0;
        var index = (int)((x - low) / span * regions);
        return Math.Clamp(index, 0, regions - 1);
    }

    /// <summary>
    /// The peaks worth labelling between <paramref name="low"/> and <paramref name="high"/>, in claim
    /// order. <paramref name="peaks"/> are (position, height) in any order; the ones returned are ordered
    /// so a caller's collision rule offers room to the tallest peak of every region before it offers a
    /// second label to any region. <paramref name="most"/> caps the total; <paramref name="minRelative"/>
    /// is a fraction of the tallest peak in view, applied before the regions are counted.
    /// </summary>
    public static IReadOnlyList<(double X, double Y)> Choose(IEnumerable<(double X, double Y)> peaks, double low, double high,
        int regions = Regions, int budget = Budget, int? most = null, double? minRelative = MinRelative)
    {
        var kept = peaks.Where(p => p.X >= low && p.X <= high).ToList();
        if (kept.Count == 0) return Array.Empty<(double, double)>();
        if (minRelative is { } fraction)
        {
            var ceiling = kept.Max(p => Math.Abs(p.Y));
            var floor = ceiling * fraction;
            kept = kept.Where(p => Math.Abs(p.Y) >= floor).ToList();
            if (kept.Count == 0) return Array.Empty<(double, double)>();
        }
        var byRegion = new SortedDictionary<int, List<(double X, double Y)>>();
        foreach (var p in kept)
        {
            var r = RegionOf(p.X, low, high, regions);
            if (!byRegion.TryGetValue(r, out var list)) byRegion[r] = list = new List<(double, double)>();
            list.Add(p);
        }
        foreach (var list in byRegion.Values) list.Sort((a, b) => Math.Abs(b.Y).CompareTo(Math.Abs(a.Y)));
        var ordered = new List<(double X, double Y)>();
        for (var rank = 0; rank < Math.Max(1, budget); rank++)
        {
            var round = byRegion.Values.Where(g => g.Count > rank).Select(g => g[rank]).OrderByDescending(p => Math.Abs(p.Y)).ToList();
            ordered.AddRange(round);
        }
        if (most is { } cap) ordered = ordered.Take(Math.Max(0, cap)).ToList();
        return ordered;
    }
}
