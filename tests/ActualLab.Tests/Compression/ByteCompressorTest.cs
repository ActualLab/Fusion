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

    // The bound the frame layer budgets on: an underestimate here means an oversized frame and a
    // dropped connection, so incompressible input - where the codec can only expand - must fit it.
    [Fact]
    public void MaxCompressedLengthIsNeverExceededTest()
    {
        var random = new Random(11);
        foreach (var length in (int[])[1, 2, 100, 1000, 10_000, 100_000, 1_000_000]) {
            using var pair = NewPair();
            var source = new byte[length];
            random.NextBytes(source); // Random data is incompressible - the worst case
            var bound = pair.Compressor.GetMaxCompressedLength(length);

            var compressed = pair.Compress(source);
            Out.WriteLine($"{length} bytes -> {compressed.Length}, bound {bound}");
            compressed.Length.Should().BeLessThanOrEqualTo(bound);
            pair.Decompress(compressed, MaxLength).Should().Equal(source);
        }
    }

    [Fact]
    public void MaxCompressedLengthIsMonotonicTest()
    {
        using var compressor = NewCompressor();
        compressor.GetMaxCompressedLength(0).Should().Be(0);

        var previous = 0;
        for (var length = 0; length <= 200_000; length += 997) {
            var bound = compressor.GetMaxCompressedLength(length);
            bound.Should().BeGreaterThanOrEqualTo(previous);
            bound.Should().BeGreaterThanOrEqualTo(length); // Compression is never guaranteed
            previous = bound;
        }
    }

    // GetMaxSourceLength is the inverse the frame layer actually calls: its result must fit, and
    // one byte more must not - otherwise the budget is either unsafe or needlessly small.
    [Fact]
    public void MaxSourceLengthIsExactTest()
    {
        using var compressor = NewCompressor();
        foreach (var maxLength in (int[])[0, 1, 1000, 100_000, 16_711_680]) {
            var sourceLength = compressor.GetMaxSourceLength(maxLength);
            sourceLength.Should().BeLessThanOrEqualTo(maxLength);
            compressor.GetMaxCompressedLength(sourceLength).Should().BeLessThanOrEqualTo(maxLength);
            if (sourceLength < maxLength)
                compressor.GetMaxCompressedLength(sourceLength + 1).Should().BeGreaterThan(maxLength);
        }
    }

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
        // The bound is the largest block K4os will actually use, not the largest integer whose
        // output fits: it rounds a request up to a 1 KiB multiple, so the next candidate is one
        // step up - and that one overflows the prefix, which is why the limit sits here.
        (maxBlockSize % 1024).Should().Be(0);
        LZ4Codec.MaximumOutputSize(maxBlockSize + 1024).Should().BeGreaterThan(ushort.MaxValue);
    }

    // Block size doesn't cap the match window - LZ4's own 64 KiB one spans blocks. That's what
    // makes block size a free parameter: shrinking it costs flush points, not dictionary reach.
    [Fact]
    public void MatchWindowSpansBlocksTest()
    {
        const int blockSize = 1024;
        var random = new Random(5);
        var source = new byte[40_000]; // 40x the block size, and incompressible on its own
        random.NextBytes(source);

        using var pair = new CodecPair(new LZ4ByteCompressor(blockSize), new LZ4ByteDecompressor(blockSize));
        pair.RoundTrip(source).Should().Equal(source);
        var firstSize = pair.LastCompressedSize;
        pair.RoundTrip(source).Should().Equal(source);
        var repeatSize = pair.LastCompressedSize;

        Out.WriteLine($"first={firstSize} repeat={repeatSize}");
        firstSize.Should().BeGreaterThan(source.Length); // Random data can only expand
        // The repeat can only compress if matches reach ~40 KB back, far past one block
        repeatSize.Should().BeLessThan(source.Length / 20);
    }

    [Fact]
    public void BlockSizeValidationTest()
    {
        new LZ4ByteCompressor().BlockSize.Should().Be(LZ4ByteCompressor.DefaultBlockSize);
        new LZ4ByteDecompressor().BlockSize.Should().Be(LZ4ByteCompressor.DefaultBlockSize);
        LZ4ByteCompressor.DefaultBlockSize.Should().BeLessThanOrEqualTo(LZ4ByteCompressor.MaxBlockSize);
        new LZ4ByteCompressor(LZ4ByteCompressor.MaxBlockSize).BlockSize
            .Should().Be(LZ4ByteCompressor.MaxBlockSize);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LZ4ByteCompressor(LZ4ByteCompressor.MaxBlockSize + 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LZ4ByteDecompressor(LZ4ByteCompressor.MaxBlockSize + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LZ4ByteCompressor(-1));
    }

    // Worst-case expansion is a property of the block geometry, not a constant: a small block
    // pays [ushort length] + LZ4's own per-block overhead far more often. A fixed per-frame
    // allowance sized for the default geometry is nowhere near enough for a custom one.
    [Fact]
    public void MaxCompressedLengthTracksBlockSizeTest()
    {
        const int frameSize = 16_711_680; // RpcFrameBasedTransport.DefaultMaxFrameSize
        const int fixedAllowance = 4 + 64 + (frameSize >> 6); // What a codec-agnostic estimate gave

        using var small = new LZ4ByteCompressor(256);
        using var large = new LZ4ByteCompressor();
        var smallOverhead = small.GetMaxCompressedLength(frameSize) - frameSize;
        var largeOverhead = large.GetMaxCompressedLength(frameSize) - frameSize;
        Out.WriteLine($"overhead: 256-byte blocks {smallOverhead}, "
            + $"{LZ4ByteCompressor.MaxBlockSize}-byte blocks {largeOverhead}, fixed {fixedAllowance}");

        largeOverhead.Should().BeLessThan(fixedAllowance); // The default geometry it was tuned for
        smallOverhead.Should().BeGreaterThan(fixedAllowance); // ...and the one it silently broke
        small.GetMaxSourceLength(frameSize).Should().BeLessThan(large.GetMaxSourceLength(frameSize));
    }

    // BlockSize must report what the encoder actually fills, not what it was asked for: K4os
    // rounds a request up to a power of two, and a bound sized for the request would then be too
    // small - a full block of incompressible data would encode past the ushort length prefix.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(256)]
    [InlineData(1024)]
    [InlineData(5000)]
    public void BlockSizeIsTheEffectiveOneTest(int requested)
    {
        using var compressor = new LZ4ByteCompressor(requested);
        using var decompressor = new LZ4ByteDecompressor(requested);

        compressor.BlockSize.Should().Be(decompressor.BlockSize);
        compressor.BlockSize.Should().BeGreaterThanOrEqualTo(requested);
        LZ4Codec.MaximumOutputSize(compressor.BlockSize).Should().BeLessThanOrEqualTo(ushort.MaxValue);
    }

    // Incompressible data at exactly MaxBlockSize is the case the ushort prefix must survive
    [Fact]
    public void MaxBlockSizeIncompressibleRoundTripTest()
    {
        // Explicitly at MaxBlockSize, so this stays the prefix's worst case whatever the default is
        using var pair = new CodecPair(
            new LZ4ByteCompressor(LZ4ByteCompressor.MaxBlockSize),
            new LZ4ByteDecompressor(LZ4ByteCompressor.MaxBlockSize));
        var random = new Random(7);
        for (var i = 0; i < 3; i++) {
            var source = new byte[LZ4ByteCompressor.MaxBlockSize];
            random.NextBytes(source);
            pair.RoundTrip(source).Should().Equal(source);
        }
    }
}
