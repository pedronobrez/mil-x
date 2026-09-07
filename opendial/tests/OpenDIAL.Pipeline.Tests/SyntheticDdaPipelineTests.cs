using OpenDIAL.Pipeline;
using OpenDIAL.Pipeline.Model;
using OpenDIAL.Pipeline.Parameters;
using OpenDIAL.Pipeline.Results;
using Xunit;
using Xunit.Abstractions;

namespace OpenDIAL.Pipeline.Tests;

public class SyntheticDdaPipelineTests
{
    private static readonly string[] ExpectedCompounds =
    {
        "Caffeine", "Tryptophan", "Phenylalanine", "Adenosine", "Riboflavin", "Kynurenine",
        "Hippuric acid", "Nicotinamide", "Cortisol", "Testosterone", "Acetylcarnitine", "Palmitoylcarnitine",
    };

    private readonly ITestOutputHelper _output;

    public SyntheticDdaPipelineTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string FindTestData()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "testdata", "synthetic_dda");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("testdata/synthetic_dda was not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public async Task FullPipeline_OnSyntheticDda_FindsPeaksAnnotationsAndAlignment()
    {
        var source = FindTestData();
        var work = Path.Combine(Path.GetTempPath(), "opendial-tests", Guid.NewGuid().ToString("N"));
        var input = Path.Combine(work, "input");
        var output = Path.Combine(work, "output");
        Directory.CreateDirectory(input);
        foreach (var name in new[] { "SampleA01.mzML", "SampleB02.mzML", "SampleA03.mzML", "library.msp", "method_lcms_dda.txt" })
        {
            File.Copy(Path.Combine(source, name), Path.Combine(input, name));
        }

        var parameters = MethodParameters.FromMethodFileText(await File.ReadAllTextAsync(Path.Combine(input, "method_lcms_dda.txt")));
        parameters.MspFilePath = Path.Combine(input, "library.msp");
        parameters.NumberOfThreads = 4;

        var request = new PipelineRequest
        {
            OutputFolder = output,
            Mode = IonizationMode.LCMS,
            Parameters = parameters,
            SaveProject = true,
        };
        request.InputFiles.Add(new InputFile(Path.Combine(input, "SampleA01.mzML")) { Class = "A", AnalyticalOrder = 1 });
        request.InputFiles.Add(new InputFile(Path.Combine(input, "SampleB02.mzML")) { Class = "B", AnalyticalOrder = 2 });
        request.InputFiles.Add(new InputFile(Path.Combine(input, "SampleA03.mzML")) { Class = "A", AnalyticalOrder = 3 });

        var log = new List<string>();
        var progress = new SynchronousProgress<PipelineProgress>(p => { if (p.ShowInLog) log.Add(p.ToString()); });
        var result = await new PipelineRunner().RunAsync(request, progress, CancellationToken.None);
        foreach (var line in log) _output.WriteLine(line);

        Assert.Equal(3, result.AnalysisFiles.Count);
        Assert.NotNull(result.AlignmentFile);
        Assert.NotNull(result.ProjectFilePath);
        Assert.True(File.Exists(result.ProjectFilePath));

        var annotated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in result.AnalysisFiles)
        {
            var peaks = await ResultLoader.LoadPeakTableAsync(file);
            _output.WriteLine($"{file.AnalysisFileName}: {peaks.Count} peaks, {peaks.Count(p => p.IsAnnotated)} annotated");
            Assert.True(peaks.Count >= 25, $"{file.AnalysisFileName} has only {peaks.Count} peaks");
            foreach (var p in peaks.Where(p => p.IsAnnotated))
            {
                annotated.Add(p.Name);
            }

            // MS/MS and EIC loading must work for an annotated peak
            var first = peaks.First(p => p.IsAnnotated);
            var ms2 = ResultLoader.LoadMs2(file, first);
            Assert.NotNull(ms2);
            Assert.NotEmpty(ms2!.Peaks);
            var raw = await ResultLoader.LoadRawMeasurementAsync(file);
            var eic = ResultLoader.LoadEic(raw, first.Mz, 0.01);
            Assert.NotEmpty(eic.Points);
            Assert.True(eic.Points.Max(pt => pt.Intensity) > 0);
            var reference = ResultLoader.LoadReferenceSpectrum(result.DataBaseMapper, first.MatchResult);
            Assert.NotNull(reference);
            Assert.NotEmpty(reference!.Peaks);
        }

        var hits = ExpectedCompounds.Count(c => annotated.Contains(c));
        _output.WriteLine("Annotated: " + string.Join(", ", annotated.OrderBy(n => n)));
        Assert.True(hits >= 10, $"Only {hits} of the expected compounds were annotated: {string.Join(", ", annotated)}");

        var alignment = await ResultLoader.LoadAlignmentTableAsync(result.AlignmentFile!, result.AnalysisFiles);
        _output.WriteLine($"Alignment: {alignment.Spots.Count} spots ({alignment.Source})");
        Assert.True(alignment.Spots.Count >= 15, $"Alignment has only {alignment.Spots.Count} spots");
        Assert.Equal(3, alignment.Samples.Count);
        Assert.All(alignment.Spots, s => Assert.Equal(3, s.SampleHeights.Count));

        var mdalign = result.ExportedFiles.Single(e => e.Path.EndsWith(".mdalign")).Path;
        var fromTsv = ResultLoader.LoadAlignmentTableFromTsv(mdalign);
        Assert.Equal(alignment.Spots.Count, fromTsv.Spots.Count);

        // re-open through the saved project
        var reopened = await ProjectOpener.OpenAsync(output);
        Assert.Equal("mdproject", reopened.Source);
        Assert.Equal(3, reopened.AnalysisFiles.Count);
        Assert.NotNull(reopened.AlignmentFile);
        var reopenedPeaks = await ResultLoader.LoadPeakTableAsync(reopened.AnalysisFiles[0]);
        Assert.True(reopenedPeaks.Count >= 25);

        // and through the folder fallback
        var fromFolder = ProjectOpener.OpenFolder(output);
        Assert.Equal(3, fromFolder.AnalysisFiles.Count);
        Assert.NotNull(fromFolder.AlignmentFile);

        // keep the working folder only when something above failed
        try { Directory.Delete(work, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public void MethodParameters_RoundTrip_PreservesValues()
    {
        var p = new MethodParameters { MinimumPeakHeight = 1234, MspFilePath = "/tmp/x.msp", TogetherWithAlignment = false, SmoothingMethod = SmoothingKind.SavitzkyGolayFilter };
        p.AdditionalLines["Some custom key"] = "value";
        var text = p.ToMethodFileText();
        var q = MethodParameters.FromMethodFileText(text);
        Assert.Equal(1234, q.MinimumPeakHeight);
        Assert.Equal("/tmp/x.msp", q.MspFilePath);
        Assert.False(q.TogetherWithAlignment);
        Assert.Equal(SmoothingKind.SavitzkyGolayFilter, q.SmoothingMethod);
        Assert.Equal("value", q.AdditionalLines["Some custom key"]);

        var lcms = MethodFileParser.CreateLcmsParameter(text);
        Assert.Equal(1234, lcms.MinimumAmplitude);
        Assert.Equal(CompMs.Common.Enum.SmoothingMethod.SavitzkyGolayFilter, lcms.SmoothingMethod);
        Assert.False(lcms.TogetherWithAlignment);
    }

    private sealed class SynchronousProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        public SynchronousProgress(Action<T> handler) => _handler = handler;
        public void Report(T value) => _handler(value);
    }
}
