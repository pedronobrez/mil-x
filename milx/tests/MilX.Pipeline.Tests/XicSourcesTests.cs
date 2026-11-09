using CompMs.Common.DataObj;
using CompMs.Common.Enum;
using MilX.Pipeline.Results;
using Xunit;

namespace MilX.Pipeline.Tests;

/// <summary>
/// The gated source gathers one precursor's product spectra across the slots they landed in, by
/// the precursor each scan actually isolated. An IDA-like file is built by hand: a survey scan per
/// cycle and two dependent slots whose precursors change cycle to cycle.
/// </summary>
public class XicSourcesTests
{
    private static RawMeasurement IdaLike()
    {
        var spectra = new List<RawSpectrum>();
        // cycles of 3 spectra: survey, slot 1, slot 2; precursors alternate so 313.24 lands in slot 1 on even cycles and slot 2 on odd ones
        for (var cycle = 0; cycle < 6; cycle++)
        {
            var rt = 1.0 + cycle * 0.01;
            spectra.Add(new RawSpectrum { Index = spectra.Count, MsLevel = 1, ExperimentID = 0, ScanNumber = cycle, ScanStartTime = rt, ScanStartTimeUnit = Units.Minute, Spectrum = Peaks((313.2384, 1000 + cycle), (183.1391, 500)) });
            var first = cycle % 2 == 0 ? 313.2384 : 401.1;
            var second = cycle % 2 == 0 ? 401.1 : 313.2384 + 0.01;   // 10 mDa off: inside a 0.05 gate, outside a 0.005 one
            spectra.Add(Ms2(spectra.Count, 1, cycle, rt, first, 100 + cycle));
            spectra.Add(Ms2(spectra.Count, 2, cycle, rt, second, 200 + cycle));
        }
        return new RawMeasurement { SpectrumList = spectra };
    }

    private static RawSpectrum Ms2(int index, int experiment, int cycle, double rt, double parent, double intensity) => new()
    {
        Index = index, MsLevel = 2, ExperimentID = experiment, ScanNumber = cycle, ScanStartTime = rt, ScanStartTimeUnit = Units.Minute,
        Precursor = new RawPrecursorIon { SelectedIonMz = parent, IsolationTargetMz = parent, IsolationWindowLowerOffset = 0.5, IsolationWindowUpperOffset = 0.5, CollisionEnergy = 35 },
        CollisionEnergy = 35,
        Spectrum = Peaks((184.0733, intensity), (86.0964, intensity / 2)),
    };

    private static RawPeakElement[] Peaks(params (double Mz, double Intensity)[] peaks) =>
        peaks.OrderBy(p => p.Mz).Select(p => new RawPeakElement { Mz = p.Mz, Intensity = p.Intensity }).ToArray();

    [Fact]
    public void The_gate_gathers_a_precursor_across_slots_in_time_order()
    {
        var raw = IdaLike();
        var gated = XicSources.Gated(raw, 313.2384, 0.05);
        Assert.Equal(6, gated.Count);
        Assert.Equal(new[] { 1, 2, 1, 2, 1, 2 }, gated.Select(i => raw.SpectrumList[i].ExperimentID));
        var rts = gated.Select(i => RawExplorer.RtMinutes(raw.SpectrumList[i])).ToList();
        Assert.Equal(rts.OrderBy(r => r), rts);

        var tight = XicSources.Gated(raw, 313.2384, 0.005);
        Assert.Equal(3, tight.Count);
        Assert.All(tight, i => Assert.Equal(1, raw.SpectrumList[i].ExperimentID));

        Assert.Empty(XicSources.Gated(raw, 500.0));
    }

    [Fact]
    public void All_ms2_is_every_product_scan_and_a_gated_xic_sums_their_fragment()
    {
        var raw = IdaLike();
        Assert.Equal(12, XicSources.Ms2All(raw).Count);
        var xic = RawExplorer.Xic(raw, XicSources.Gated(raw, 313.2384), 184.0733, 0.01);
        Assert.Equal(6, xic.Points.Count);
        // even cycles come from slot 1 (100+cycle), odd from slot 2 (200+cycle)
        Assert.Equal(new double[] { 100, 201, 102, 203, 104, 205 }, xic.Points.Select(p => p.Intensity));
    }

    [Fact]
    public void A_source_is_spelt_the_contracts_way()
    {
        Assert.Equal(XicSourceKind.Ms1, XicSource.Parse("ms1").Kind);
        Assert.Equal(XicSourceKind.Ms2All, XicSource.Parse("MS2-all").Kind);
        Assert.Equal(3, XicSource.Parse("channel:3").Channel);
        var gated = XicSource.Parse("gated:313.2384");
        Assert.Equal(313.2384, gated.GateMz);
        Assert.Equal(0.05, gated.GateTolerance);
        Assert.Equal(0.01, XicSource.Parse("gated:313.2384:0.01").GateTolerance);
        Assert.Equal("gated:313.2384:0.05", gated.ToString());
        Assert.Throws<FormatException>(() => XicSource.Parse("gated:"));
        Assert.Throws<FormatException>(() => XicSource.Parse("tic"));
    }

    [Fact]
    public void Resolve_answers_the_channels_by_index_and_the_survey_by_kind()
    {
        var raw = IdaLike();
        var channels = RawExplorer.DiscoverChannels(raw);
        Assert.Equal(3, channels.Count);
        Assert.Equal(6, XicSources.Resolve(raw, channels, XicSource.Ms1).Count);
        Assert.Equal(channels[1].SpectrumIndices, XicSources.Resolve(raw, channels, XicSource.OfChannel(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => XicSources.Resolve(raw, channels, XicSource.OfChannel(9)));
        var precursors = XicSources.Precursors(raw, XicSources.Ms2All(raw));
        Assert.Equal(3, precursors.Count);
        Assert.Equal(6, precursors.First(p => p.Mz == 401.1).Scans);
    }

    [Fact]
    public void A_mass_list_takes_commas_spaces_and_semicolons()
    {
        var masses = XicSources.ParseMzList("183.1391, 313.2384;401.1 500", out var rejected);
        Assert.Equal(new[] { 183.1391, 313.2384, 401.1, 500.0 }, masses);
        Assert.Empty(rejected);
        masses = XicSources.ParseMzList("abc 12.5 -3", out rejected);
        Assert.Equal(new[] { 12.5 }, masses);
        Assert.Equal(new[] { "abc", "-3" }, rejected);
    }

    [Fact]
    public void The_precursor_of_a_scan_is_described_per_scan()
    {
        var raw = IdaLike();
        Assert.Equal(string.Empty, RawExplorer.DescribePrecursor(raw, 0));
        Assert.Equal("precursor 313.2384 (isolation ± 0.50), CE 35, slot 1", RawExplorer.DescribePrecursor(raw, 1));
        Assert.Equal("precursor 401.1000 (isolation ± 0.50), CE 35, slot 2", RawExplorer.DescribePrecursor(raw, 2));
    }
}
