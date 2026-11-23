using System.Buffers;

namespace ActualLab.Compression;

/// <summary>
/// The decoding counterpart of <see cref="ByteEncoder"/>: consumes exactly one flush point per
/// <see cref="Decode"/> call, in the order the encoder produced them.
/// </summary>
public abstract class ByteDecoder : IDisposable
{
    // maxLength bounds the output of a single call - it's what makes a decompression bomb
    // fail fast instead of expanding into memory.
    public abstract void Decode(ReadOnlyMemory<byte> source, IBufferWriter<byte> target, int maxLength);

    // Starts over from the initial state: the dictionary of the ByteCodec it came from, if any
    public abstract void Reset();

    public virtual void Dispose()
    { }
}
