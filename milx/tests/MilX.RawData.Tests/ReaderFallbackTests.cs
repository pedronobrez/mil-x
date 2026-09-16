using CompMs.Common.DataObj;
using CompMs.RawDataHandler.Core;
using MilX.RawData.Plugins;
using Xunit;

namespace MilX.RawData.Tests;

/// <summary>
/// A plugin that claims a file and then cannot read it must not fail the run: the file goes on to
/// the vendor bridge, and the log says why.
/// </summary>
public class ReaderFallbackTests
{
    private sealed class ClaimsAndFails : IRawFileReaderPlugin
    {
        public string Name => "claims and fails";
        public int Priority => 5;
        public bool CanRead(string path) => Path.GetFileName(path) == "fallback-probe.raw";
        public RawMeasurement Read(string path, int fileId, RawReadOptions options) => throw new NotSupportedException("this reader cannot open the file on this platform");
    }

    [Fact]
    public void A_plugin_that_fails_hands_the_file_to_the_vendor_bridge()
    {
        var dir = Path.Combine(Path.GetTempPath(), "milx-fallback-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "fallback-probe.raw");
        File.WriteAllBytes(path, new byte[16]);
        var log = new List<string>();
        var previousLog = RawDataAccessOptions.Log;
        var previousImplicit = RawDataAccessOptions.AllowImplicitVendorConversion;
        RawDataAccessOptions.Log = log.Add;
        RawDataAccessOptions.AllowImplicitVendorConversion = false;   // the bridge is not run here; what matters is that it is reached
        RawReaderPlugins.Register(new ClaimsAndFails());
        try
        {
            using var access = new RawDataAccess(path, 0, false, false, false);
            var measurement = access.GetMeasurement();
            Assert.Null(measurement);
            Assert.Contains(log, l => l.Contains("could not read fallback-probe.raw", StringComparison.Ordinal) && l.Contains("trying the vendor bridge", StringComparison.Ordinal));
            Assert.Contains(log, l => l.Contains("requires conversion to mzML", StringComparison.Ordinal));
        }
        finally
        {
            RawDataAccessOptions.Log = previousLog;
            RawDataAccessOptions.AllowImplicitVendorConversion = previousImplicit;
            Directory.Delete(dir, recursive: true);
        }
    }
}
