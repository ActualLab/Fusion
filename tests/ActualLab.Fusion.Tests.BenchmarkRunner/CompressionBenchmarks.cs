using System.Text;
using ActualLab.Compression;
using BenchmarkDotNet.Attributes;

namespace ActualLab.Fusion.Tests.BenchmarkRunner;

// Compresses and decompresses a stream of RPC-like frames the way an RPC connection does: one codec context
// per stream, a flush point per frame, a reset per stream. Dictionaries are synthetic samples of the same
// kind of traffic; the size and ratio of each case are printed by GlobalSetup ("Ratio:" lines).
[MemoryDiagnoser]
public class CompressionBenchmarks
{
    private const int FrameCount = 256;

    private static readonly string[] Methods = [
        "ITodoService.Get", "ITodoService.List", "IAuth.GetUser", "IChatService.PostMessage",
        "$sys.Ok", "$sys.Invalidate", "$sys-c.Ok",
    ];

    private byte[][] _frames = null!;
    private byte[][] _compressedFrames = null!;
    private ByteEncoder _encoder = null!;
    private ByteDecoder _decoder = null!;
    private ArrayPoolBuffer<byte> _buffer = null!;

    public static IEnumerable<string> Codecs => [
        "LZ4", "LZ4+Dict",
#if NET11_0_OR_GREATER
        "Zstd1", "Zstd1+Dict", "Zstd-5", "Zstd-5+Dict", "Zstd3",
#endif
    ];

    [ParamsSource(nameof(Codecs))]
    public string Codec { get; set; } = "";

    [Params(300, 4000)]
    public int FrameSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var codec = CreateCodec(Codec);
        _frames = Enumerable.Range(0, FrameCount).Select(i => NewMessages(1000 + i, FrameSize)).ToArray();
        _encoder = codec.NewEncoder();
        _decoder = codec.NewDecoder();
        _buffer = new ArrayPoolBuffer<byte>(1024 * 1024, mustClear: false);

        _compressedFrames = new byte[FrameCount][];
        for (var i = 0; i < FrameCount; i++) {
            _buffer.Reset();
            _encoder.Encode(_frames[i], _buffer);
            _compressedFrames[i] = _buffer.WrittenSpan.ToArray();
        }
        var sourceSize = _frames.Sum(x => x.Length);
        var compressedSize = _compressedFrames.Sum(x => x.Length);
        var firstFramesSize = _compressedFrames.Take(4).Sum(x => x.Length);
        Console.WriteLine($"Ratio: {Codec}, FrameSize={FrameSize}: {sourceSize} -> {compressedSize} bytes "
            + $"({(double)sourceSize / compressedSize:F2}x), "
            + $"first 4 frames: {4 * FrameSize} -> {firstFramesSize} bytes");
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _encoder.Dispose();
        _decoder.Dispose();
        _buffer.Dispose();
    }

    [Benchmark(OperationsPerInvoke = FrameCount)]
    public int Encode()
    {
        _encoder.Reset();
        var total = 0;
        foreach (var frame in _frames) {
            _buffer.Reset();
            _encoder.Encode(frame, _buffer);
            total += _buffer.WrittenCount;
        }
        return total;
    }

    [Benchmark(OperationsPerInvoke = FrameCount)]
    public int Decode()
    {
        _decoder.Reset();
        var total = 0;
        foreach (var frame in _compressedFrames) {
            _buffer.Reset();
            _decoder.Decode(frame, _buffer, int.MaxValue);
            total += _buffer.WrittenCount;
        }
        return total;
    }

    [Benchmark]
    public void ResetEncoder()
        => _encoder.Reset();

    [Benchmark]
    public void ResetDecoder()
        => _decoder.Reset();

    // Private methods

    private static ByteCodec CreateCodec(string name)
    {
        var parts = name.Split('+');
        ByteCodec codec = parts[0] switch {
            "LZ4" => LZ4ByteCodec.Default,
#if NET11_0_OR_GREATER
            "Zstd1" => ZstdByteCodec.Default with { Quality = 1 },
            "Zstd-5" => ZstdByteCodec.Default with { Quality = -5 },
            "Zstd3" => ZstdByteCodec.Default with { Quality = 3 },
#endif
            _ => throw new ArgumentOutOfRangeException(nameof(name)),
        };
        return parts.Length > 1 ? codec with { DictionaryData = NewMessages(1, 64 * 1024) } : codec;
    }

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
