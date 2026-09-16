using CompMs.Common.Components;
using CompMs.Common.DataObj.Property;
using CompMs.Common.DataObj.Result;
using CompMs.Common.Enum;
using CompMs.MsdialCore.DataObj;
using CompMs.MsdialCore.MSDec;
using MilX.Pipeline.Curation;
using Xunit;

namespace MilX.Pipeline.Tests;

public class LibrarySearcherTests
{
    private static MoleculeMsReference Reference(int id, string name, double precursor, params (double Mz, double Intensity)[] peaks) => new()
    {
        ScanID = id,
        Name = name,
        PrecursorMz = precursor,
        IonMode = IonMode.Positive,
        AdductType = AdductIon.GetAdductIon("[M+H]+"),
        ChromXs = new ChromXs(5.0, ChromXType.RT, ChromXUnit.Min),
        Spectrum = peaks.Select(p => new SpectrumPeak(p.Mz, p.Intensity)).ToList(),
    };

    private static AlignmentSpotProperty Spot(double mz, double rt) => new()
    {
        MasterAlignmentID = 1,
        AlignmentID = 1,
        MassCenter = mz,
        TimesCenter = new ChromXs(rt, ChromXType.RT, ChromXUnit.Min),
        IonMode = IonMode.Positive,
        AlignedPeakProperties = new List<AlignmentChromPeakFeature>(),
        AlignmentDriftSpotFeatures = new List<AlignmentSpotProperty>(),
    };

    private static MoleculeDataBase Database(params MoleculeMsReference[] records) =>
        new(records.ToList(), "test", DataBaseSource.Msp, SourceType.MspDB, "test.msp");

    [Fact]
    public void Finds_every_record_inside_the_given_mass_tolerance()
    {
        var database = Database(
            Reference(0, "PC 34:1", 760.5851),
            Reference(1, "PE 37:1", 760.5855),
            Reference(2, "TG 48:0", 806.7500));

        var narrow = LibrarySearcher.Search(database, TargetOmics.Lipidomics, Spot(760.5853, 11.4), null, new LibrarySearchOptions(Ms1Tolerance: 0.001));
        Assert.Equal(2, narrow.Count);
        Assert.DoesNotContain(narrow, c => c.Name == "TG 48:0");

        var wide = LibrarySearcher.Search(database, TargetOmics.Lipidomics, Spot(760.5853, 11.4), null, new LibrarySearchOptions(Ms1Tolerance: 50.0));
        Assert.Equal(3, wide.Count);

        var tight = LibrarySearcher.Search(database, TargetOmics.Lipidomics, Spot(500.0, 11.4), null, new LibrarySearchOptions(Ms1Tolerance: 0.01));
        Assert.Empty(tight);
    }

    [Fact]
    public void A_matching_product_spectrum_lifts_a_record_above_the_others()
    {
        var database = Database(
            Reference(0, "with matching fragments", 760.5851, (184.0733, 999), (496.3398, 500), (524.3711, 300)),
            Reference(1, "same mass, other fragments", 760.5851, (100.0, 999), (200.0, 500), (300.0, 300)));

        var scan = new MSDecResult
        {
            PrecursorMz = 760.5853,
            Spectrum = new List<SpectrumPeak> { new(184.0733, 999), new(496.3398, 480), new(524.3711, 310) },
        };

        var results = LibrarySearcher.Search(database, TargetOmics.Lipidomics, Spot(760.5853, 11.4), scan, new LibrarySearchOptions(Ms1Tolerance: 0.01, Ms2Tolerance: 0.05));

        Assert.Equal(2, results.Count);
        Assert.Equal("with matching fragments", results[0].Name);
        Assert.True(results[0].TotalScore > results[1].TotalScore);
        Assert.True(results[0].MatchedPeaksCount >= 3);
        Assert.True(results[0].WeightedDotProduct > 0.5);
    }

    [Fact]
    public void Nothing_is_dropped_by_a_score_cut_off()
    {
        // the run filters; a hand search must show what exists, however badly it scores
        var database = Database(Reference(0, "hopeless match", 760.5851, (50.0, 10)));
        var scan = new MSDecResult { PrecursorMz = 760.5853, Spectrum = new List<SpectrumPeak> { new(700.0, 999) } };
        var results = LibrarySearcher.Search(database, TargetOmics.Lipidomics, Spot(760.5853, 11.4), scan, new LibrarySearchOptions());
        Assert.Single(results);
        Assert.Equal("hopeless match", results[0].Name);
    }
}
