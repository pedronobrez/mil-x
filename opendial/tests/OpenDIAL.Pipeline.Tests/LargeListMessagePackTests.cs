using CompMs.Common.Components;
using CompMs.Common.MessagePack;
using Xunit;

namespace OpenDIAL.Pipeline.Tests;

/// <summary>
/// A .mdproject stores its libraries as LZ4-compressed MessagePack inside a zip entry, and reading
/// that entry goes through a DeflateStream, which returns short reads on a large buffer. The reader
/// used to issue a single Read and hand the partly-filled buffer to the unsafe LZ4 decoder, which
/// walked past the valid data and killed the process with an AccessViolationException — opening a
/// processed project with a large library crashed the application outright.
/// </summary>
public class LargeListMessagePackTests
{
    /// <summary>A stream that never returns more than a few bytes per Read, like a DeflateStream.</summary>
    private sealed class DribblingStream : Stream
    {
        private readonly MemoryStream _inner;
        private readonly int _max;
        public DribblingStream(byte[] data, int max) { _inner = new MemoryStream(data); _max = max; }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, Math.Min(count, _max));
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public void Round_trips_a_large_list_through_a_stream_that_returns_short_reads()
    {
        // big enough that the compressed payload cannot arrive in one Read
        var original = Enumerable.Range(0, 40000)
            .Select(i => new SpectrumPeak(100.0 + i * 0.001, i % 997 + 1))
            .ToList();

        using var buffer = new MemoryStream();
        LargeListMessagePack.Serialize(buffer, original);
        var bytes = buffer.ToArray();
        Assert.True(bytes.Length > 65536, "the payload must exceed one buffer for this to be a real test");

        foreach (var chunk in new[] { 1, 17, 4096, 65536 })
        {
            using var stream = new DribblingStream(bytes, chunk);
            var restored = LargeListMessagePack.Deserialize<SpectrumPeak>(stream);
            Assert.Equal(original.Count, restored.Count);
            Assert.Equal(original[0].Mass, restored[0].Mass, 6);
            Assert.Equal(original[^1].Mass, restored[^1].Mass, 6);
            Assert.Equal(original[12345].Intensity, restored[12345].Intensity, 6);
        }
    }
}
