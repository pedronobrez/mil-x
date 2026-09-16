using CompMs.RawDataHandler.Core;
using MilX.RawData.Tests.TestSupport;
using MilX.RawData.Vendor;
using Xunit;

namespace MilX.RawData.Tests;

public class VendorConverterTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "milx-vendor-" + Guid.NewGuid().ToString("N"));

    public VendorConverterTests() {
        Directory.CreateDirectory(_dir);
    }

    public void Dispose() {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static VendorConverterOptions NoConverter() => new() {
        MsconvertPath = Path.Combine(Path.GetTempPath(), "definitely-not-msconvert-" + Guid.NewGuid().ToString("N")),
        DockerPath = Path.Combine(Path.GetTempPath(), "definitely-not-docker-" + Guid.NewGuid().ToString("N")),
        Log = _ => { },
    };

    [Fact]
    public void Vendor_extensions_are_recognised_only_when_the_path_exists() {
        var raw = Path.Combine(_dir, "sample.raw");
        Assert.False(VendorConverter.IsVendorPath(raw));
        File.WriteAllText(raw, "x");
        Assert.True(VendorConverter.IsVendorPath(raw));
        var d = Path.Combine(_dir, "sample.d");
        Directory.CreateDirectory(d);
        Assert.True(VendorConverter.IsVendorPath(d));
        Assert.False(VendorConverter.IsVendorPath(Path.Combine(_dir, "sample.mzML")));
    }

    [Fact]
    public void Cached_mzml_next_to_vendor_file_is_used_without_a_converter() {
        var raw = Path.Combine(_dir, "sample.raw");
        File.WriteAllText(raw, "fake vendor content");
        var mzml = new MzmlTestWriter().Write(Path.Combine(_dir, "sample.mzML"), new List<TestSpectrum> {
            new() { MsLevel = 1, RtSeconds = 1, Mz = new[] { 100.0, 200.0 }, Intensity = new[] { 1.0, 2.0 } },
        });
        File.SetLastWriteTimeUtc(mzml, DateTime.UtcNow.AddMinutes(1));
        var converter = new VendorConverter(NoConverter());
        Assert.Equal(mzml, converter.EnsureMzml(raw));

        // and the whole access layer reads the vendor path transparently
        var previous = RawDataAccessOptions.Vendor;
        RawDataAccessOptions.Vendor = converter;
        try {
            using var access = new RawDataAccess(raw, 0, false, false, true);
            Assert.Equal(RawDataExtension.raw, access.Extension);
            var m = access.GetMeasurement();
            Assert.NotNull(m);
            Assert.Single(m!.SpectrumList);
        }
        finally {
            RawDataAccessOptions.Vendor = previous;
        }
    }

    [Fact]
    public void Missing_converter_gives_actionable_error() {
        var raw = Path.Combine(_dir, "other.raw");
        File.WriteAllText(raw, "fake vendor content");
        var converter = new VendorConverter(NoConverter());
        var availability = converter.Detect();
        Assert.False(availability.IsAvailable);
        var ex = Assert.Throws<NotSupportedException>(() => converter.EnsureMzml(raw));
        Assert.Contains("msconvert", ex.Message);
        Assert.Contains("Docker", ex.Message);
    }

    [Fact]
    public void Cache_directory_option_redirects_output_location() {
        var cache = Path.Combine(_dir, "cache");
        var converter = new VendorConverter(new VendorConverterOptions { CacheDirectory = cache, Log = _ => { } });
        var raw = Path.Combine(_dir, "run1.wiff");
        Assert.Equal(Path.Combine(cache, "run1.mzML"), converter.GetCachedMzmlPath(raw));
    }
}
