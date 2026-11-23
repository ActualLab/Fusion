using System.Buffers;
using ActualLab.Compression;

namespace ActualLab.Tests.Compression;

/// <summary>
/// The contract every <see cref="ByteEncoder"/>/<see cref="ByteDecoder"/> pair must satisfy.
/// Derive one test class per codec; codec-specific cases go in the derived class.
/// </summary>
public abstract class ByteEncoderTestBase(ITestOutputHelper @out) : TestBase(@out)
{
    protected const int MaxLength = 1 << 22;

    protected abstract ByteEncoder NewEncoder();
    protected abstract ByteDecoder NewDecoder();

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

        // Every compressed frame must be fed to the decoder, in order - skipping one
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

        pair.Encoder.Reset();
        pair.Decoder.Reset();

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
            pair.Encoder.Reset();
            pair.Decoder.Reset();
        }
    }

    [Fact]
    public void DecompressionBombTest()
    {
        using var pair = NewPair();
        var source = new byte[100_000]; // All zeros - compresses to almost nothing
        var compressed = pair.Encode(source);
        compressed.Length.Should().BeLessThan(2000);

        Assert.Throws<FormatException>(() => pair.Decode(compressed, 10_000));
    }

    [Fact]
    public void MaxLengthBoundaryTest()
    {
        using var pair = NewPair();
        var source = NewPayload(0, 1000);
        var compressed = pair.Encode(source);

        // Exactly at the limit is fine
        pair.Decode(compressed, source.Length).Should().Equal(source);
    }

    [Fact]
    public void MaxLengthExceededByOneByteTest()
    {
        using var pair = NewPair();
        var source = NewPayload(0, 1000);
        var compressed = pair.Encode(source);

        Assert.Throws<FormatException>(() => pair.Decode(compressed, source.Length - 1));
    }

    // Whatever a codec does with a corrupted frame, it must never silently return the original
    [Fact]
    public void CorruptedFrameIsNeverSilentlyAcceptedTest()
    {
        using var pair = NewPair();
        var source = NewPayload(0, 4000);
        var compressed = pair.Encode(source);
        compressed[compressed.Length / 2] ^= 0xFF;

        DecodeOrNull(pair, compressed)?.Should().NotEqual(source);
    }

    // A frame cut mid-block must fail rather than decode partially or leak into the next frame.
    // (A cut exactly at a block boundary is undetectable, but a 4 KB frame is a single block.)
    [Fact]
    public void TruncatedFrameIsRejectedTest()
    {
        var source = NewPayload(0, 4000);
        byte[] compressed;
        using (var pair = NewPair())
            compressed = pair.Encode(source);

        for (var length = 1; length < compressed.Length; length += 1 + length / 8) {
            using var pair = NewPair();
            var truncated = compressed.AsSpan(0, length).ToArray();
            Assert.ThrowsAny<Exception>(() => pair.Decode(truncated, MaxLength));
        }
    }

    // A big spare buffer must not let a decoder produce much more than maxLength before it fails
    [Fact]
    public void MaxLengthIsEnforcedEarlyTest()
    {
        using var pair = NewPair();
        var source = new byte[1 << 20];
        var compressed = pair.Encode(source);
        var target = new BoundedWriter(1 << 21);

        Assert.Throws<FormatException>(() => pair.Decoder.Decode(compressed, target, 1000));
        target.WrittenCount.Should().BeLessThan(1 << 16);
    }

    // A decoder that lost the history the encoder still references must not silently produce the
    // right bytes. (An encoder-only reset is undetectable: a frame that references no history decodes fine.)
    [Fact]
    public void OneSidedResetIsNeverSilentlyAcceptedTest()
    {
        using var pair = NewPair();
        var source = NewPayload(0, 4000);

        pair.RoundTrip(source).Should().Equal(source);
        pair.Decoder.Reset(); // Encoder deliberately not reset

        var compressed = pair.Encode(source);
        DecodeOrNull(pair, compressed)?.Should().NotEqual(source);
    }

    [Fact]
    public void DoubleDisposeIsSafeTest()
    {
        var encoder = NewEncoder();
        var decoder = NewDecoder();

        encoder.Dispose();
        encoder.Dispose();
        decoder.Dispose();
        decoder.Dispose();
    }

    // Protected methods

    protected byte[]? DecodeOrNull(CodecPair pair, byte[] compressed)
    {
        try {
            return pair.Decode(compressed, MaxLength);
        }
        catch (Exception e) {
            Out.WriteLine($"Rejected with {e.GetType().Name}");
            return null;
        }
    }

    protected CodecPair NewPair()
        => new(NewEncoder(), NewDecoder());

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
    public void MaxEncodedLengthIsNeverExceededTest()
    {
        var random = new Random(11);
        foreach (var length in (int[])[1, 2, 100, 1000, 10_000, 100_000, 1_000_000]) {
            using var pair = NewPair();
            var source = new byte[length];
            random.NextBytes(source); // Random data is incompressible - the worst case
            var bound = pair.Encoder.GetMaxEncodedLength(length);

            var compressed = pair.Encode(source);
            Out.WriteLine($"{length} bytes -> {compressed.Length}, bound {bound}");
            compressed.Length.Should().BeLessThanOrEqualTo(bound);
            pair.Decode(compressed, MaxLength).Should().Equal(source);
        }
    }

    [Fact]
    public void MaxEncodedLengthIsMonotonicTest()
    {
        using var encoder = NewEncoder();
        encoder.GetMaxEncodedLength(0).Should().Be(0);

        var previous = 0;
        for (var length = 0; length <= 200_000; length += 997) {
            var bound = encoder.GetMaxEncodedLength(length);
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
        using var encoder = NewEncoder();
        foreach (var maxLength in (int[])[0, 1, 1000, 100_000, 16_711_680]) {
            var sourceLength = encoder.GetMaxSourceLength(maxLength);
            sourceLength.Should().BeLessThanOrEqualTo(maxLength);
            encoder.GetMaxEncodedLength(sourceLength).Should().BeLessThanOrEqualTo(maxLength);
            if (sourceLength < maxLength)
                encoder.GetMaxEncodedLength(sourceLength + 1).Should().BeGreaterThan(maxLength);
        }
    }

    // Always offers its whole spare capacity, as a pooled buffer may
    protected sealed class BoundedWriter(int capacity) : IBufferWriter<byte>
    {
        private readonly byte[] _buffer = new byte[capacity];

        public int WrittenCount { get; private set; }

        public void Advance(int count)
            => WrittenCount += count;

        public Memory<byte> GetMemory(int sizeHint = 0)
            => _buffer.AsMemory(WrittenCount);

        public Span<byte> GetSpan(int sizeHint = 0)
            => _buffer.AsSpan(WrittenCount);
    }

    protected sealed class CodecPair(ByteEncoder encoder, ByteDecoder decoder) : IDisposable
    {
        public ByteEncoder Encoder { get; } = encoder;
        public ByteDecoder Decoder { get; } = decoder;
        public int LastCompressedSize { get; private set; }

        public void Dispose()
        {
            Encoder.Dispose();
            Decoder.Dispose();
        }

        public byte[] RoundTrip(byte[] source, int maxLength = MaxLength)
            => Decode(Encode(source), maxLength);

        public byte[] Encode(byte[] source)
        {
            using var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);
            Encoder.Encode(source, buffer);
            LastCompressedSize = buffer.WrittenCount;
            return buffer.ToArray();
        }

        public byte[] Decode(byte[] source, int maxLength)
        {
            using var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);
            Decoder.Decode(source, buffer, maxLength);
            return buffer.ToArray();
        }
    }
}

