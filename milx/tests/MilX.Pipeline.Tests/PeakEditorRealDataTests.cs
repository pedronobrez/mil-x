using CompMs.MsdialCore.DataObj;
using MilX.Pipeline.Curation;
using MilX.Pipeline.Results;
using Xunit;

namespace MilX.Pipeline.Tests;

/// <summary>
/// End to end on a real alignment result: re-integrate a feature, split it, write the container
/// back and read it again. Runs only when MILX_TEST_ALIGNMENT points at an alignment .arf,
/// and always on a copy, so a run is never edited by the test suite.
/// </summary>
public class PeakEditorRealDataTests
{
    private static string? Source => Environment.GetEnvironmentVariable("MILX_TEST_ALIGNMENT") is { Length: > 0 } p && File.Exists(p) ? p : null;

    [Fact]
    public void Reintegrates_splits_saves_and_reloads_a_real_alignment()
    {
        var source = Source;
        if (source is null)
        {
            Console.WriteLine("skipped: set MILX_TEST_ALIGNMENT to an alignment .arf");
            return;
        }

        var work = Path.Combine(Path.GetTempPath(), "milx-align-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work);
        try
        {
            var stem = Path.GetFileNameWithoutExtension(source);
            var dir = Path.GetDirectoryName(source)!;
            foreach (var file in Directory.EnumerateFiles(dir, stem + "*"))
            {
                File.Copy(file, Path.Combine(work, Path.GetFileName(file)));
            }
            // the container file on disk is "<name>.arf2" while the bean names it "<name>.arf";
            // the sibling peak-property file is derived from the bean's extension
            var beanName = Path.GetFileName(source);
            if (beanName.EndsWith(".arf2", StringComparison.OrdinalIgnoreCase)) beanName = beanName[..^1];
            var copy = new AlignmentFileBean { FilePath = Path.Combine(work, beanName), FileName = stem };

            var container = AlignmentResultContainer.Load(copy);
            Assert.NotNull(container);
            var spots = container!.AlignmentSpotProperties;
            Assert.NotEmpty(spots);

            // a feature detected in several samples, so a re-integration has something to change
            var spot = spots.First(s => (s.AlignedPeakProperties?.Count(p => p.PeakHeightTop > 0) ?? 0) >= 2);
            var originalCount = spots.Count;
            var originalHeight = spot.HeightAverage;
            var apex = spot.TimesCenter.RT.Value;

            // a flat synthetic chromatogram is enough to prove the edit lands: the apex is known
            var chromatograms = spot.AlignedPeakProperties!.ToDictionary(
                p => p.FileID,
                p => (IReadOnlyList<ChromatogramPoint>)Enumerable.Range(0, 41)
                    .Select(i => new ChromatogramPoint(apex - 0.20 + i * 0.01, i == 20 ? 12345.0 : 100.0))
                    .ToList());

            var result = PeakEditor.Reintegrate(spot, chromatograms, apex - 0.15, apex + 0.15);
            Assert.True(result.SamplesChanged > 0);
            Assert.Equal(12345.0, spot.AlignedPeakProperties![0].PeakHeightTop, 0);
            Assert.True(spot.IsManuallyModifiedForQuant);
            Assert.NotEqual(originalHeight, spot.HeightAverage);

            var clone = PeakEditor.SplitIsomer(container, spot);
            Assert.Equal(originalCount + 1, spots.Count);

            PeakEditor.Save(container, copy);
            Assert.True(File.Exists(copy.FilePath + "2.before-curation"), "the container must be kept");
            Assert.True(Directory.EnumerateFiles(work, "*_PeakProperties*.before-curation").Any(), "the peak properties must be kept");

            var reloaded = AlignmentResultContainer.Load(copy);
            Assert.NotNull(reloaded);
            Assert.Equal(originalCount + 1, reloaded!.AlignmentSpotProperties.Count);
            var reloadedSpot = reloaded.AlignmentSpotProperties.First(s => s.MasterAlignmentID == spot.MasterAlignmentID);
            Assert.Equal(12345.0, reloadedSpot.AlignedPeakProperties![0].PeakHeightTop, 0);
            Assert.True(reloadedSpot.IsManuallyModifiedForQuant);
            Assert.Contains(reloaded.AlignmentSpotProperties, s => s.MasterAlignmentID == clone.MasterAlignmentID);
        }
        finally
        {
            try { Directory.Delete(work, true); } catch { }
        }
    }
}
