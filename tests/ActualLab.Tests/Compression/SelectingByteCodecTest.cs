using ActualLab.Compression;

namespace ActualLab.Tests.Compression;

// Runs the whole encoder contract with frames split between two differently configured LZ4 codecs
public class SelectingByteCodecTest(ITestOutputHelper @out) : ByteEncoderTestBase(@out)
{
    private static readonly LZ4ByteCodec SmallFrameCodec = LZ4ByteCodec.Default with {
        BlockSize = 4 * 1024,
        DictionaryData = LZ4DictionaryTest.NewMessages(3, 8 * 1024),
    };
    private static readonly SelectingByteCodec Codec = new(
        static frame => frame.Length < 1024 ? 0 : 1,
        SmallFrameCodec, LZ4ByteCodec.Default);

    protected override ByteEncoder NewEncoder()
        => Codec.NewEncoder();
    protected override ByteDecoder NewDecoder()
        => Codec.NewDecoder();

    [Fact]
    public void FrameShouldStartWithCodecIndex()
    {
        // arrange
        using var pair = NewPair();
        var small = LZ4DictionaryTest.NewMessages(1, 300);
        var large = LZ4DictionaryTest.NewMessages(2, 5000);

        // act
        var encodedSmall = pair.Encode(small);
        var encodedLarge = pair.Encode(large);

        // assert
        encodedSmall[0].Should().Be(0);
        encodedLarge[0].Should().Be(1);
        pair.Decode(encodedSmall, MaxLength).Should().Equal(small);
        pair.Decode(encodedLarge, MaxLength).Should().Equal(large);
    }

    [Fact]
    public void CodecShouldEncodeFrameAfterIndexByte()
    {
        // arrange
        using var pair = NewPair();
        using var lz4 = SmallFrameCodec.NewEncoder();
        var source = LZ4DictionaryTest.NewMessages(4, 500);

        // act
        var encoded = pair.Encode(source);
        using var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);
        lz4.Encode(source, buffer);

        // assert
        encoded.AsSpan(1).ToArray().Should().Equal(buffer.ToArray());
    }

    [Fact]
    public void EachCodecShouldKeepItsOwnContext()
    {
        // arrange
        using var pair = NewPair();
        var rnd = new Random(5);

        // act + assert
        for (var i = 0; i < 300; i++) {
            var source = LZ4DictionaryTest.NewMessages(i, rnd.Next(1, 3000));
            pair.RoundTrip(source).Should().Equal(source);
            if (i % 97 == 0) {
                pair.Encoder.Reset();
                pair.Decoder.Reset();
            }
        }
    }

    [Fact]
    public void ResetShouldResetEveryCodec()
    {
        // arrange
        using var pair = NewPair();
        var small = LZ4DictionaryTest.NewMessages(6, 400);
        var large = LZ4DictionaryTest.NewMessages(7, 4000);
        var freshSmall = pair.Encode(small);
        var freshLarge = pair.Encode(large);
        pair.Decode(freshSmall, MaxLength);
        pair.Decode(freshLarge, MaxLength);

        // act
        pair.Encoder.Reset();
        pair.Decoder.Reset();

        // assert
        pair.Encode(small).Should().Equal(freshSmall);
        pair.Encode(large).Should().Equal(freshLarge);
        pair.Decode(freshSmall, MaxLength).Should().Equal(small);
        pair.Decode(freshLarge, MaxLength).Should().Equal(large);
    }

    [Fact]
    public void InvalidIndexShouldFail()
    {
        // arrange
        var codec = new SelectingByteCodec(static _ => 2, LZ4ByteCodec.Default, LZ4ByteCodec.Default);
        using var encoder = codec.NewEncoder();
        using var decoder = codec.NewDecoder();
        using var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);

        // act + assert
        Assert.Throws<InvalidOperationException>(() => encoder.Encode(new byte[10], buffer));
        Assert.Throws<FormatException>(() => decoder.Decode(new byte[] { 2, 0, 0 }, buffer, MaxLength));
    }

    [Fact]
    public void CodecsShouldBeValidated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SelectingByteCodec(static _ => 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SelectingByteCodec(static _ => 0, Enumerable.Repeat(LZ4ByteCodec.Default, 257).ToArray()));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SelectingByteCodec(static _ => 0, null!, LZ4ByteCodec.Default));
    }

    [Fact]
    public void CodecShouldBeImmutableRecord()
    {
        // arrange
        var copy = Codec with { };

        // assert
        Codec.ToString().Should().Be("Selecting(LZ4(4K+dict), LZ4(16K))");
        Codec.DefaultName.Should().BeNull();
        copy.Should().NotBeSameAs(Codec);
        copy.Equals(Codec).Should().BeFalse();
        copy.Codecs.Should().Equal(Codec.Codecs);
    }
}
