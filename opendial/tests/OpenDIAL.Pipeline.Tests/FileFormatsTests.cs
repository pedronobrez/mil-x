using OpenDIAL.Pipeline.Vendor;
using Xunit;

namespace OpenDIAL.Pipeline.Tests;

/// <summary>One injection per acquisition: SCIEX OS's .wiff and .wiff2 pair is one file, not two.</summary>
public class FileFormatsTests
{
    [Fact]
    public void A_wiff2_beside_its_wiff_is_the_same_acquisition_and_is_dropped()
    {
        var dir = Path.Combine(Path.GetTempPath(), "opendial-formats-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            foreach (var name in new[] { "a.wiff", "a.wiff2", "a.wiff.scan", "b.wiff2", "c.mzML", "notes.txt" })
                File.WriteAllBytes(Path.Combine(dir, name), new byte[4]);

            var found = FileFormats.EnumerateRawFiles(dir).Select(Path.GetFileName).ToList();
            Assert.Equal(new[] { "a.wiff", "b.wiff2", "c.mzML" }, found);

            Assert.NotNull(FileFormats.WhyDropped(Path.Combine(dir, "a.wiff2")));
            Assert.Contains("a.wiff", FileFormats.WhyDropped(Path.Combine(dir, "a.wiff2"))!);
            Assert.Null(FileFormats.WhyDropped(Path.Combine(dir, "b.wiff2")));
            Assert.Null(FileFormats.WhyDropped(Path.Combine(dir, "a.wiff")));

            // the rule holds for files handed over one by one, whichever order they come in
            var picked = FileFormats.OnePerAcquisition(new[] { Path.Combine(dir, "a.wiff2"), Path.Combine(dir, "a.wiff"), Path.Combine(dir, "b.wiff2") }).Select(Path.GetFileName).ToList();
            Assert.Equal(new[] { "a.wiff", "b.wiff2" }, picked);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
