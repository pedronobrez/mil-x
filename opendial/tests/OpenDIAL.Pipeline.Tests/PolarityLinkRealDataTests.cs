using CompMs.MsdialCore.DataObj;
using OpenDIAL.Pipeline.Results;
using Xunit;
using Xunit.Abstractions;

namespace OpenDIAL.Pipeline.Tests;

/// <summary>
/// The pairing measured against a real alignment's density.
///
/// There is no negative-mode acquisition of the liver batch to pair against yet, so the question a
/// synthetic case cannot answer is asked here instead: at two and a half thousand real features, in
/// a real gradient, how often does a coincidence pass the gates? The negative side is derived from
/// the real one — every feature as its deprotonated molecule, at a jittered time, with a jittered
/// height profile — so what is recovered is known, and a decoy run with the masses pushed off a
/// real adduct distance says what the false pairing rate is.
///
/// Runs only when OPENDIAL_TEST_ALIGNMENT points at an .arf2, and only reads it.
/// </summary>
public class PolarityLinkRealDataTests
{
    private readonly ITestOutputHelper _output;
    public PolarityLinkRealDataTests(ITestOutputHelper output) => _output = output;

    private const int NegativeIdOffset = 1_000_000;
    private const double Proton = 1.0072765;

    private static string? Source =>
        Environment.GetEnvironmentVariable("OPENDIAL_TEST_ALIGNMENT") is { Length: > 0 } p && File.Exists(p) ? p : null;

    [Fact]
    public void A_real_feature_list_pairs_what_it_should_and_almost_nothing_it_should_not()
    {
        var source = Source;
        if (source is null)
        {
            Console.WriteLine("skipped: set OPENDIAL_TEST_ALIGNMENT to an alignment .arf2");
            return;
        }

        var table = Load(source);
        var positive = table.Spots
            .Where(s => s.SampleHeights.Count >= 3 || s.SamplePeaks.Count >= 3)
            .Select(s => new PolarityCandidate(s.Id, s.Rt, s.Mz, s.AverageHeight, s.Adduct, s.Name, s.SignalToNoiseAverage, Heights(s)))
            .ToList();
        _output.WriteLine($"{table.Spots.Count} features in the alignment, {positive.Count} with a profile to compare");
        foreach (var (adduct, count) in positive.GroupBy(c => string.IsNullOrWhiteSpace(c.Adduct) ? "(none)" : c.Adduct)
                     .Select(g => (g.Key, g.Count())).OrderByDescending(x => x.Item2).Take(6))
        {
            _output.WriteLine($"  adduct {adduct}: {count:N0}");
        }
        Assert.True(positive.Count >= 200, "the alignment should carry a real feature list");

        var negative = Derive(positive, massShift: 0);
        var found = PolarityLink.Link(positive, negative);
        var correct = found.Pairs.Count(p => p.NegativeId == p.PositiveId + NegativeIdOffset);
        var recovery = (double)correct / positive.Count;
        var precision = found.Pairs.Count == 0 ? 0 : (double)correct / found.Pairs.Count;
        _output.WriteLine($"recovery: {correct:N0} of {positive.Count:N0} ({recovery:P1}) · precision {precision:P2} · {found.Sentence()}");

        Assert.True(recovery > 0.80, $"the same molecule in the other polarity should be found again; recovered {recovery:P1}");
        Assert.True(precision > 0.95, $"a recovered pair should be the right one; precision {precision:P2}");

        // the decoy: every neutral pushed 0.37 Da off, which is no adduct distance at all
        var decoy = PolarityLink.Link(positive, Derive(positive, massShift: 0.37));
        var falseRate = (double)decoy.Pairs.Count / positive.Count;
        _output.WriteLine($"decoy: {decoy.Pairs.Count:N0} pair(s) out of {positive.Count:N0} ({falseRate:P2})");
        Assert.True(falseRate < 0.05, $"a feature list this dense should not pair by coincidence; {falseRate:P2} did");
    }

    /// <summary>The same compounds as they would come out of a negative-mode run of the same batch.</summary>
    private static List<PolarityCandidate> Derive(IReadOnlyList<PolarityCandidate> positive, double massShift)
    {
        var random = new Random(20260914);
        var derived = new List<PolarityCandidate>(positive.Count);
        foreach (var c in positive)
        {
            // the negative run sees the same neutral molecule, whichever adduct the positive run made of it
            if (!PolarityLink.TryNeutral(c.Mz, c.Adduct, out var neutral)) neutral = c.Mz - Proton;
            var response = 0.2 + random.NextDouble();                 // a different ionisation efficiency
            var heights = c.Heights.Select(h => h * response * (0.88 + 0.24 * random.NextDouble())).ToList();
            derived.Add(new PolarityCandidate(
                c.Id + NegativeIdOffset,
                c.Rt + (random.NextDouble() - 0.5) * 0.06,            // ±0.03 min of drift between the two runs
                neutral + massShift - Proton,
                c.Height * response,
                "[M-H]-",
                string.Empty,                                         // the negative run annotates on its own
                c.SignalToNoise * response,
                heights));
        }
        return derived;
    }

    private static IReadOnlyList<double> Heights(AlignmentSpotRow spot)
    {
        if (spot.SamplePeaks.Count >= 3) return spot.SamplePeaks.Select(p => double.IsNaN(p.Height) ? 0 : p.Height).ToList();
        return spot.SampleHeights.Select(h => double.IsNaN(h.Height) ? 0 : h.Height).ToList();
    }

    private static AlignmentTable Load(string source)
    {
        var beanName = Path.GetFileName(source);
        if (beanName.EndsWith(".arf2", StringComparison.OrdinalIgnoreCase)) beanName = beanName[..^1];
        var bean = new AlignmentFileBean { FilePath = Path.Combine(Path.GetDirectoryName(source)!, beanName), FileName = Path.GetFileNameWithoutExtension(source) };
        var container = AlignmentResultContainer.Load(bean);
        return ResultLoader.LoadAlignmentTableAsync(bean, Array.Empty<AnalysisFileBean>(), container).GetAwaiter().GetResult();
    }
}