public class LZ4ByteEncoderTest(ITestOutputHelper @out) : ByteEncoderTestBase(@out)
{
    protected override ByteEncoder NewEncoder()
        => new LZ4ByteEncoder();
    protected override ByteDecoder NewDecoder()
        => new LZ4ByteDecoder();

    // A block size well below the frame sizes the base tests use, to exercise the multi-block
    // path far more often than the default one does
    [Fact]
    public void SmallBlockSizeTest()
    {
        var codec = new LZ4ByteCodec { BlockSize = 1024 };
        using var pair = new CodecPair(codec.NewEncoder(), codec.NewDecoder());

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
        var maxBlockSize = LZ4ByteEncoder.MaxBlockSize;
        Out.WriteLine($"MaxBlockSize = {maxBlockSize}, "
            + $"MaximumOutputSize = {LZ4MaximumOutputSize(maxBlockSize)}");

        maxBlockSize.Should().BeLessThanOrEqualTo(64 * 1024);
        LZ4MaximumOutputSize(maxBlockSize).Should().BeLessThanOrEqualTo(ushort.MaxValue);
        // The bound is the largest block K4os will actually use, not the largest integer whose
        // output fits: it rounds a request up to a 1 KiB multiple, so the next candidate is one
        // step up - and that one overflows the prefix, which is why the limit sits here.
        (maxBlockSize % 1024).Should().Be(0);
        LZ4MaximumOutputSize(maxBlockSize + 1024).Should().BeGreaterThan(ushort.MaxValue);
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

        var codec = new LZ4ByteCodec { BlockSize = blockSize };
        using var pair = new CodecPair(codec.NewEncoder(), codec.NewDecoder());
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
        new LZ4ByteEncoder().BlockSize.Should().Be(LZ4ByteEncoder.DefaultBlockSize);
        new LZ4ByteDecoder().BlockSize.Should().Be(LZ4ByteEncoder.DefaultBlockSize);
        LZ4ByteEncoder.DefaultBlockSize.Should().BeLessThanOrEqualTo(LZ4ByteEncoder.MaxBlockSize);
        new LZ4ByteEncoder(new LZ4ByteCodec { BlockSize = LZ4ByteEncoder.MaxBlockSize }).BlockSize
            .Should().Be(LZ4ByteEncoder.MaxBlockSize);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LZ4ByteEncoder(new LZ4ByteCodec { BlockSize = LZ4ByteEncoder.MaxBlockSize + 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new LZ4ByteDecoder(new LZ4ByteCodec { BlockSize = LZ4ByteEncoder.MaxBlockSize + 1 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LZ4ByteCodec { BlockSize = -1 });
    }

    // Worst-case expansion is a property of the block geometry, not a constant: a small block
    // pays [ushort length] + LZ4's own per-block overhead far more often. A fixed per-frame
    // allowance sized for the default geometry is nowhere near enough for a custom one.
    [Fact]
    public void MaxEncodedLengthTracksBlockSizeTest()
    {
        const int frameSize = 16_711_680; // RpcFrameBasedTransport.DefaultMaxFrameSize
        const int fixedAllowance = 4 + 64 + (frameSize >> 6); // What a codec-agnostic estimate gave

        using var small = new LZ4ByteEncoder(new LZ4ByteCodec { BlockSize = 256 });
        using var large = new LZ4ByteEncoder();
        var smallOverhead = small.GetMaxEncodedLength(frameSize) - frameSize;
        var largeOverhead = large.GetMaxEncodedLength(frameSize) - frameSize;
        Out.WriteLine($"overhead: 256-byte blocks {smallOverhead}, "
            + $"{LZ4ByteEncoder.MaxBlockSize}-byte blocks {largeOverhead}, fixed {fixedAllowance}");

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
        using var encoder = new LZ4ByteEncoder(new LZ4ByteCodec { BlockSize = requested });
        using var decoder = new LZ4ByteDecoder(new LZ4ByteCodec { BlockSize = requested });

        encoder.BlockSize.Should().Be(decoder.BlockSize);
        encoder.BlockSize.Should().BeGreaterThanOrEqualTo(requested);
        LZ4MaximumOutputSize(encoder.BlockSize).Should().BeLessThanOrEqualTo(ushort.MaxValue);
    }

    // Incompressible data at exactly MaxBlockSize is the case the ushort prefix must survive
    [Fact]
    public void MaxBlockSizeIncompressibleRoundTripTest()
    {
        // Explicitly at MaxBlockSize, so this stays the prefix's worst case whatever the default is
        using var pair = new CodecPair(
            new LZ4ByteEncoder(new LZ4ByteCodec { BlockSize = LZ4ByteEncoder.MaxBlockSize }),
            new LZ4ByteDecoder(new LZ4ByteCodec { BlockSize = LZ4ByteEncoder.MaxBlockSize }));
        var random = new Random(7);
        for (var i = 0; i < 3; i++) {
            var source = new byte[LZ4ByteEncoder.MaxBlockSize];
            random.NextBytes(source);
            pair.RoundTrip(source).Should().Equal(source);
        }
    }

    // LZ4's worst-case output size for a block (LZ4_compressBound)
    private static int LZ4MaximumOutputSize(int length)
        => length + length / 255 + 16;
}
