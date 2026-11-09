using MilX.Pipeline.Results;
using Xunit;

namespace MilX.Pipeline.Tests;

public class SignalToNoiseTests
{
    private static List<ChromatogramPoint> Trace()
    {
        // a baseline wobbling between 100 and 120, a peak of 5000 at 2.0 min
        var points = new List<ChromatogramPoint>();
        for (var i = 0; i < 100; i++)
        {
            var rt = i * 0.04;
            var baseline = 100 + (i % 3) * 10;
            var peak = 5000 * Math.Exp(-0.5 * Math.Pow((rt - 2.0) / 0.05, 2));
            points.Add(new ChromatogramPoint(rt, baseline + peak));
        }
        return points;
    }

    [Fact]
    public void Peak_to_peak_is_the_swing_and_sd_the_spread_of_the_region()
    {
        var trace = Trace();
        var p2p = SignalToNoise.Measure(trace, 0.0, 1.0, NoiseMode.PeakToPeak)!;
        Assert.Equal(20, p2p.Noise, 6);
        Assert.Equal(26, p2p.Points);
        Assert.Equal("noise 0.00–1.00 min, peak-to-peak", p2p.Describe());
        var sd = SignalToNoise.Measure(trace, 1.0, 0.0, NoiseMode.StandardDeviation)!;   // the bounds may come reversed
        Assert.InRange(sd.Noise, 7.5, 8.5);
        Assert.True(sd.Noise < p2p.Noise);
        Assert.Equal(250, SignalToNoise.Ratio(5000, p2p)!.Value, 6);
    }

    [Fact]
    public void Nothing_to_measure_is_not_measured()
    {
        var zeros = Enumerable.Range(0, 50).Select(i => new ChromatogramPoint(i * 0.1, 0)).ToList();
        Assert.Null(SignalToNoise.Measure(zeros, 0, 5, NoiseMode.PeakToPeak));
        Assert.Null(SignalToNoise.Measure(Trace(), 0.0, 0.01, NoiseMode.PeakToPeak));   // one point
        Assert.Null(SignalToNoise.Measure(Trace(), 7, 8, NoiseMode.PeakToPeak));         // outside
        Assert.Null(SignalToNoise.Ratio(5000, null));
        Assert.Null(SignalToNoise.Automatic(zeros));
    }

    [Fact]
    public void The_automatic_estimate_is_not_inflated_by_the_peak()
    {
        var noise = SignalToNoise.Automatic(Trace())!.Value;
        // the baseline steps by 10 or 20 between points; the robust estimate stays in that order, not the peak's
        Assert.InRange(noise, 3, 30);
        var inside = SignalToNoise.Measure(Trace(), 1.9, 2.1, NoiseMode.PeakToPeak)!.Noise;
        Assert.True(inside > 1000, "the peak's own window reports the peak's slope, which is why the region is chosen on baseline");
    }
}
