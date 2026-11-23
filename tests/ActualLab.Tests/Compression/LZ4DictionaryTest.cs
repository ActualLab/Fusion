using System.Diagnostics;
using System.Text;
using ActualLab.Compression;
using ActualLab.Rpc;
using ActualLab.Rpc.Compression;

namespace ActualLab.Tests.Compression;

// Every base test runs against one dictionary shared by all of them, so it also checks
// that no compressor or decompressor changes it - xUnit runs these tests in parallel.
// Compression without a dictionary is covered by LZ4ByteCompressorTest.
public class LZ4DictionaryTest(ITestOutputHelper @out) : ByteCompressorTestBase(@out)
{
    private static readonly string[] Methods = [
        "ITodoService.Get", "ITodoService.List", "IAuth.GetUser", "IChatService.PostMessage",
        "$sys.Ok", "$sys.Invalidate", "$sys-c.Ok",
    ];
    private static readonly LZ4ByteCompression Compression = LZ4ByteCompression.Default;
    private static readonly byte[] SharedDictionaryData = NewMessages(1, 64 * 1024);
    private static readonly object SharedDictionary = Compression.CreateDictionary(SharedDictionaryData);

    protected override ByteCompressor NewCompressor()
        => Compression.NewCompressor(SharedDictionary);
    protected override ByteDecompressor NewDecompressor()
        => Compression.NewDecompressor(SharedDictionary);

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
        var dictionary = Compression.CreateDictionary(NewMessages(1, length));
        using var pair = NewPair(dictionary);
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
        using var plain = new CodecPair(new LZ4ByteCompressor(), new LZ4ByteDecompressor());
        using var primed = NewPair(SharedDictionary);
        var frames = Enumerable.Range(0, 5).Select(i => NewMessages(10 + i, 300)).ToList();

        // act
        var plainSize = frames.Sum(x => plain.Compress(x).Length);
        var primedSize = frames.Sum(x => primed.Compress(x).Length);

