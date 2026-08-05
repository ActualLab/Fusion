using ActualLab.Compression;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc.Compression;

/// <summary>
/// Compresses a connection's outbound frames and applies the reset policy.
/// Exists only when the serialization format compresses the outbound direction.
/// </summary>
/// <remarks>
/// A frame is <c>[int32 length|flags][body]</c> - the two flags ride in the header word's top
/// bits rather than in a byte of their own, so a stored frame needs no buffer and no copy: its
/// header is written straight into the length prefix the transport already reserved.
/// Only the transport's writer loop calls this, so it isn't thread-safe.
/// </remarks>
public sealed class RpcFrameEncoder(
    ByteCompressor compressor,
    RpcCompressionOptions settings,
    int bufferSize,
    int maxBufferSize
    ) : IDisposable
{
    private const int Int32Size = sizeof(int);

    private readonly ArrayPoolBuffer<byte> _buffer = new(ArrayPools.SharedBytePool, bufferSize, mustClear: false);
    private long _bytesSinceReset;
    private int _framesSinceReset;

    public RpcCompressionOptions Settings { get; } = settings;

    public void Dispose()
    {
        compressor.Dispose();
        _buffer.Dispose();
    }

    // frame is [int32 length placeholder][payload]; the result is [int32 length|flags][body]
    public ReadOnlyMemory<byte> Encode(Memory<byte> frame)
    {
        var payload = frame.Span[Int32Size..];
        // Stored vs. compressed has to be decided before the codec sees the bytes: feeding them in
        // and then sending the frame stored would advance our dictionary but not the peer's.
        var mustCompress = payload.Length >= Settings.MinCompressedFrameSize;
        var flags = 0;
        if (mustCompress && (_framesSinceReset >= Settings.ContextResetFrames
            || _bytesSinceReset >= Settings.ContextResetBytes)) {
            compressor.Reset();
            _bytesSinceReset = 0;
            _framesSinceReset = 0;
            flags = RpcFrameCodec.ContextResetFrameFlag;
        }

        if (!mustCompress) {
            // Zero-copy: the payload stays where the serializer wrote it
            frame.Span.WriteLittleEndian(payload.Length | flags);
            return frame;
        }

        _buffer.Renew(maxBufferSize);
        _buffer.GetSpan(Int32Size);
        _buffer.Advance(Int32Size);
        compressor.Compress(payload, _buffer);
        _bytesSinceReset += payload.Length;
        _framesSinceReset++;

        var length = _buffer.WrittenCount - Int32Size;
        _buffer.Array.AsSpan().WriteLittleEndian(length | flags | RpcFrameCodec.CompressedFrameFlag);
        return _buffer.WritableWrittenMemory;
    }

    // A compressed frame can be slightly larger than its input, so the sender caps what it feeds
    // in by this much - which keeps every encoded frame within the limit the peer enforces.
    public static int GetMaxOverhead(int maxFrameSize)
        => Int32Size + 64 + (maxFrameSize >> 6);
}
