using System.Buffers;

namespace ActualLab.Compression;

/// <summary>
/// The decoding counterpart of <see cref="ByteCompressor"/>: consumes exactly one flush point per
/// <see cref="Decompress"/> call, in the order the compressor produced them.
/// </summary>
public abstract class ByteDecompressor : IDisposable
{
    // maxLength bounds the output of a single call - it's what makes a decompression bomb
    // fail fast instead of expanding into memory.
    public abstract void Decompress(ReadOnlyMemory<byte> source, IBufferWriter<byte> target, int maxLength);
    public abstract void Reset();

    public virtual void Dispose()
    { }
}
