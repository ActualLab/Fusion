using ActualLab.Compression;
using ActualLab.Rpc.Serialization;

namespace ActualLab.Rpc.Compression;

/// <summary>
/// Decodes the frames <see cref="RpcFrameEncoder"/> produces on the peer's side.
/// Exists only when the serialization format compresses the inbound direction.
/// </summary>
/// <remarks>
/// Only the transport's reader loop calls this, so it isn't thread-safe.
/// </remarks>
public sealed class RpcFrameDecoder(
    ByteDecompressor decompressor,
    int bufferSize,
    int maxBufferSize
    ) : IDisposable
{
    private readonly ArrayPoolBuffer<byte> _buffer = new(ArrayPools.SharedBytePool, bufferSize, mustClear: false);

    public void Dispose()
    {
        decompressor.Dispose();
        _buffer.Dispose();
    }

    // header is the frame's [flags|length] word - over WebSocket only its most significant byte
    // is transmitted, so the length bits may be absent there; only the flags are read.
    public (byte[] Array, int Offset, int End) Decode(
        int header, byte[] array, int offset, int end, int maxLength)
    {
        if ((header & RpcFrameCodec.ContextResetFrameFlag) != 0)
            decompressor.Reset();
        if ((header & RpcFrameCodec.CompressedFrameFlag) == 0 || offset >= end)
            return (array, offset, end);

        _buffer.Renew(maxBufferSize);
        decompressor.Decompress(array.AsMemory(offset, end - offset), _buffer, maxLength);
        return (_buffer.Array, 0, _buffer.WrittenCount);
    }
}
