#if NET11_0_OR_GREATER
using ActualLab.Compression;
using ActualLab.Rpc;
using ActualLab.Rpc.Compression;

namespace ActualLab.Tests.Compression;

public class ZstdByteEncoderTest(ITestOutputHelper @out) : ByteEncoderTestBase(@out)
{
    protected override ByteEncoder NewEncoder()
        => new ZstdByteEncoder();
    protected override ByteDecoder NewDecoder()
        => new ZstdByteDecoder();

    [Theory]
    [InlineData(-5)]
    [InlineData(1)]
    [InlineData(3)]
    public void AnyQualityShouldRoundTrip(int quality)
    {
        // arrange
        var codec = ZstdByteCodec.Default with { Quality = quality };
        using var pair = new CodecPair(codec.NewEncoder(), codec.NewDecoder());

        // act + assert
        for (var i = 0; i < 50; i++) {
            var source = LZ4DictionaryTest.NewMessages(i, 1 + i * 211 % 10_000);
            pair.RoundTrip(source).Should().Equal(source);
        }
    }

    [Fact]
    public void ZstdVariantsShouldTakeCompression()
    {
        // arrange
        var formats = new RpcWireFormatConfiguration();
        var withDictionary = ZstdByteCodec.Default with {
            DictionaryData = LZ4DictionaryTest.NewMessages(1, 16 * 1024),
        };

        // act
        formats.AddCompressedVariants("zstd", ZstdByteCodec.Default);
        formats.AddCompressedVariants("zstdf", ZstdByteCodec.Default, RpcCompressionMode.Full);
        formats.AddCompressedVariants("zstdd1", withDictionary);

        // assert
        formats["msgpack6c-zstd"].CompressionFormat.Should().BeSameAs(RpcCompressionFormat.Zstd);
        formats["msgpack6c-zstdf"].CompressionMode.Should().Be(RpcCompressionMode.Full);
        formats["msgpack6c-zstdd1"].CompressionFormat!.Codec.Should().BeSameAs(withDictionary);
        Assert.Throws<ArgumentOutOfRangeException>(() => formats.AddCompressedVariants("zstd", withDictionary));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => formats.AddCompressedVariants("zstd", LZ4ByteCodec.Default));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => formats.AddCompressedVariants("zstd", ZstdByteCodec.Default with { Quality = 3 }));
        Assert.Throws<InvalidOperationException>(
            () => formats.AddCompressedVariants("zstd", ZstdByteCodec.Default));
    }
}

// Every base test runs against one dictionary shared by all of them
public class ZstdDictionaryTest(ITestOutputHelper @out) : ByteEncoderTestBase(@out)
{
    private static readonly byte[] SharedDictionaryData = LZ4DictionaryTest.NewMessages(1, 64 * 1024);
    private static readonly ByteCodec Shared
        = ZstdByteCodec.Default with { DictionaryData = SharedDictionaryData };

    protected override ByteEncoder NewEncoder()
        => Shared.NewEncoder();
    protected override ByteDecoder NewDecoder()
        => Shared.NewDecoder();

    [Fact]
    public void DictionaryShouldShrinkFirstFrames()
    {
        // arrange
        using var plain = NewPair(ZstdByteCodec.Default);
        using var primed = NewPair(Shared);
        var frames = Enumerable.Range(0, 5).Select(i => LZ4DictionaryTest.NewMessages(10 + i, 300)).ToList();

        // act
        var plainSize = frames.Sum(x => plain.Encode(x).Length);
        var primedSize = frames.Sum(x => primed.Encode(x).Length);

        // assert
        Out.WriteLine($"Plain: {plainSize} bytes, with dictionary: {primedSize} bytes");
        primedSize.Should().BeLessThan(plainSize * 9 / 10);
    }

    [Fact]
    public void ResetShouldRestoreDictionaryState()
    {
        // arrange
        using var pair = NewPair(Shared);
        var frame = LZ4DictionaryTest.NewMessages(3, 2000);
        var expected = NewPair(Shared).Encode(frame);

        // act
        pair.RoundTrip(LZ4DictionaryTest.NewMessages(2, 50_000));
        pair.Encoder.Reset();
        pair.Decoder.Reset();
        var compressed = pair.Encode(frame);

        // assert
        compressed.Should().Equal(expected);
        pair.Decode(compressed, MaxLength).Should().Equal(frame);
    }

    [Fact]
    public void CopyShouldPrepareItsOwnDictionary()
    {
        // arrange
        var shared = (ZstdByteCodec)Shared;
        var frame = LZ4DictionaryTest.NewMessages(4, 1000);

        // act
        var copy = shared with { Quality = 3 };
        var sameDataCopy = ZstdByteCodec.Default with { DictionaryData = SharedDictionaryData };

        // assert
        shared.Dictionary.Should().NotBeNull();
        copy.Dictionary.Should().NotBeNull().And.NotBeSameAs(shared.Dictionary, "a copy may have other settings");
        copy.DictionaryData.ToArray().Should().Equal(SharedDictionaryData);
        sameDataCopy.Should().NotBe(shared, "ByteCodec records use reference equality");
        shared.ToString().Should().Be("Zstd(q1+dict)");
        copy.ToString().Should().Be("Zstd(q3+dict)");
        new CodecPair(sameDataCopy.NewEncoder(), Shared.NewDecoder()).RoundTrip(frame).Should().Equal(frame);
    }

    // Private methods

    private static CodecPair NewPair(ByteCodec codec)
        => new(codec.NewEncoder(), codec.NewDecoder());
}
#endif
