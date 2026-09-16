using CompMs.Common.DataObj;
using CompMs.RawDataHandler.Core;
using MilX.RawData.Mzml;
using MilX.RawData.Tests.TestSupport;
using Xunit;

namespace MilX.RawData.Tests;

public class MzmlReaderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "milx-tests-" + Guid.NewGuid().ToString("N"));

    public MzmlReaderTests() {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static List<TestSpectrum> DdaCycle(int cycles = 3) {
        var list = new List<TestSpectrum>();
        for (var c = 0; c < cycles; c++) {
            var t = c * 0.6;
            list.Add(new TestSpectrum {
                MsLevel = 1, RtSeconds = t,
                Mz = new[] { 100.1, 200.2, 300.3, 400.4, 945.995 },
                Intensity = new[] { 10.0, 500.0, 20.0, 30.0, 40.0 },
            });
            list.Add(new TestSpectrum {
                MsLevel = 2, RtSeconds = t + 0.2, PrecursorMz = 200.2, IsolationTarget = 200.2, CollisionEnergy = 30,
                Mz = new[] { 80.05, 120.1, 200.2 },
                Intensity = new[] { 100.0, 60.0, 5.0 },
            });
        }
        return list;
    }

    private RawMeasurement Read(string path) {
        using var access = new RawDataAccess(path, 0, false, false, true);
        var m = access.GetMeasurement();
        Assert.NotNull(m);
        return m!;
    }

    [Theory]
    [InlineData(true, ArrayEncoding.Float64, ArrayEncoding.Float32)]
    [InlineData(false, ArrayEncoding.Float64, ArrayEncoding.Float32)]
    [InlineData(true, ArrayEncoding.Float32, ArrayEncoding.Float64)]
    [InlineData(false, ArrayEncoding.NumpressLinear, ArrayEncoding.NumpressSlof)]
    [InlineData(true, ArrayEncoding.NumpressLinear, ArrayEncoding.NumpressPic)]
    [InlineData(false, ArrayEncoding.NumpressLinear, ArrayEncoding.NumpressPic)]
    public void Reads_every_peak_for_all_encodings(bool zlib, ArrayEncoding mzEnc, ArrayEncoding intEnc) {
        var writer = new MzmlTestWriter { Zlib = zlib, MzEncoding = mzEnc, IntensityEncoding = intEnc };
        var path = writer.Write(Path.Combine(_dir, $"dda_{zlib}_{mzEnc}_{intEnc}.mzML"), DdaCycle());
        var m = Read(path);

        Assert.Equal(6, m.SpectrumList.Count);
        var ms1 = m.SpectrumList[0];
        Assert.Equal(1, ms1.MsLevel);
        // the last peak (the highest m/z) must survive: the closed reader drops it
        Assert.Equal(5, ms1.Spectrum.Length);
        Assert.Equal(5, ms1.DefaultArrayLength);
        Assert.Equal(945.995, ms1.Spectrum[4].Mz, mzEnc == ArrayEncoding.Float32 ? 3 : 4);
        Assert.Equal(945.995, ms1.HighestObservedMz, mzEnc == ArrayEncoding.Float32 ? 3 : 4);
        Assert.Equal(100.1, ms1.LowestObservedMz, 3);
        // base peak is the most intense peak, not the highest m/z
        Assert.Equal(200.2, ms1.BasePeakMz, 3);
        Assert.Equal(500.0, ms1.BasePeakIntensity, intEnc == ArrayEncoding.NumpressSlof ? 0 : 3);
        Assert.Equal(10.0, ms1.MinIntensity, intEnc == ArrayEncoding.NumpressSlof ? 0 : 3);
    }

    [Fact]
    public void Converts_seconds_to_minutes_and_keeps_minutes() {
        var spectra = DdaCycle(1);
        spectra[0].RtSeconds = 90; // 1.5 min
        spectra[1].RtSeconds = 120; spectra[1].RtInMinutes = true; // written as 2 min
        var path = new MzmlTestWriter().Write(Path.Combine(_dir, "rt.mzML"), spectra);
        var m = Read(path);
        Assert.Equal(1.5, m.SpectrumList[0].ScanStartTime, 6);
        Assert.Equal(Units.Minute, m.SpectrumList[0].ScanStartTimeUnit);
        Assert.Equal(2.0, m.SpectrumList[1].ScanStartTime, 6);
        Assert.Equal(Units.Minute, m.SpectrumList[1].ScanStartTimeUnit);
    }

    [Fact]
    public void Spectra_are_sorted_and_reindexed_positionally() {
        var spectra = DdaCycle(2);
        // shuffle: write later cycle first
        var shuffled = new List<TestSpectrum> { spectra[2], spectra[3], spectra[0], spectra[1] };
        var path = new MzmlTestWriter().Write(Path.Combine(_dir, "order.mzML"), shuffled);
        var m = Read(path);
        for (var i = 0; i < m.SpectrumList.Count; i++) {
            Assert.Equal(i, m.SpectrumList[i].Index);
            Assert.Equal(i, m.SpectrumList[i].ScanNumber);
            Assert.Equal(i, m.SpectrumList[i].OriginalIndex);
            if (i > 0) Assert.True(m.SpectrumList[i - 1].ScanStartTime <= m.SpectrumList[i].ScanStartTime);
        }
        Assert.Equal(1, m.SpectrumList[0].MsLevel);
        Assert.Equal(2, m.SpectrumList[1].MsLevel);
    }

    [Fact]
    public void Precursor_fields_and_collision_energy_are_populated() {
        var path = new MzmlTestWriter().Write(Path.Combine(_dir, "pre.mzML"), DdaCycle(1));
        var m = Read(path);
        var ms2 = m.SpectrumList[1];
        Assert.NotNull(ms2.Precursor);
        Assert.Equal(200.2, ms2.Precursor.SelectedIonMz, 6);
        Assert.Equal(200.2, ms2.Precursor.IsolationTargetMz, 6);
        Assert.Equal(0.5, ms2.Precursor.IsolationWindowLowerOffset, 6);
        Assert.Equal(0.5, ms2.Precursor.IsolationWindowUpperOffset, 6);
        Assert.Equal(30.0, ms2.Precursor.CollisionEnergy, 6);
        Assert.Equal(Units.ElectronVolt, ms2.Precursor.CollisionEnergyUnit);
        Assert.Equal(DissociationMethods.HCD, ms2.Precursor.Dissociationmethod);
        // MIL-X deviation: activation CE is surfaced on the spectrum and in the CE target list
        Assert.Equal(30.0, ms2.CollisionEnergy, 6);
        Assert.Equal(new List<double> { 30.0 }, m.CollisionEnergyTargets);
        Assert.Null(m.SpectrumList[0].Precursor);
    }

    [Fact]
    public void Selected_ion_falls_back_to_isolation_target() {
        var spectra = DdaCycle(1);
        spectra[1].OmitSelectedIon = true;
        spectra[1].IsolationTarget = 201.0;
        var path = new MzmlTestWriter().Write(Path.Combine(_dir, "fallback.mzML"), spectra);
        var m = Read(path);
        Assert.Equal(201.0, m.SpectrumList[1].Precursor.SelectedIonMz, 6);
    }

    [Fact]
    public void Referenceable_param_groups_apply_polarity() {
        var writer = new MzmlTestWriter();
        writer.ParamGroups["neg"] = "<cvParam cvRef=\"MS\" accession=\"MS:1000129\" name=\"negative scan\" value=\"\"/>";
        var spectra = DdaCycle(1);
        spectra[0].ParamGroupRef = "neg";
        spectra[1].ParamGroupRef = "SpectrumParamsNegative"; // MS-DIAL's undeclared group name
        var path = writer.Write(Path.Combine(_dir, "groups.mzML"), spectra);
        var m = Read(path);
        Assert.Equal(ScanPolarity.Negative, m.SpectrumList[0].ScanPolarity);
        Assert.Equal(ScanPolarity.Negative, m.SpectrumList[1].ScanPolarity);
    }

    [Fact]
    public void Ion_mobility_drift_time_is_read() {
        var spectra = DdaCycle(1);
        spectra[0].DriftTimeMs = 12.5;
        spectra[1].InverseK0 = 1.05;
        var path = new MzmlTestWriter().Write(Path.Combine(_dir, "im.mzML"), spectra);
        var reader = new MzmlReader();
        var m = reader.ReadMzml(path, 0, true);
        Assert.True(reader.IsIonMobilityData);
        Assert.Equal(12.5, m.SpectrumList[0].DriftTime, 6);
        Assert.Equal(Units.Milliseconds, m.SpectrumList[0].DriftTimeUnit);
        Assert.Equal(1.05, m.SpectrumList[1].DriftTime, 6);
        Assert.Equal(Units.Oneoverk0, m.SpectrumList[1].DriftTimeUnit);
    }

    [Fact]
    public void Empty_spectrum_uses_scan_window_limits() {
        var spectra = DdaCycle(1);
        spectra[0].Mz = Array.Empty<double>();
        spectra[0].Intensity = Array.Empty<double>();
        var path = new MzmlTestWriter().Write(Path.Combine(_dir, "empty.mzML"), spectra);
        var m = Read(path);
        var s = m.SpectrumList[0];
        Assert.Empty(s.Spectrum);
        Assert.Equal(0, s.DefaultArrayLength);
        Assert.Equal(50.0, s.LowestObservedMz, 6);
        Assert.Equal(1500.0, s.HighestObservedMz, 6);
    }

    [Fact]
    public void Missing_tic_is_computed_and_chromatograms_are_read() {
        var writer = new MzmlTestWriter { WriteTic = false, WriteChromatogram = true };
        var path = writer.Write(Path.Combine(_dir, "tic.mzML"), DdaCycle(1));
        var m = Read(path);
        Assert.Equal(600.0, m.SpectrumList[0].TotalIonCurrent, 3);
        Assert.Single(m.ChromatogramList);
        var c = m.ChromatogramList[0];
        Assert.Equal("TIC", c.Id);
        Assert.Equal(3, c.Chromatogram.Length);
        Assert.Equal(0.5, c.Chromatogram[1].RtInMin, 6); // 30 s
        Assert.Equal(20.0, c.Chromatogram[1].Intensity, 3);
    }

    [Fact]
    public void Non_indexed_mzml_is_read() {
        var path = new MzmlTestWriter { Indexed = false }.Write(Path.Combine(_dir, "plain.mzML"), DdaCycle(2));
        var m = Read(path);
        Assert.Equal(4, m.SpectrumList.Count);
    }

    [Fact]
    public void Rt_correction_list_is_applied() {
        var path = new MzmlTestWriter().Write(Path.Combine(_dir, "rtc.mzML"), DdaCycle(1));
        var corrected = new List<double> { 5.0, 5.1 };
        using var access = new RawDataAccess(path, 0, false, false, true, corrected);
        var m = access.GetMeasurement()!;
        Assert.Equal(5.0, m.SpectrumList[0].ScanStartTime, 6);
        Assert.Equal(5.1, m.SpectrumList[1].ScanStartTime, 6);
    }

    [Fact]
    public void Unsupported_extension_returns_null() {
        var path = Path.Combine(_dir, "x.unknown");
        File.WriteAllText(path, "nope");
        using var access = new RawDataAccess(path, 0, false, false, true);
        Assert.False(access.IsDataSupported);
        Assert.Null(access.GetMeasurement());
    }

    [Fact]
    public void Extension_mapping_matches_closed_reader() {
        Assert.Equal(RawDataExtension.mzml, new RawDataAccess("a.MZML", 0, false, false, true).Extension);
        Assert.Equal(RawDataExtension.raw, new RawDataAccess("a.raw", 0, false, false, true).Extension);
        Assert.Equal(RawDataExtension.d, new RawDataAccess("a.d", 0, false, false, true).Extension);
        Assert.Equal(RawDataExtension.wiff, new RawDataAccess("a.wiff", 0, false, false, true).Extension);
        Assert.Equal(RawDataExtension.wiff2, new RawDataAccess("a.wiff2", 0, false, false, true).Extension);
        Assert.Equal(RawDataExtension.ibf, new RawDataAccess("a.iabf", 0, false, false, true).Extension);
        Assert.Equal(RawDataExtension.abf, new RawDataAccess("a.abf", 0, false, false, true).Extension);
        Assert.Equal(RawDataExtension.cdf, new RawDataAccess("a.cdf", 0, false, false, true).Extension);
    }
}

