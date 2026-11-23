using System.Diagnostics;
using System.Text;
using ActualLab.Compression;
using ActualLab.Rpc;
using ActualLab.Rpc.Compression;

namespace ActualLab.Tests.Compression;

// Every base test runs against one dictionary shared by all of them, so it also checks
// that no encoder or decoder changes it - xUnit runs these tests in parallel.
// Compression without a dictionary is covered by LZ4ByteEncoderTest.
public class LZ4DictionaryTest(ITestOutputHelper @out) : ByteEncoderTestBase(@out)
{
    private static readonly string[] Methods = [
        "ITodoService.Get", "ITodoService.List", "IAuth.GetUser", "IChatService.PostMessage",
        "$sys.Ok", "$sys.Invalidate", "$sys-c.Ok",
    ];
    private static readonly byte[] SharedDictionaryData = NewMessages(1, 64 * 1024);
    private static readonly ByteCodec Shared
        = LZ4ByteCodec.Default with { DictionaryData = SharedDictionaryData };

    protected override ByteEncoder NewEncoder()
        => Shared.NewEncoder();
    protected override ByteDecoder NewDecoder()
        => Shared.NewDecoder();

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(100)]
    [InlineData(16 * 1024)]
    [InlineData(64 * 1024)]
    [InlineData(100_000)]
    public void DictionaryOfAnySizeShouldRoundTrip(int length)
    {
        // arrange
        var codec = LZ4ByteCodec.Default with { DictionaryData = NewMessages(1, length) };
        using var pair = NewPair(codec);
        var frames = Enumerable.Range(0, 200).Select(i => NewMessages(100 + i, 1 + i * 97 % 3000)).ToList();

        // act
        var results = frames.Select(x => pair.RoundTrip(x)).ToList();

        // assert
        results.Should().BeEquivalentTo(frames, o => o.WithStrictOrdering());
    }

    [Fact]
    public void DictionaryShouldShrinkFirstFrames()
    {
        // arrange
        using var plain = NewPair(LZ4ByteCodec.Default);
        using var primed = NewPair(Shared);
        var frames = Enumerable.Range(0, 5).Select(i => NewMessages(10 + i, 300)).ToList();

        // act
        var plainSize = frames.Sum(x => plain.Encode(x).Length);
        var primedSize = frames.Sum(x => primed.Encode(x).Length);

        // assert
        Out.WriteLine($"Plain: {plainSize} bytes, with dictionary: {primedSize} bytes");
        primedSize.Should().BeLessThan(plainSize * 3 / 4);
    }

    [Fact]
    public void ResetShouldRestoreDictionaryState()
    {
        // arrange
        using var pair = NewPair(Shared);
        var history = NewMessages(2, 200_000);
        var frame = NewMessages(3, 2000);
        var expected = NewPair(Shared).Encode(frame);

        // act
        pair.RoundTrip(history);
        pair.Encoder.Reset();
        pair.Decoder.Reset();
        var compressed = pair.Encode(frame);

        // assert
        compressed.Should().Equal(expected, "after Reset a encoder must start from the dictionary state");
        pair.Decode(compressed, MaxLength).Should().Equal(frame);
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(64 * 1024)]
    [InlineData(100_000)]
    public void OnlyDictionaryTailShouldMatter(int length)
    {
        // arrange
        var data = NewMessages(9, length);
        var codec = LZ4ByteCodec.Default with { DictionaryData = data };
        var tail = LZ4ByteCodec.Default with { DictionaryData = data.AsMemory(Math.Max(0, length - 64 * 1024)) };
        var frames = Enumerable.Range(0, 20).Select(i => NewMessages(40 + i, 1000)).ToList();

        // act
        var expected = Encode(tail.NewEncoder(), frames);
        var actual = Encode(codec.NewEncoder(), frames);

        // assert
        // LZ4 can't reference anything beyond 64 KiB back, so only that much of a dictionary is used
        actual.Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
        codec.DictionaryData.ToArray().Should().Equal(data);
    }

    [Fact]
    public void CodecShouldBeImmutableRecord()
    {
        // act
        var custom = LZ4ByteCodec.Default with { ExtraBlocks = 8 };
        var smallBlocks = LZ4ByteCodec.Default with { BlockSize = 1024 };
        var withDictionary = custom with { DictionaryData = SharedDictionaryData };

        // assert
        LZ4ByteCodec.Default.ExtraBlocks.Should().Be(LZ4ByteCodec.DefaultExtraBlocks);
        custom.ExtraBlocks.Should().Be(8);
        custom.HasDictionary.Should().BeFalse();
        withDictionary.ExtraBlocks.Should().Be(8);
        withDictionary.HasDictionary.Should().BeTrue();
        // Peers must match the block size and the dictionary, but not the buffer size
        custom.DefaultName.Should().Be("lz4");
        smallBlocks.DefaultName.Should().BeNull();
        withDictionary.DefaultName.Should().BeNull();
        new LZ4ByteCodec().Should().NotBe(LZ4ByteCodec.Default, "they use reference equality");
        LZ4ByteCodec.Default.ToString().Should().Be("LZ4(16K)");
        withDictionary.ToString().Should().Be("LZ4(16K+dict)");
        Assert.Throws<ArgumentOutOfRangeException>(() => LZ4ByteCodec.Default with { ExtraBlocks = -1 });
    }

    [Fact]
    public void DecoderShouldNeedTheSameDictionary()
    {
        // arrange
        using var encoder = Shared.NewEncoder();
        var frame = NewMessages(4, 2000);
        using var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);
        encoder.Encode(frame, buffer);
        var compressed = buffer.ToArray();

        // act
        byte[]? withoutDictionary = null;
        try {
            withoutDictionary = Decode(LZ4ByteCodec.Default.NewDecoder(), compressed);
        }
        catch (FormatException) { }
        var other = LZ4ByteCodec.Default with { DictionaryData = NewMessages(5, 64 * 1024) };
        byte[]? withOtherDictionary = null;
        try {
            withOtherDictionary = Decode(other.NewDecoder(), compressed);
        }
        catch (FormatException) { }

        // assert
        Decode(Shared.NewDecoder(), compressed).Should().Equal(frame);
        (withoutDictionary ?? []).Should().NotEqual(frame);
        (withOtherDictionary ?? []).Should().NotEqual(frame);
    }

    [Fact]
    public async Task SharedDictionaryShouldNeverChange()
    {
        // arrange
        var frames = Enumerable.Range(0, 50).Select(i => NewMessages(20 + i, 1500)).ToList();
        var expected = Encode(Shared.NewEncoder(), frames);

        // act
        await Task.WhenAll(Enumerable.Range(0, 32).Select(i => Task.Run(() => {
            using var pair = NewPair(Shared);
            for (var pass = 0; pass < 3; pass++) {
                for (var j = 0; j < 100; j++) {
                    var source = NewMessages(1000 * i + j, 1 + j * 131 % 5000);
                    pair.RoundTrip(source).Should().Equal(source);
                }
                pair.Encoder.Reset();
                pair.Decoder.Reset();
            }
        })));

        // assert
        Shared.DictionaryData.ToArray().Should().Equal(SharedDictionaryData);
        Encode(Shared.NewEncoder(), frames)
            .Should().BeEquivalentTo(expected, o => o.WithStrictOrdering(), "the dictionary state must stay intact");
    }

    [Fact]
    public void ResetShouldNotReprocessDictionary()
    {
        // arrange
        var data = NewMessages(6, 64 * 1024);
        using var encoder = Shared.NewEncoder();
        const int iterations = 2000;

        // act
        var resetTime = MeasureBest(() => {
            for (var i = 0; i < iterations; i++)
                encoder.Reset();
        });
        var prepareTime = MeasureBest(() => {
            for (var i = 0; i < iterations; i++)
                (LZ4ByteCodec.Default with { DictionaryData = data }).NewEncoder().Dispose(); // Prepares it
        });

        // assert
        Out.WriteLine($"Reset: {resetTime.TotalMilliseconds * 1000 / iterations:F2}us, "
            + $"preparing a dictionary: {prepareTime.TotalMilliseconds * 1000 / iterations:F2}us");
        resetTime.Ticks.Should().BeLessThan(prepareTime.Ticks / 2);
    }

    [Fact]
    public void DictionaryShouldNotSlowDownCompression()
    {
        // arrange
        var frames = Enumerable.Range(0, 2000).Select(i => NewMessages(30 + i, 300)).ToList();
        using var plain = LZ4ByteCodec.Default.NewEncoder();
        using var primed = Shared.NewEncoder();
        using var buffer = new ArrayPoolBuffer<byte>(1024 * 1024, mustClear: false);

        // act
        var plainTime = MeasureBest(() => CompressAll(plain));
        var primedTime = MeasureBest(() => CompressAll(primed));

        // assert
        Out.WriteLine(
            $"Plain: {plainTime.TotalMilliseconds:F3}ms, with dictionary: {primedTime.TotalMilliseconds:F3}ms");
        primedTime.Ticks.Should().BeLessThan(plainTime.Ticks * 2);

        void CompressAll(ByteEncoder encoder)
        {
            encoder.Reset();
            buffer.Reset();
            foreach (var frame in frames)
                encoder.Encode(frame, buffer);
        }
    }

    [Fact]
    public void CompressedVariantsShouldTakeCodec()
    {
        // arrange
        var formats = new RpcWireFormatConfiguration();
        var bigBuffers = LZ4ByteCodec.Default with { ExtraBlocks = 8 };

        // act
        formats.AddCompressedVariants("lz4d1", Shared);
        formats.AddCompressedVariants("lz4d1f", Shared, RpcCompressionMode.Full);
        formats.AddCompressedVariants("lz4x", LZ4ByteCodec.Default, filter: x => x.Key == "msgpack6c");
        formats.AddCompressedVariants("lzy", x => x.Key switch {
            "msgpack6" => Shared,
            "msgpack6c" => bigBuffers,
            _ => null,
        });
        formats.AddCompressedVariants("lz4", bigBuffers, filter: x => x.Key == "msgpack6");
        var single = RpcDefaultWireFormats.MessagePackV6.WithCompression("lz4d2", Shared, RpcCompressionMode.Full);

        // assert
        formats["msgpack6c-lz4d1"].CompressionMode.Should().Be(RpcCompressionMode.ServerToClient);
        formats["msgpack6c-lz4d1f"].CompressionMode.Should().Be(RpcCompressionMode.Full);
        formats["msgpack6c-lz4d1f"].CompressionFormat!.Codec.Should().BeSameAs(Shared);
        formats["msgpack6c-lz4x"].CompressionFormat.Should().BeSameAs(RpcCompressionFormat.LZ4);
        formats.ContainsKey("msgpack6-lz4x").Should().BeFalse();
        formats["msgpack6-lzy"].CompressionFormat!.Codec.Should().BeSameAs(Shared);
        formats["msgpack6c-lzy"].CompressionFormat!.Codec.Should().BeSameAs(bigBuffers);
        formats.ContainsKey("msgpack5-lzy").Should().BeFalse();
        formats["msgpack6-lz4"].CompressionFormat!.Codec.Should().BeSameAs(bigBuffers);
        RpcCompressionFormat.LZ4.ToString().Should().Be("RpcCompressionFormat(LZ4(16K), min 512, reset 128K/512)");
        new RpcCompressionFormat(Shared, new RpcCompressionOptions { MinCompressedFrameSize = 100 })
            .ToString().Should().Be("RpcCompressionFormat(LZ4(16K+dict), min 100, reset 128K/512)");
        single.Key.Should().Be("msgpack6-lz4d2");
        single.CompressionMode.Should().Be(RpcCompressionMode.Full);
        using var encoder = (LZ4ByteEncoder)formats["msgpack6c-lz4d1"].CompressionFormat!.Codec
            .NewEncoder();
        encoder.Codec.Should().BeSameAs(Shared);
    }

    [Fact]
    public void ReservedSuffixesShouldMeanDefaultCompression()
    {
        // arrange
        var formats = new RpcWireFormatConfiguration();
        var msgpack6 = RpcDefaultWireFormats.MessagePackV6;

        // act + assert
        Assert.Throws<ArgumentOutOfRangeException>(() => formats.AddCompressedVariants("lz4", Shared));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => formats.AddCompressedVariants("lz4", LZ4ByteCodec.Default, RpcCompressionMode.Full));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => formats.AddCompressedVariants("lz4f", LZ4ByteCodec.Default));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => formats.AddCompressedVariants("lz4", LZ4ByteCodec.Default with { BlockSize = 1024 }));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => msgpack6.WithCompression("lz4f", Shared, RpcCompressionMode.Full));
        Assert.Throws<ArgumentOutOfRangeException>(() => msgpack6.WithCompression("", LZ4ByteCodec.Default));
        formats.Keys.Should().NotContain(x => x.Contains("-lz4", StringComparison.Ordinal));
        msgpack6.WithCompression("lz4", LZ4ByteCodec.Default).CompressionFormat
            .Should().BeSameAs(RpcCompressionFormat.LZ4);
    }

    // Private methods

    private static CodecPair NewPair(ByteCodec codec)
        => new(codec.NewEncoder(), codec.NewDecoder());

    private static byte[] Decode(ByteDecoder decoder, byte[] compressed)
    {
        using var _ = decoder;
        using var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);
        decoder.Decode(compressed, buffer, MaxLength);
        return buffer.ToArray();
    }

    private static List<byte[]> Encode(ByteEncoder encoder, List<byte[]> frames)
    {
        using var _ = encoder;
        var result = new List<byte[]>();
        foreach (var frame in frames) {
            using var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);
            encoder.Encode(frame, buffer);
            result.Add(buffer.ToArray());
        }
        return result;
    }

    private static TimeSpan MeasureBest(Action action)
    {
        action.Invoke();
        var best = TimeSpan.MaxValue;
        for (var i = 0; i < 5; i++) {
            var stopwatch = Stopwatch.StartNew();
            action.Invoke();
            if (stopwatch.Elapsed < best)
                best = stopwatch.Elapsed;
        }
        return best;
    }

    // RPC-like messages: a few method names, small numbers, short strings - unlike NewPayload's
    // single repeated message, the first frames of these compress poorly without a dictionary
    internal static byte[] NewMessages(int seed, int length)
    {
        var random = new Random(seed);
        var sb = new StringBuilder();
        while (sb.Length < length)
            sb.Append("{\"Method\":\"").Append(Methods[random.Next(Methods.Length)])
                .Append("\",\"CallId\":").Append(random.Next(1, 100_000))
                .Append(",\"Headers\":{\"@version\":\"").Append(random.Next(1, 1000))
                .Append("\"},\"Args\":[\"session-").Append(random.Next(1000))
                .Append("\",").Append(random.Next())
                .Append(",\"Title ").Append(random.Next(100)).Append("\",")
                .Append(random.Next(2) == 0 ? "true" : "false")
                .Append("]}");
        return Encoding.UTF8.GetBytes(sb.ToString(0, length));
    }
}
