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

    // The encoder's window is set explicitly, so even the slowest levels stay decodable
    [Theory]
    [InlineData(19, 16)]
    [InlineData(22, 16)]
    public void HighQualityShouldRoundTrip(int quality, int windowLog2)
    {
        // arrange
        var codec = ZstdByteCodec.Default with { Quality = quality, WindowLog2 = windowLog2 };
        using var pair = new CodecPair(codec.NewEncoder(), codec.NewDecoder());

        // act + assert
        for (var i = 0; i < 10; i++) {
            var source = LZ4DictionaryTest.NewMessages(i, 100_000);
            pair.RoundTrip(source).Should().Equal(source);
        }
    }

    [Fact]
    public void SettingsShouldBeValidated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ZstdByteCodec.Default with { Quality = 1000 });
        Assert.Throws<ArgumentOutOfRangeException>(() => ZstdByteCodec.Default with { WindowLog2 = 5 });
        Assert.Throws<ArgumentOutOfRangeException>(() => ZstdByteCodec.Default with { MaxWindowLog2 = 40 });

        var tooWide = ZstdByteCodec.Default with { WindowLog2 = 24 };
        Assert.Throws<ArgumentOutOfRangeException>(() => tooWide.NewEncoder());
        Assert.Throws<ArgumentOutOfRangeException>(() => tooWide.NewDecoder());
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RpcWireFormatConfiguration().AddCompressedVariants("zstdw", tooWide));
        (tooWide with { MaxWindowLog2 = 24 }).NewDecoder().Dispose();
    }

    [Fact]
    public void WiderPeerWindowShouldBeRejected()
    {
        // arrange
        var wide = ZstdByteCodec.Default with { WindowLog2 = 22 };
        var narrow = ZstdByteCodec.Default with { MaxWindowLog2 = 20 };
        using var pair = new CodecPair(wide.NewEncoder(), narrow.NewDecoder());
        var source = LZ4DictionaryTest.NewMessages(0, 10_000); // The window is in the frame header

        // act + assert
        Assert.Throws<FormatException>(() => pair.RoundTrip(source));
    }

    // The documented use: LZ4 for small frames, Zstandard for large ones
    [Fact]
    public void SelectingCodecShouldMixLZ4AndZstd()
    {
        // arrange
        var codec = new SelectingByteCodec(
            static frame => frame.Length < 2048 ? 0 : 1,
            LZ4ByteCodec.Default,
            ZstdByteCodec.Default with { DictionaryData = LZ4DictionaryTest.NewMessages(9, 32_768) });
        using var pair = new CodecPair(codec.NewEncoder(), codec.NewDecoder());
        var rnd = new Random(1);

        // act + assert
        for (var i = 0; i < 300; i++) {
            var source = LZ4DictionaryTest.NewMessages(i, rnd.Next(1, 10_000));
            var encoded = pair.Encode(source);
            encoded[0].Should().Be(source.Length < 2048 ? (byte)0 : (byte)1);
            pair.Decode(encoded, MaxLength).Should().Equal(source);
            if (i % 101 == 100) {
                pair.Encoder.Reset();
                pair.Decoder.Reset();
            }
        }
        codec.ToString().Should().Be("Selecting(LZ4(16K), Zstd(Q1, WL17+dict))");
    }

    [Fact]
    public void PresetsShouldBeSharedAndWork()
    {
        // arrange
        var presets = new[] { SelectingByteCodec.LZ4HL16_And_Zstd_At1K, SelectingByteCodec.LZ4HL16_And_ZstdQ3_At1K };
        var rnd = new Random(2);

        // assert
        ZstdByteCodec.Q3.Should().BeSameAs(ZstdByteCodec.Q3);
        ZstdByteCodec.Q3.Quality.Should().Be(3);
        SelectingByteCodec.LZ4HL16_And_Zstd_At1K.Should().BeSameAs(SelectingByteCodec.LZ4HL16_And_Zstd_At1K);
        SelectingByteCodec.LZ4HL16_And_Zstd_At1K.ToString()
            .Should().Be("Selecting(LZ4(16K, HL16), Zstd(Q1, WL17))");
        SelectingByteCodec.LZ4HL16_And_ZstdQ3_At1K.ToString()
            .Should().Be("Selecting(LZ4(16K, HL16), Zstd(Q3, WL17))");
        foreach (var length in (int[])[1023, 1024, 1025]) {
            using var encoder = SelectingByteCodec.LZ4HL16_And_Zstd_At1K.NewEncoder();
            using var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);
            encoder.Encode(LZ4DictionaryTest.NewMessages(length, length), buffer);
            buffer.WrittenSpan[0].Should().Be(length < 1024 ? (byte)0 : (byte)1);
        }
        foreach (var codec in presets) {
            using var pair = new CodecPair(codec.NewEncoder(), codec.NewDecoder());
            for (var i = 0; i < 100; i++) {
                var source = LZ4DictionaryTest.NewMessages(i, rnd.Next(1, 5000));
                var encoded = pair.Encode(source);
                encoded[0].Should().Be(source.Length < 1024 ? (byte)0 : (byte)1);
                pair.Decode(encoded, MaxLength).Should().Equal(source);
            }
        }
    }

    [Fact]
    public void ZstdVariantsShouldTakeCodec()
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
        using var fresh = NewPair(Shared);
        var expected = fresh.Encode(frame);

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
        using (var copyPair = NewPair(copy))
            copyPair.RoundTrip(frame).Should().Equal(frame);
        copy.DictionaryData.ToArray().Should().Equal(SharedDictionaryData);
        sameDataCopy.Should().NotBe(shared, "ByteCodec records use reference equality");
        shared.ToString().Should().Be("Zstd(Q1, WL17+dict)");
        copy.ToString().Should().Be("Zstd(Q3, WL17+dict)");
        new CodecPair(sameDataCopy.NewEncoder(), Shared.NewDecoder()).RoundTrip(frame).Should().Equal(frame);
    }

    // Private methods

    private static CodecPair NewPair(ByteCodec codec)
        => new(codec.NewEncoder(), codec.NewDecoder());
}
#endif
