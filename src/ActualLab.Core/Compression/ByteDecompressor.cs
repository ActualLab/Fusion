using System.Buffers;

namespace ActualLab.Compression;

/// <summary>
/// The decoding counterpart of <see cref="ByteCompressor"/>: consumes exactly one flush point per
/// <see cref="Decompress"/> call, in the order the compressor produced them.
/// </summary>
public abstract class ByteDecompressor : IDisposable
{
    // The dictionary the context starts from (null = none): an opaque object made by the codec's
    // ByteCompression. It must be the one the peer compressor uses.
    public object? Dictionary { get; protected set; }

    // maxLength bounds the output of a single call - it's what makes a decompression bomb
    // fail fast instead of expanding into memory.
    public abstract void Decompress(ReadOnlyMemory<byte> source, IBufferWriter<byte> target, int maxLength);

    public void Reset()
        => Reset(Dictionary);

    // Starts over from the given dictionary, which subsequent Reset() calls use too
    public void Reset(object? dictionary)
    {
        ResetCore(dictionary);
        Dictionary = dictionary;
    }

    public virtual void Dispose()
    { }

    // Protected methods

    protected abstract void ResetCore(object? dictionary);
}
