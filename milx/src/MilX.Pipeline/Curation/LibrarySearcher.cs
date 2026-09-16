using CompMs.Common.DataObj.Result;
using CompMs.Common.Enum;
using CompMs.Common.Parameter;
using CompMs.MsdialCore.Algorithm.Annotation;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using CompMs.MsdialLcMsApi.Algorithm.Annotation;
using MilX.Pipeline.Results;

namespace MilX.Pipeline.Curation;

/// <summary>Tolerances of a hand-driven library search, the ones MS-DIAL's compound search exposes.</summary>
public sealed record LibrarySearchOptions(
    double Ms1Tolerance = 0.01,
    double Ms2Tolerance = 0.05,
    double RtTolerance = 0.5,
    bool UseRetentionTime = false,
    int MaxResults = 100);

/// <summary>
/// Searches the library again for one feature, with tolerances the reviewer chooses.
///
/// The run keeps only its best few matches, so when the right compound is not among them the only
/// way forward is to ask the library again — usually wider on mass or looser on the spectrum. This
/// runs the same annotator the pipeline used, so a score here means what a score there means.
/// </summary>
public static class LibrarySearcher
{
    public static IReadOnlyList<AnnotationCandidate> Search(
        MoleculeDataBase database,
        TargetOmics omics,
        AlignmentSpotProperty spot,
        MSDecResult? scan,
        LibrarySearchOptions options)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(spot);

        var parameter = new MsRefSearchParameterBase
        {
            Ms1Tolerance = (float)options.Ms1Tolerance,
            Ms2Tolerance = (float)options.Ms2Tolerance,
            RtTolerance = (float)options.RtTolerance,
            IsUseTimeForAnnotationFiltering = options.UseRetentionTime,
            IsUseTimeForAnnotationScoring = options.UseRetentionTime,
            // the reviewer is looking for what exists, not for what passes: cut-offs stay off here
            TotalScoreCutoff = 0f,
            WeightedDotProductCutOff = 0f,
            SimpleDotProductCutOff = 0f,
            ReverseDotProductCutOff = 0f,
            MatchedPeaksPercentageCutOff = 0f,
            MinimumSpectrumMatch = 0f,
        };

        var annotator = new LcmsMspAnnotator(database, parameter, omics, "re-search", 1);
        var query = new AnnotationQuery(
            spot,
            scan ?? new MSDecResult(),
            spot.IsotopicPeaks ?? new List<CompMs.Common.DataObj.Property.IsotopicPeak>(),
            spot.PeakCharacter,
            parameter,
            annotator,
            ignoreIsotopicPeak: false);

        return annotator.FindCandidates(query)
            // without a product spectrum every record at the same mass ties on the total score, so
            // the closest mass decides the order; that is the only evidence there is in that case
            .OrderByDescending(r => r.TotalScore)
            .ThenByDescending(r => r.AcurateMassSimilarity)
            .Take(Math.Max(1, options.MaxResults))
            .Select(r => new AnnotationCandidate(
                string.IsNullOrEmpty(r.Name) ? "(unnamed record)" : r.Name,
                r.TotalScore, r.SimpleDotProduct, r.WeightedDotProduct, r.ReverseDotProduct,
                Math.Max(0, r.MatchedPeaksCount), Math.Max(0, r.MatchedPeaksPercentage),
                r.AcurateMassSimilarity, r.RtSimilarity,
                false, r.IsSpectrumMatch, r.IsLipidClassMatch, r.IsLipidChainsMatch, r.IsLipidPositionMatch,
                r.Source.ToString(), r.LibraryID))
            .ToList();
    }

    /// <summary>
    /// The library a result was annotated against, taken from the project when it is already loaded.
    /// The annotator id lives on each match result, which is the only handle the mapper exposes.
    /// </summary>
    public static MoleculeDataBase? ResolveDatabase(DataBaseMapper? mapper, IEnumerable<AlignmentSpotRow> spots)
    {
        if (mapper is null) return null;
        foreach (var id in spots.Select(s => s.MatchResult?.AnnotatorID).Where(id => !string.IsNullOrEmpty(id)).Distinct())
        {
            if (mapper.FindReferByAnnotatorID(id) is MoleculeDataBase database && database.Database.Count > 0)
            {
                return database;
            }
        }
        return null;
    }

    /// <summary>Reads a library from an .msp when the project does not carry one that can be searched.</summary>
    public static async Task<MoleculeDataBase> LoadAsync(string mspPath, CancellationToken ct = default)
    {
        var records = await ResultLoader.LoadMspLibraryAsync(mspPath, ct).ConfigureAwait(false);
        return new MoleculeDataBase(records.ToList(), Path.GetFileNameWithoutExtension(mspPath), DataBaseSource.Msp, SourceType.MspDB, mspPath);
    }
}
