using ActualLab.Compression;
using K4os.Compression.LZ4;

namespace ActualLab.Tests.Compression;

/// <summary>
/// The contract every <see cref="ByteCompressor"/>/<see cref="ByteDecompressor"/> pair must satisfy.
/// Derive one test class per codec; codec-specific cases go in the derived class.
/// </summary>
public abstract class ByteCompressorTestBase(ITestOutputHelper @out) : TestBase(@out)
{
    protected const int MaxLength = 1 << 22;

    protected abstract ByteCompressor NewCompressor();
    protected abstract ByteDecompressor NewDecompressor();

    [Fact]
    public void SingleFrameTest()
    {
        using var pair = NewPair();
        var source = NewPayload(0, 1000);

        pair.RoundTrip(source).Should().Equal(source);
    }

    // The point of the whole design: frame N compresses against the dictionary frames 0..N-1 built.
    [Fact]
    public void ContextIsRetainedAcrossFramesTest()
    {
        using var pair = NewPair();
        var source = NewPayload(0, 2000);

        // Every compressed frame must be fed to the decompressor, in order - skipping one
        // desynchronizes the pair, which is exactly what the frame flag byte prevents.
        pair.RoundTrip(source).Should().Equal(source);
        var firstSize = pair.LastCompressedSize;
        var lastSize = firstSize;
        for (var i = 0; i < 10; i++) {
            pair.RoundTrip(source).Should().Equal(source);
            lastSize = pair.LastCompressedSize;
        }

        Out.WriteLine($"First frame: {firstSize} bytes, 11th frame: {lastSize} bytes");
        lastSize.Should().BeLessThan(firstSize);
    }

    [Fact]
    public void VaryingFramesTest()
    {
        using var pair = NewPair();
        for (var i = 0; i < 50; i++) {
            var source = NewPayload(i, 1 + (i * 37 % 5000));
            pair.RoundTrip(source).Should().Equal(source);
        }
    }

    [Fact]
    public void EmptyFrameTest()
    {
        using var pair = NewPair();

        pair.RoundTrip([]).Should().BeEmpty();
        var source = NewPayload(1, 100);
        pair.RoundTrip(source).Should().Equal(source);
    }

    [Fact]
    public void ManyTinyFramesTest()
    {
        using var pair = NewPair();
        for (var i = 0; i < 2000; i++) {
            var source = new byte[] { (byte)i, (byte)(i >> 8) };
            pair.RoundTrip(source).Should().Equal(source);
        }
    }

    [Fact]
    public void IncompressibleFramesTest()
    {
        using var pair = NewPair();
        var random = new Random(42);
        for (var i = 0; i < 10; i++) {
            var source = new byte[10_000];
            random.NextBytes(source);
            pair.RoundTrip(source).Should().Equal(source);
        }
    }

    [Fact]
    public void RandomSizedFramesTest()
    {
        using var pair = NewPair();
        var random = new Random(1234);
        for (var i = 0; i < 300; i++) {
            var length = random.Next(0, 20_000);
            var source = new byte[length];
            if (i % 3 == 0)
                random.NextBytes(source); // Incompressible
            else
                source = NewPayload(i, length);

            pair.RoundTrip(source).Should().Equal(source);
        }
    }

    // Frames larger than an internal block size must survive the multi-block path
    [Fact]
    public void LargeFramesTest()
    {
        using var pair = NewPair();
        for (var i = 0; i < 3; i++) {
            var source = NewPayload(i, 300_000);
            pair.RoundTrip(source).Should().Equal(source);
        }
    }

    [Fact]
    public void MultiMegabyteFrameTest()
    {
        using var pair = NewPair();
        var source = NewPayload(0, 3_000_000);

        pair.RoundTrip(source).Should().Equal(source);
    }

    // Both sides must reset on the same frame boundary, otherwise the stream desynchronizes.
    [Fact]
    public void ResetTest()
    {
        using var pair = NewPair();
        var source = NewPayload(0, 2000);

        pair.RoundTrip(source).Should().Equal(source);
        pair.RoundTrip(source).Should().Equal(source);
        var beforeResetSize = pair.LastCompressedSize;

        pair.Compressor.Reset();
        pair.Decompressor.Reset();

        pair.RoundTrip(source).Should().Equal(source);
        // A reset drops the dictionary, so the same frame no longer compresses to almost nothing
        pair.LastCompressedSize.Should().BeGreaterThan(beforeResetSize);
    }

    [Fact]
    public void RepeatedResetCyclesTest()
    {
        using var pair = NewPair();
        for (var cycle = 0; cycle < 20; cycle++) {
            for (var i = 0; i < 5; i++) {
                var source = NewPayload(cycle, 1000 + i);
                pair.RoundTrip(source).Should().Equal(source);
            }
            pair.Compressor.Reset();
            pair.Decompressor.Reset();
        }
    }

    [Fact]
    public void DecompressionBombTest()
    {
        using var pair = NewPair();
        var source = new byte[100_000]; // All zeros - compresses to almost nothing
        var compressed = pair.Compress(source);
        compressed.Length.Should().BeLessThan(2000);

        Assert.Throws<FormatException>(() => pair.Decompress(compressed, 10_000));
    }

    [Fact]
    public void MaxLengthBoundaryTest()
    {
        using var pair = NewPair();
        var source = NewPayload(0, 1000);
        var compressed = pair.Compress(source);

        // Exactly at the limit is fine
        pair.Decompress(compressed, source.Length).Should().Equal(source);
    }

    [Fact]
    public void MaxLengthExceededByOneByteTest()
    {
        using var pair = NewPair();
        var source = NewPayload(0, 1000);
        var compressed = pair.Compress(source);

        Assert.Throws<FormatException>(() => pair.Decompress(compressed, source.Length - 1));
    }