public class NumpressTests
{
    [Fact]
    public void Linear_round_trip() {
        var data = new[] { 100.0001, 100.0023, 100.0050, 250.12345, 250.12346, 999.99999, 1000.0 };
        var decoded = Numpress.DecodeLinear(NumpressEncoder.EncodeLinear(data, 100000.0));
        Assert.Equal(data.Length, decoded.Length);
        for (var i = 0; i < data.Length; i++) Assert.Equal(data[i], decoded[i], 4);
    }

    [Fact]
    public void Linear_short_inputs() {
        Assert.Empty(Numpress.DecodeLinear(NumpressEncoder.EncodeLinear(Array.Empty<double>(), 1000)));
        Assert.Single(Numpress.DecodeLinear(NumpressEncoder.EncodeLinear(new[] { 42.5 }, 1000)));
        var two = Numpress.DecodeLinear(NumpressEncoder.EncodeLinear(new[] { 42.5, 43.25 }, 1000));
        Assert.Equal(new[] { 42.5, 43.25 }, two);
    }

    [Fact]
    public void Pic_round_trip() {
        var data = new[] { 0.0, 1.0, 15.0, 16.0, 255.0, 4096.0, 1_000_000.0, 123456789.0 };
        var decoded = Numpress.DecodePic(NumpressEncoder.EncodePic(data));
        Assert.Equal(data, decoded);
    }

