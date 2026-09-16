using MilX.Pipeline.Curation;

namespace MilX.Pipeline.Results;

/// <summary>One matrix out of two runs: the compounds, the injections, and the review that goes with them.</summary>
public sealed record MergedMatrix(
    IReadOnlyList<AlignmentSpotRow> Spots,
    IReadOnlyList<SampleInfo> Samples,
    CurationStore Curation,
    int FromBoth,
    int FromPositiveOnly,
    int FromNegativeOnly)
{
    public string Sentence() =>
        $"{Spots.Count:N0} compound(s) across {Samples.Count:N0} injection(s) · " +
        $"{FromBoth:N0} measured in both polarities · {FromPositiveOnly:N0} positive only · {FromNegativeOnly:N0} negative only";
}

/// <summary>
/// The two polarities of a batch as one table for the statistics.
///
/// A compound both runs saw must be counted once, not twice, or every model is fitted on a matrix
/// where half the rows are copies of the other half — the clustering finds the copies, the PCA
/// spends a component on them, and the false discovery correction is applied to a feature count
/// that is not real. So a pair contributes one row, taken from the polarity that measured it
/// better, and what only one polarity saw comes across as it is.
///
/// Heights are never combined. The row is one run's numbers, whole; the other run is what confirmed
/// the identification, not a second measurement to average in.
/// </summary>
public static class PolarityMerge
{
    /// <summary>Ids of the negative-only compounds are shifted so they cannot collide with the positive ones.</summary>
    public const int NegativeIdOffset = 1_000_000;

    public static MergedMatrix Build(
        AlignmentTable positive,
        AlignmentTable negative,
        PolarityLinkResult link,
        CurationStore? positiveCuration = null,
        CurationStore? negativeCuration = null)
    {
        // the injections of the positive run name the columns; the negative run's values are
        // carried onto them through the pairing, so a row is one compound across one set of samples
        var samples = link.Samples.Count > 0
            ? link.Samples.Select(s => positive.Samples.FirstOrDefault(p => p.FileId == s.PositiveFileId)
                                       ?? new SampleInfo(s.PositiveFileId, s.PositiveFile, "1", "Sample")).ToList()
            : positive.Samples.ToList();
        var negativeByPositive = link.Samples.ToDictionary(s => s.PositiveFileId, s => s.NegativeFileId);

        var positiveById = positive.Spots.ToDictionary(s => s.Id);
        var negativeById = negative.Spots.ToDictionary(s => s.Id);
        var curation = CurationStore.InMemory();

        var spots = new List<AlignmentSpotRow>();
        var pairedPositive = new HashSet<int>();
        var pairedNegative = new HashSet<int>();
        var both = 0;

        foreach (var pair in link.Pairs)
        {
            if (!positiveById.TryGetValue(pair.PositiveId, out var p) || !negativeById.TryGetValue(pair.NegativeId, out var n)) continue;
            pairedPositive.Add(pair.PositiveId);
            pairedNegative.Add(pair.NegativeId);
            both++;
            var quantifiesPositive = pair.Quantify == PolarityChoice.Positive;
            var row = quantifiesPositive
                ? Retag(p, p.Id, p.Name, p.Adduct)
                : FromNegative(n, p.Id, n.IsAnnotated ? n.Name : p.Name, n.Adduct, samples, negativeByPositive);
            spots.Add(row);
            CopyTags(curation, p.Id, positiveCuration, pair.PositiveId, negativeCuration, pair.NegativeId);
        }

        foreach (var p in positive.Spots)
        {
            if (pairedPositive.Contains(p.Id)) continue;
            spots.Add(Retag(p, p.Id, p.Name, p.Adduct));
            CopyTags(curation, p.Id, positiveCuration, p.Id, null, 0);
        }

        foreach (var n in negative.Spots)
        {
            if (pairedNegative.Contains(n.Id)) continue;
            var id = n.Id + NegativeIdOffset;
            spots.Add(FromNegative(n, id, n.Name, n.Adduct, samples, negativeByPositive));
            CopyTags(curation, id, null, 0, negativeCuration, n.Id);
        }

        spots.Sort((a, b) => a.Rt.CompareTo(b.Rt));
        return new MergedMatrix(
            spots, samples, curation,
            both,
            positive.Spots.Count - pairedPositive.Count,
            negative.Spots.Count - pairedNegative.Count);
    }