    // Whatever a codec does with a corrupted frame, it must never silently return the original
    [Fact]
    public void CorruptedFrameIsNeverSilentlyAcceptedTest()
    {
        using var pair = NewPair();
        var source = NewPayload(0, 4000);
        var compressed = pair.Compress(source);
        compressed[compressed.Length / 2] ^= 0xFF;

        try {
            pair.Decompress(compressed, MaxLength).Should().NotEqual(source);
        }
        catch (Exception e) {
            Out.WriteLine($"Rejected with {e.GetType().Name}");
        }
    }

    // A reset by only one side must not silently produce the right bytes either
    [Fact]
    public void OneSidedResetIsNeverSilentlyAcceptedTest()
    {
        using var pair = NewPair();
        var source = NewPayload(0, 4000);

        pair.RoundTrip(source).Should().Equal(source);
        pair.Compressor.Reset(); // Decompressor deliberately not reset

        var compressed = pair.Compress(source);
        try {
            pair.Decompress(compressed, MaxLength).Should().NotEqual(source);
        }
        catch (Exception e) {
            Out.WriteLine($"Rejected with {e.GetType().Name}");
        }
    }

    [Fact]
    public void DoubleDisposeIsSafeTest()
    {
        var compressor = NewCompressor();
        var decompressor = NewDecompressor();

        compressor.Dispose();
        compressor.Dispose();
        decompressor.Dispose();
        decompressor.Dispose();
    }

    // Protected methods

    protected CodecPair NewPair()
        => new(NewCompressor(), NewDecompressor());

    protected static byte[] NewPayload(int seed, int length)
    {
        // Deliberately repetitive: RPC frames are highly self-similar, which is the case worth measuring
        var text = $"{{\"method\":\"ITestService.Compute\",\"args\":[{seed},\"abcdefghij\"],\"id\":";
        var result = new byte[length];
        for (var i = 0; i < length; i++)
            result[i] = (byte)text[i % text.Length];
        return result;
    }

    // Nested types

    protected sealed class CodecPair(ByteCompressor compressor, ByteDecompressor decompressor) : IDisposable
    {
        public ByteCompressor Compressor { get; } = compressor;
        public ByteDecompressor Decompressor { get; } = decompressor;
        public int LastCompressedSize { get; private set; }

        public void Dispose()
        {
            Compressor.Dispose();
            Decompressor.Dispose();
        }

        public byte[] RoundTrip(byte[] source, int maxLength = MaxLength)
            => Decompress(Compress(source), maxLength);

        public byte[] Compress(byte[] source)
        {
            using var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);
            Compressor.Compress(source, buffer);
            LastCompressedSize = buffer.WrittenCount;
            return buffer.ToArray();
        }

        public byte[] Decompress(byte[] source, int maxLength)
        {
            using var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);
            Decompressor.Decompress(source, buffer, maxLength);
            return buffer.ToArray();
        }
    }
}

public class LZ4ByteCompressorTest(ITestOutputHelper @out) : ByteCompressorTestBase(@out)
{
    protected override ByteCompressor NewCompressor()
        => new LZ4ByteCompressor();
    protected override ByteDecompressor NewDecompressor()
        => new LZ4ByteDecompressor();

    // A block size well below the frame sizes the base tests use, to exercise the multi-block
    // path far more often than the default one does
    [Fact]
    public void SmallBlockSizeTest()
    {
        using var pair = new CodecPair(new LZ4ByteCompressor(1024), new LZ4ByteDecompressor(1024));

        for (var i = 0; i < 50; i++) {
            var source = NewPayload(i, 1 + (i * 211 % 10_000));
            pair.RoundTrip(source).Should().Equal(source);
        }
    }

    // MaxBlockSize is exactly the largest block whose worst-case LZ4 output still fits the
    // ushort length prefix - one byte more would overflow it.
    [Fact]
    public void MaxBlockSizeIsTheLengthPrefixBoundTest()
    {
        var maxBlockSize = LZ4ByteCompressor.MaxBlockSize;
        Out.WriteLine($"MaxBlockSize = {maxBlockSize}, "
            + $"MaximumOutputSize = {LZ4Codec.MaximumOutputSize(maxBlockSize)}");

        maxBlockSize.Should().BeLessThanOrEqualTo(64 * 1024);
        LZ4Codec.MaximumOutputSize(maxBlockSize).Should().BeLessThanOrEqualTo(ushort.MaxValue);
        LZ4Codec.MaximumOutputSize(maxBlockSize + 1).Should().BeGreaterThan(ushort.MaxValue);
    }

    [Fact]
    public void BlockSizeValidationTest()
    {
        new LZ4ByteCompressor().BlockSize.Should().Be(LZ4ByteCompressor.MaxBlockSize);
        new LZ4ByteDecompressor().BlockSize.Should().Be(LZ4ByteCompressor.MaxBlockSize);
        new LZ4ByteCompressor(LZ4ByteCompressor.MaxBlockSize).BlockSize
            .Should().Be(LZ4ByteCompressor.MaxBlockSize);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LZ4ByteCompressor(LZ4ByteCompressor.MaxBlockSize + 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LZ4ByteDecompressor(LZ4ByteCompressor.MaxBlockSize + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LZ4ByteCompressor(-1));
    }

    // Incompressible data at exactly MaxBlockSize is the case the ushort prefix must survive
    [Fact]
    public void MaxBlockSizeIncompressibleRoundTripTest()
    {
        using var pair = NewPair();
        var random = new Random(7);
        for (var i = 0; i < 3; i++) {
            var source = new byte[LZ4ByteCompressor.MaxBlockSize];
            random.NextBytes(source);
            pair.RoundTrip(source).Should().Equal(source);
        }
    }
}