    [Fact]
    public void Slof_round_trip_is_within_relative_error() {
        var data = new[] { 1.0, 10.0, 100.0, 1000.0, 12345.678, 5e6 };
        var fp = 65535.0 / Math.Log(5e6 + 1);
        var decoded = Numpress.DecodeSlof(NumpressEncoder.EncodeSlof(data, fp));
        Assert.Equal(data.Length, decoded.Length);
        for (var i = 0; i < data.Length; i++) Assert.InRange(decoded[i] / data[i], 0.999, 1.001);
    }
}

public class SpectrumParserTests
{
    [Fact]
    public void Accumulated_ms1_merges_drift_scans_per_frame() {
        var spectra = new List<RawSpectrum>();
        for (var frame = 0; frame < 2; frame++) {
            for (var drift = 0; drift < 3; drift++) {
                spectra.Add(new RawSpectrum {
                    MsLevel = 1, ScanNumber = frame, DriftScanNumber = drift, ScanStartTime = frame, DriftTime = drift,
                    Spectrum = new[] { new RawPeakElement { Mz = 100.0001 + drift * 0.0001, Intensity = 10 }, new RawPeakElement { Mz = 200.5, Intensity = 5 } },
                    LowestObservedMz = 100, HighestObservedMz = 201,
                });
            }
        }
        var acc = SpectrumParser.GetAccumulatedMs1Spectrum(spectra);
        Assert.Equal(2, acc.Count);
        Assert.Equal(2, acc[0].Spectrum.Length); // bins of 1 mDa: 100.000x collapse into one bin
        Assert.Equal(30.0, acc[0].Spectrum[0].Intensity, 3);
        Assert.Equal(15.0, acc[0].Spectrum[1].Intensity, 3);
        Assert.Equal(45.0, acc[0].TotalIonCurrent, 3);
    }

    [Fact]
    public void GetRawMeasurementObj_sorts_by_time_drift_level() {
        var s = new List<RawSpectrum> {
            new() { ScanStartTime = 2, MsLevel = 1 },
            new() { ScanStartTime = 1, MsLevel = 2, CollisionEnergy = 20 },
            new() { ScanStartTime = 1, MsLevel = 1 },
        };
        var m = SpectrumParser.GetRawMeasurementObj("/tmp/x.mzML", 3, s);
        Assert.Equal("x", m.SourceFileInfo.Name);
        Assert.Equal("3", m.Sample.Id);
        Assert.Equal(new[] { 1, 2, 1 }, m.SpectrumList.Select(x => x.MsLevel).ToArray());
        Assert.Equal(new[] { 0, 1, 2 }, m.SpectrumList.Select(x => x.Index).ToArray());
        Assert.Equal(new List<double> { 20 }, m.CollisionEnergyTargets);
    }
}