    /// <summary>
    /// The same feature under the id, the name and the columns the merged table knows it by.
    /// Everything not named here is carried across untouched — a merged row is a real feature, not
    /// a summary of one, so the spectrum, the candidates and the hand edits all come with it.
    /// </summary>
    private static AlignmentSpotRow Retag(
        AlignmentSpotRow spot, int id, string name, string adduct,
        IReadOnlyList<SampleValue>? heights = null, IReadOnlyList<AlignedSamplePeak>? peaks = null) => new()
    {
        Spot = spot.Spot,
        Id = id,
        Name = name,
        Rt = spot.Rt,
        Mz = spot.Mz,
        AverageHeight = spot.AverageHeight,
        FillPercent = spot.FillPercent,
        Score = spot.Score,
        Adduct = adduct,
        SampleHeights = heights ?? spot.SampleHeights,
        SamplePeaks = peaks ?? spot.SamplePeaks,
        Ontology = spot.Ontology,
        Formula = spot.Formula,
        InChIKey = spot.InChIKey,
        SignalToNoiseAverage = spot.SignalToNoiseAverage,
        MatchResult = spot.MatchResult,
        MsmsAssigned = spot.MsmsAssigned,
        RepresentativeFileId = spot.RepresentativeFileId,
        IsotopicPeaks = spot.IsotopicPeaks,
        Comment = spot.Comment,
        Candidates = spot.Candidates,
        IsManuallyAnnotated = spot.IsManuallyAnnotated,
        MonoisotopicPercentage = spot.MonoisotopicPercentage,
        IsotopeWeight = spot.IsotopeWeight,
        IsManuallyQuantified = spot.IsManuallyQuantified,
        ClassHeights = spot.ClassHeights,
    };

    /// <summary>
    /// A negative-run feature under the merged table's id and name, with its per-injection values
    /// moved onto the positive run's columns — so every row speaks about the same injections, in
    /// the same order. An injection the pairing could not match reads as NaN rather than zero,
    /// which is the difference between "not measured" and "measured as nothing".
    /// </summary>
    private static AlignmentSpotRow FromNegative(
        AlignmentSpotRow spot, int id, string name, string adduct,
        IReadOnlyList<SampleInfo> samples, IReadOnlyDictionary<int, int> negativeByPositive)
    {
        if (negativeByPositive.Count == 0) return Retag(spot, id, name, adduct);

        var heightByFile = spot.SampleHeights.ToDictionary(h => h.FileId, h => h.Height);
        var peakByFile = spot.SamplePeaks.ToDictionary(p => p.FileId);
        var heights = new List<SampleValue>(samples.Count);
        var peaks = new List<AlignedSamplePeak>(samples.Count);

        foreach (var sample in samples)
        {
            var theirs = negativeByPositive.TryGetValue(sample.FileId, out var mapped) ? mapped : -1;
            heights.Add(new SampleValue(sample.FileId, sample.FileName, sample.Class,
                theirs >= 0 && heightByFile.TryGetValue(theirs, out var h) ? h : double.NaN));
            if (theirs >= 0 && peakByFile.TryGetValue(theirs, out var peak))
            {
                peaks.Add(peak with { FileId = sample.FileId, FileName = sample.FileName, Class = sample.Class, SampleType = sample.SampleType });
            }
        }
        return Retag(spot, id, name, adduct, heights, peaks);
    }

    /// <summary>
    /// The verdict a merged row carries. A pair takes whichever review said something — the tags
    /// travel between the two runs while reviewing, so normally they agree; when only one side was
    /// tagged, that is the answer rather than nothing.
    /// </summary>
    private static void CopyTags(CurationStore into, int id, CurationStore? positive, int positiveId, CurationStore? negative, int negativeId)
    {
        var tags = positive?.TagsOf(positiveId) ?? Array.Empty<PeakSpotTagKind>();
        if (tags.Count == 0 && negative is not null) tags = negative.TagsOf(negativeId);
        foreach (var tag in tags) into.SetTag(id, tag, true);

        var comment = positive?.Get(positiveId).Comment ?? string.Empty;
        if (comment.Length == 0 && negative is not null) comment = negative.Get(negativeId).Comment;
        if (comment.Length > 0) into.SetComment(id, comment);
    }
}
