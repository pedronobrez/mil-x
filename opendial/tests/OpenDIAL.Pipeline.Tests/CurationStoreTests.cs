using OpenDIAL.Pipeline.Curation;
using Xunit;

namespace OpenDIAL.Pipeline.Tests;

public class CurationStoreTests
{
    private static string NewAlignmentPath() =>
        Path.Combine(Path.GetTempPath(), "opendial-curation-" + Guid.NewGuid().ToString("N"), "AlignResult-1.arf");

    [Fact]
    public void Reads_the_tag_file_MS_DIAL_writes()
    {
        var alignment = NewAlignmentPath();
        Directory.CreateDirectory(Path.GetDirectoryName(alignment)!);
        File.WriteAllText(CurationStore.TagFileFor(alignment), """
        <?xml version="1.0" encoding="utf-8"?>
        <PeakSpotTags>
          <Definitions>
            <Tag><Id>1</Id><Label>Confirmed</Label></Tag>
            <Tag><Id>3</Id><Label>Misannotation</Label></Tag>
          </Definitions>
          <Peaks>
            <Peak Id="463"><Tag>1</Tag></Peak>
            <Peak Id="272"><Tag>3</Tag></Peak>
            <Peak Id="660"><Tag>2</Tag><Tag>4</Tag></Peak>
          </Peaks>
        </PeakSpotTags>
        """);
        try
        {
            var store = CurationStore.Load(alignment);
            Assert.True(store.HasTag(463, PeakSpotTagKind.Confirmed));
            Assert.True(store.HasTag(272, PeakSpotTagKind.Misannotation));
            Assert.Equal(2, store.TagsOf(660).Count);
            Assert.True(store.HasTag(660, PeakSpotTagKind.LowQualitySpectrum));
            Assert.True(store.HasTag(660, PeakSpotTagKind.Coelution));
            Assert.False(store.HasTag(463, PeakSpotTagKind.Misannotation));
            Assert.False(store.IsDirty);
            Assert.Equal(1, store.TaggedCount(PeakSpotTagKind.Confirmed));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(alignment)!, true);
        }
    }

    [Fact]
    public void Writes_a_tag_file_MS_DIAL_can_read_back()
    {
        var alignment = NewAlignmentPath();
        Directory.CreateDirectory(Path.GetDirectoryName(alignment)!);
        try
        {
            var store = CurationStore.Load(alignment);
            store.SetTag(7, PeakSpotTagKind.Confirmed, true);
            store.SetTag(9, PeakSpotTagKind.Overannotation, true);
            store.SetTag(9, PeakSpotTagKind.Coelution, true);
            store.SetComment(7, "class fragment present, both chains matched");
            store.SetManualName(9, "PC 34:1");
            Assert.True(store.IsDirty);
            store.Save();
            Assert.False(store.IsDirty);

            // MS-DIAL's schema: one Definitions block of all five tags, then a Peak per tagged spot
            var xml = File.ReadAllText(store.TagFilePath);
            Assert.Contains("<PeakSpotTags>", xml);
            Assert.Contains("<Label>Coelution (mixed spectra)</Label>", xml);
            Assert.Contains("<Peak Id=\"7\">", xml);
            Assert.DoesNotContain("Comment", xml);   // comments stay out of the file MS-DIAL owns

            var reopened = CurationStore.Load(alignment);
            Assert.True(reopened.HasTag(7, PeakSpotTagKind.Confirmed));
            Assert.Equal(2, reopened.TagsOf(9).Count);
            Assert.Equal("class fragment present, both chains matched", reopened.Get(7).Comment);
            Assert.Equal("PC 34:1", reopened.Get(9).ManualName);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(alignment)!, true);
        }
    }

    [Fact]
    public void Clearing_the_last_tag_leaves_no_stale_entry()
    {
        var alignment = NewAlignmentPath();
        Directory.CreateDirectory(Path.GetDirectoryName(alignment)!);
        try
        {
            var store = CurationStore.Load(alignment);
            store.SetTag(3, PeakSpotTagKind.Misannotation, true);
            store.Save();
            store.ClearTags(3);
            store.Save();
            Assert.DoesNotContain("<Peak ", File.ReadAllText(store.TagFilePath));
            Assert.Empty(CurationStore.Load(alignment).TagsOf(3));
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(alignment)!, true);
        }
    }
}
