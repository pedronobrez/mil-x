using CompMs.Common.Components;
using CompMs.Common.Enum;
using CompMs.MsdialCore.DataObj;
using OpenDIAL.Pipeline.Curation;
using OpenDIAL.Pipeline.Results;
using Xunit;

namespace OpenDIAL.Pipeline.Tests;

public class PeakEditorTests
{
    /// <summary>A gaussian centred at <paramref name="centre"/> sampled every 0.01 min.</summary>
    private static IReadOnlyList<ChromatogramPoint> Gaussian(double centre, double height, double sigma = 0.05)
    {
        var points = new List<ChromatogramPoint>();
        for (var rt = 0.0; rt <= 5.0; rt += 0.01)
        {
            points.Add(new ChromatogramPoint(Math.Round(rt, 4), height * Math.Exp(-Math.Pow(rt - centre, 2) / (2 * sigma * sigma))));
        }
        return points;
    }

    private static AlignmentChromPeakFeature Peak(int fileId) => new()
    {
        FileID = fileId,
        MasterPeakID = fileId,
        ChromXsLeft = new ChromXs(1.0, ChromXType.RT, ChromXUnit.Min),
        ChromXsTop = new ChromXs(1.2, ChromXType.RT, ChromXUnit.Min),
        ChromXsRight = new ChromXs(1.4, ChromXType.RT, ChromXUnit.Min),
        PeakHeightTop = 1,
    };

    private static AlignmentSpotProperty Spot(params AlignmentChromPeakFeature[] peaks) => new()
    {
        MasterAlignmentID = 5,
        AlignmentID = 5,
        TimesCenter = new ChromXs(1.2, ChromXType.RT, ChromXUnit.Min),
        MassCenter = 500.3,
        AlignedPeakProperties = peaks.ToList(),
        AlignmentDriftSpotFeatures = new List<AlignmentSpotProperty>(),
        TimesMin = new ChromXs(1.0, ChromXType.RT, ChromXUnit.Min),
        TimesMax = new ChromXs(1.4, ChromXType.RT, ChromXUnit.Min),
    };

    [Fact]
    public void Reintegration_takes_the_apex_and_the_area_of_the_window()
    {
        var spot = Spot(Peak(0), Peak(1));
        var chromatograms = new Dictionary<int, IReadOnlyList<ChromatogramPoint>>
        {
            [0] = Gaussian(2.50, 10000),
            [1] = Gaussian(2.52, 4000),
        };

        var result = PeakEditor.Reintegrate(spot, chromatograms, 2.30, 2.70);

        Assert.Equal(2, result.SamplesChanged);
        Assert.Equal(0, result.SamplesSkipped);
        var a = spot.AlignedPeakProperties[0];
        Assert.Equal(10000, a.PeakHeightTop, 0);            // the apex inside the window
        Assert.Equal(2.50, a.ChromXsTop.RT.Value, 2);
        Assert.Equal(2.30, a.ChromXsLeft.RT.Value, 2);
        Assert.Equal(2.70, a.ChromXsRight.RT.Value, 2);
        // a gaussian of height h and sigma s has area h*s*sqrt(2 pi); in seconds that is x60
        var expectedArea = 10000 * 0.05 * Math.Sqrt(2 * Math.PI) * 60;
        Assert.InRange(a.PeakAreaAboveZero, expectedArea * 0.97, expectedArea * 1.03);
        Assert.True(a.PeakAreaAboveBaseline > 0 && a.PeakAreaAboveBaseline <= a.PeakAreaAboveZero);
        Assert.Equal(4000, spot.AlignedPeakProperties[1].PeakHeightTop, 0);

        // the feature's own numbers follow its peaks
        Assert.Equal(7000, spot.HeightAverage, 0);
        Assert.Equal(1.0, spot.FillParcentage, 3);
        Assert.Equal(2.51, spot.TimesCenter.RT.Value, 2);
        Assert.True(spot.IsManuallyModifiedForQuant);
    }

    [Fact]
    public void A_sample_with_no_chromatogram_is_skipped_and_the_rest_still_apply()
    {
        var spot = Spot(Peak(0), Peak(1));
        var chromatograms = new Dictionary<int, IReadOnlyList<ChromatogramPoint>> { [0] = Gaussian(2.5, 8000) };
        var result = PeakEditor.Reintegrate(spot, chromatograms, 2.3, 2.7);
        Assert.Equal(1, result.SamplesChanged);
        Assert.Equal(1, result.SamplesSkipped);
        Assert.Equal(8000, spot.AlignedPeakProperties[0].PeakHeightTop, 0);
    }

    [Fact]
    public void Only_the_named_samples_are_touched()
    {
        var spot = Spot(Peak(0), Peak(1));
        var chromatograms = new Dictionary<int, IReadOnlyList<ChromatogramPoint>>
        {
            [0] = Gaussian(2.5, 9000),
            [1] = Gaussian(2.5, 9000),
        };
        PeakEditor.Reintegrate(spot, chromatograms, 2.3, 2.7, new[] { 1 });
        Assert.Equal(1, spot.AlignedPeakProperties[0].PeakHeightTop, 0);   // untouched
        Assert.Equal(9000, spot.AlignedPeakProperties[1].PeakHeightTop, 0);
    }

    [Fact]
    public void Splitting_gives_two_independent_features_next_to_each_other()
    {
        var spot = Spot(Peak(0), Peak(1));
        var container = new AlignmentResultContainer
        {
            AlignmentSpotProperties = new System.Collections.ObjectModel.ObservableCollection<AlignmentSpotProperty> { spot },
            TotalAlignmentSpotCount = 1,
        };

        var clone = PeakEditor.SplitIsomer(container, spot);

        Assert.Equal(2, container.AlignmentSpotProperties.Count);
        Assert.Equal(2, container.TotalAlignmentSpotCount);
        Assert.Same(spot, container.AlignmentSpotProperties[0]);
        Assert.Same(clone, container.AlignmentSpotProperties[1]);
        Assert.NotEqual(spot.MasterAlignmentID, clone.MasterAlignmentID);
        Assert.Contains("split from #5", clone.Comment);
        Assert.True(clone.IsManuallyModifiedForQuant);

        // integrating one copy must leave the other alone: that is the point of the split
        var chromatograms = new Dictionary<int, IReadOnlyList<ChromatogramPoint>>
        {
            [0] = Gaussian(2.40, 6000),
            [1] = Gaussian(2.40, 6000),
        };
        PeakEditor.Reintegrate(clone, chromatograms, 2.20, 2.60);
        Assert.Equal(6000, clone.AlignedPeakProperties[0].PeakHeightTop, 0);
        Assert.Equal(1, spot.AlignedPeakProperties[0].PeakHeightTop, 0);
    }
}
