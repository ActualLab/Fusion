using ActualLab.Compression;

namespace ActualLab.Tests.Compression;

// Runs the whole encoder contract with 64K hash slots
public class LZ4HL16ByteEncoderTest(ITestOutputHelper @out) : ByteEncoderTestBase(@out)
{
    protected override ByteEncoder NewEncoder()
        => LZ4ByteCodec.HL16.NewEncoder();
    protected override ByteDecoder NewDecoder()
        => LZ4ByteCodec.HL16.NewDecoder();

    [Fact]
    public void PresetShouldBeSharedAndDescribed()
    {
        LZ4ByteCodec.HL16.Should().BeSameAs(LZ4ByteCodec.HL16);
        LZ4ByteCodec.HL16.HashLog.Should().Be(16);
        LZ4ByteCodec.HL16.ToString().Should().Be("LZ4(16K, HL16)");
        (LZ4ByteCodec.HL16 with { DictionaryData = new byte[100] }).ToString().Should().Be("LZ4(16K, HL16+dict)");
        LZ4ByteCodec.HL16.DefaultName.Should().Be("lz4", "the hash table is local to the encoder");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(14)]
    [InlineData(16)]
    public void AnyHashLogShouldBeDecodableByDefaultDecoder(int hashLog)
    {
        // arrange
        var codec = LZ4ByteCodec.Default with { HashLog = hashLog };
        using var pair = new CodecPair(codec.NewEncoder(), LZ4ByteCodec.Default.NewDecoder());

        // act + assert
        for (var i = 0; i < 100; i++) {
            var source = LZ4DictionaryTest.NewMessages(i, 1 + i * 377 % 20_000);
            pair.RoundTrip(source).Should().Equal(source);
        }
    }

    [Fact]
    public void MoreSlotsShouldCompressBetter()
    {
        // arrange
        var frames = Enumerable.Range(0, 200).Select(i => LZ4DictionaryTest.NewMessages(i, 4000)).ToList();
        using var small = new CodecPair(LZ4ByteCodec.Default.NewEncoder(), LZ4ByteCodec.Default.NewDecoder());
        using var large = new CodecPair(LZ4ByteCodec.HL16.NewEncoder(), LZ4ByteCodec.HL16.NewDecoder());

        // act
        var smallSize = frames.Sum(x => small.Encode(x).Length);
        var largeSize = frames.Sum(x => large.Encode(x).Length);

        // assert
        Out.WriteLine($"4K slots: {smallSize} bytes, 64K slots: {largeSize} bytes");
        largeSize.Should().BeLessThan(smallSize, "more slots find more matches");
    }

    // Proves the setting reaches the match finder: each table size finds different matches
    [Fact]
    public void HashLogShouldChangeEncoderOutput()
    {
        // arrange
        var source = LZ4DictionaryTest.NewMessages(7, 16_000);

        // act
        var outputs = Enumerable.Range(LZ4ByteCodec.MinHashLog, LZ4ByteCodec.MaxHashLog - LZ4ByteCodec.MinHashLog + 1)
            .Select(hashLog => {
                using var encoder = (LZ4ByteCodec.Default with { HashLog = hashLog }).NewEncoder();
                using var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);
                encoder.Encode(source, buffer);
                return Convert.ToBase64String(buffer.WrittenSpan.ToArray());
            })
            .ToList();

        // assert
        outputs[^1].Should().NotBe(outputs[LZ4ByteCodec.DefaultHashLog - LZ4ByteCodec.MinHashLog]);
        outputs[LZ4ByteCodec.DefaultHashLog - LZ4ByteCodec.MinHashLog].Should().Be(DefaultOutput(source));
    }

    private static string DefaultOutput(byte[] source)
    {
        using var encoder = LZ4ByteCodec.Default.NewEncoder();
        using var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);
        encoder.Encode(source, buffer);
        return Convert.ToBase64String(buffer.WrittenSpan.ToArray());
    }

    [Fact]
    public void HashLogShouldBeValidated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LZ4ByteCodec.Default with { HashLog = 9 });
        Assert.Throws<ArgumentOutOfRangeException>(() => LZ4ByteCodec.Default with { HashLog = 17 });
    }
}

// Same, with a dictionary prepared for 64K slots
public class LZ4HL16DictionaryTest(ITestOutputHelper @out) : ByteEncoderTestBase(@out)
{
    private static readonly LZ4ByteCodec Shared = LZ4ByteCodec.HL16 with {
        DictionaryData = LZ4DictionaryTest.NewMessages(1, 64 * 1024),
    };

    protected override ByteEncoder NewEncoder()
        => Shared.NewEncoder();
    protected override ByteDecoder NewDecoder()
        => Shared.NewDecoder();

    // A copy with another HashLog must prepare its own dictionary, even if the original's is ready
    [Fact]
    public void CopyWithOtherHashLogShouldPrepareItsOwnDictionary()
    {
        // arrange
        using (Shared.NewEncoder()) { } // Prepares the original's dictionary
        var copy = Shared with { HashLog = 12 };
        var source = LZ4DictionaryTest.NewMessages(5, 3000);

        // act
        using var pair = new CodecPair(copy.NewEncoder(), Shared.NewDecoder());

        // assert
        pair.RoundTrip(source).Should().Equal(source);
    }

    [Fact]
    public void DictionaryShouldWorkWithDefaultSlotDecoder()
    {
        // arrange
        var decoderCodec = LZ4ByteCodec.Default with { DictionaryData = Shared.DictionaryData };
        using var pair = new CodecPair(Shared.NewEncoder(), decoderCodec.NewDecoder());

        // act + assert
        for (var i = 0; i < 50; i++) {
            var source = LZ4DictionaryTest.NewMessages(10 + i, 300 + i * 97);
            pair.RoundTrip(source).Should().Equal(source);
        }
    }
}