        // assert
        Out.WriteLine($"Plain: {plainSize} bytes, with dictionary: {primedSize} bytes");
        primedSize.Should().BeLessThan(plainSize * 3 / 4);
    }

    [Fact]
    public void DictionaryShouldApplyAfterEveryReset()
    {
        // arrange
        using var pair = NewPair(SharedDictionary);
        var history = NewMessages(2, 200_000);
        var frame = NewMessages(3, 2000);
        var expected = NewPair(SharedDictionary).Compress(frame);

        // act
        pair.RoundTrip(history);
        pair.Compressor.Reset();
        pair.Decompressor.Reset();
        var compressed = pair.Compress(frame);

        // assert
        compressed.Should().Equal(expected, "after Reset a compressor must start from the dictionary state");
        pair.Decompress(compressed, MaxLength).Should().Equal(frame);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    [InlineData(64 * 1024)]
    [InlineData(100_000)]
    public void SavedDictionaryShouldLoadBack(int length)
    {
        // arrange
        var data = NewMessages(9, length);
        var dictionary = Compression.CreateDictionary(data);
        var frames = Enumerable.Range(0, 20).Select(i => NewMessages(40 + i, 1000)).ToList();

        // act
        var saved = Compression.SaveDictionary(dictionary);
        var loaded = Compression.LoadDictionary(saved);

        // assert
        // LZ4 can't reference anything beyond 64 KiB back, so that's all a dictionary keeps
        saved.Should().Equal(data.Skip(Math.Max(0, length - 64 * 1024)));
        var expected = Compress(Compression.NewCompressor(dictionary), frames);
        Compress(Compression.NewCompressor(loaded), frames)
            .Should().BeEquivalentTo(expected, o => o.WithStrictOrdering());
    }

    [Fact]
    public void ForeignDictionaryShouldBeRejected()
    {
        // arrange
        using var compressor = Compression.NewCompressor(SharedDictionary);

        // act + assert
        Assert.Throws<ArgumentOutOfRangeException>(() => Compression.NewCompressor("not a dictionary"));
        Assert.Throws<ArgumentOutOfRangeException>(() => compressor.Reset(new object()));
        compressor.Dictionary.Should().BeSameAs(SharedDictionary);
    }

    [Fact]
    public void ResetShouldSwitchDictionary()
    {
        // arrange
        var other = Compression.CreateDictionary(NewMessages(5, 32 * 1024));
        using var pair = new CodecPair(new LZ4ByteCompressor(), new LZ4ByteDecompressor());
        var frame = NewMessages(3, 2000);
        var expected = NewPair(other).Compress(frame);

        // act
        pair.RoundTrip(NewMessages(2, 50_000));
        pair.Compressor.Reset(other);
        pair.Decompressor.Reset(other);
        var compressed = pair.Compress(frame);

        // assert
        compressed.Should().Equal(expected);
        pair.Decompress(compressed, MaxLength).Should().Equal(frame);
        pair.Compressor.Dictionary.Should().BeSameAs(other);

        pair.Compressor.Reset(null);
        pair.Decompressor.Reset(null);
        pair.Compressor.Dictionary.Should().BeNull();
        pair.RoundTrip(frame).Should().Equal(frame);
    }

    [Fact]
    public void DecompressorShouldNeedTheSameDictionary()
    {
        // arrange
        using var compressor = Compression.NewCompressor(SharedDictionary);
        var frame = NewMessages(4, 2000);
        using var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);
        compressor.Compress(frame, buffer);
        var compressed = buffer.ToArray();

        // act
        byte[]? withoutDictionary = null;
        try {
            withoutDictionary = Decompress(new LZ4ByteDecompressor(), compressed);
        }
        catch (FormatException) { }
        var otherDictionary = Compression.CreateDictionary(NewMessages(5, 64 * 1024));
        byte[]? withOtherDictionary = null;
        try {
            withOtherDictionary = Decompress(Compression.NewDecompressor(otherDictionary), compressed);
        }
        catch (FormatException) { }

        // assert
        Decompress(Compression.NewDecompressor(SharedDictionary), compressed).Should().Equal(frame);
        (withoutDictionary ?? []).Should().NotEqual(frame);
        (withOtherDictionary ?? []).Should().NotEqual(frame);
    }

    [Fact]
    public async Task SharedDictionaryShouldNeverChange()
    {
        // arrange
        var frames = Enumerable.Range(0, 50).Select(i => NewMessages(20 + i, 1500)).ToList();
        var expected = Compress(Compression.NewCompressor(SharedDictionary), frames);

        // act
        await Task.WhenAll(Enumerable.Range(0, 32).Select(i => Task.Run(() => {
            using var pair = NewPair(SharedDictionary);
            for (var pass = 0; pass < 3; pass++) {
                for (var j = 0; j < 100; j++) {
                    var source = NewMessages(1000 * i + j, 1 + j * 131 % 5000);
                    pair.RoundTrip(source).Should().Equal(source);
                }
                pair.Compressor.Reset();
                pair.Decompressor.Reset();
            }
        })));

        // assert
        Compression.SaveDictionary(SharedDictionary).Should().Equal(SharedDictionaryData);
        Compress(Compression.NewCompressor(SharedDictionary), frames)
            .Should().BeEquivalentTo(expected, o => o.WithStrictOrdering(), "the dictionary state must stay intact");
    }

    [Fact]
    public void ResetShouldNotReprocessDictionary()
    {
        // arrange
        var data = NewMessages(6, 64 * 1024);
        using var compressor = Compression.NewCompressor(SharedDictionary);
        const int iterations = 2000;

        // act
        var resetTime = MeasureBest(() => {
            for (var i = 0; i < iterations; i++)
                compressor.Reset();
        });
        var prepareTime = MeasureBest(() => {
            for (var i = 0; i < iterations; i++)
                compressor.Reset(Compression.CreateDictionary(data));
        });
        compressor.Reset(SharedDictionary);

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
        using var plain = new LZ4ByteCompressor();
        using var primed = Compression.NewCompressor(SharedDictionary);
        using var buffer = new ArrayPoolBuffer<byte>(1024 * 1024, mustClear: false);

        // act
        var plainTime = MeasureBest(() => CompressAll(plain));
        var primedTime = MeasureBest(() => CompressAll(primed));

        // assert
        Out.WriteLine(
            $"Plain: {plainTime.TotalMilliseconds:F3}ms, with dictionary: {primedTime.TotalMilliseconds:F3}ms");
        primedTime.Ticks.Should().BeLessThan(plainTime.Ticks * 2);

        void CompressAll(ByteCompressor compressor)
        {
            compressor.Reset();
            buffer.Reset();
            foreach (var frame in frames)
                compressor.Compress(frame, buffer);
        }
    }

    [Fact]
    public void CompressedVariantsShouldUseLiteralSuffix()
    {
        // arrange
        var formats = new RpcWireFormatConfiguration();
        var lz4d = RpcCompressionFormat.New("lz4d", Compression, SharedDictionary);

        // act
        formats.AddCompressedVariants("lz4d", lz4d);
        formats.AddCompressedVariants("lz4df", lz4d, RpcCompressionMode.Full);
        formats.AddLZ4Variants("lz4");
        var format = formats["msgpack6c-lz4d"];
        using var compressor = format.CompressionFormat!.CompressorFactory.Invoke();
        using var decompressor = format.CompressionFormat.DecompressorFactory.Invoke();

        // assert
        formats["msgpack6c-lz4df"].CompressionMode.Should().Be(RpcCompressionMode.Full);
        formats["msgpack6c-lz4"].CompressionFormat.Should().BeSameAs(RpcCompressionFormat.LZ4);
        compressor.Dictionary.Should().BeSameAs(SharedDictionary);
        decompressor.Dictionary.Should().BeSameAs(SharedDictionary);
        RpcDefaultWireFormats.MessagePackV6C.WithLZ4("lz4f", compressionMode: RpcCompressionMode.Full).Key
            .Should().Be("msgpack6c-lz4f");
    }

    [Fact]
    public void LZ4VariantsShouldTakeSuffixAndDictionary()
    {
        // arrange
        var formats = new RpcWireFormatConfiguration();

        // act
        formats.AddLZ4Variants("lz4d1", SharedDictionary);
        formats.AddLZ4Variants("lz4d1f", SharedDictionary, RpcCompressionMode.Full);
        formats.AddLZ4Variants("lz4x", filter: x => x.Key == "msgpack6c");
        var single = RpcDefaultWireFormats.MessagePackV6.WithLZ4("lz4d2", SharedDictionary, RpcCompressionMode.Full);
        using var compressor = formats["msgpack6c-lz4d1"].CompressionFormat!.CompressorFactory.Invoke();

        // assert
        formats["msgpack6c-lz4d1"].CompressionMode.Should().Be(RpcCompressionMode.ServerToClient);
        formats["msgpack6c-lz4d1f"].CompressionMode.Should().Be(RpcCompressionMode.Full);
        compressor.Dictionary.Should().BeSameAs(SharedDictionary);
        formats.ContainsKey("msgpack6c-lz4x").Should().BeTrue();
        formats.ContainsKey("msgpack6-lz4x").Should().BeFalse();
        formats["msgpack6c-lz4x"].CompressionFormat!.CompressorFactory.Invoke().Dictionary.Should().BeNull();
        single.Key.Should().Be("msgpack6-lz4d2");
        single.CompressionMode.Should().Be(RpcCompressionMode.Full);
        RpcDefaultWireFormats.MessagePackV6.WithLZ4("lz4").CompressionFormat
            .Should().BeSameAs(RpcCompressionFormat.LZ4);
        Assert.Throws<ArgumentOutOfRangeException>(() => formats.AddLZ4Variants("lz4", SharedDictionary));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => formats.AddLZ4Variants("lz4f", SharedDictionary, RpcCompressionMode.Full));
    }

    // Private methods

    private static CodecPair NewPair(object dictionary)
        => new(Compression.NewCompressor(dictionary), Compression.NewDecompressor(dictionary));

    private static byte[] Decompress(ByteDecompressor decompressor, byte[] compressed)
    {
        using var _ = decompressor;
        using var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);
        decompressor.Decompress(compressed, buffer, MaxLength);
        return buffer.ToArray();
    }

    private static List<byte[]> Compress(ByteCompressor compressor, List<byte[]> frames)
    {
        using var _ = compressor;
        var result = new List<byte[]>();
        foreach (var frame in frames) {
            using var buffer = new ArrayPoolBuffer<byte>(256, mustClear: false);
            compressor.Compress(frame, buffer);
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
    private static byte[] NewMessages(int seed, int length)
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
