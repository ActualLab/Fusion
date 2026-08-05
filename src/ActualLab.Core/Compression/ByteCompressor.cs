using System.Buffers;

namespace ActualLab.Compression;

/// <summary>
/// Compresses a stream of frames: every <see cref="Compress"/> call ends at a flush point, but
/// the dictionary the earlier calls built is retained until <see cref="Reset"/>.
/// </summary>
/// <remarks>
/// An instance belongs to one stream and one direction, and isn't thread-safe. Its output is
/// decodable only by a <see cref="ByteDecompressor"/> fed the same frames in the same order.
/// </remarks>
public abstract class ByteCompressor : IDisposable
{
    public abstract void Compress(ReadOnlySpan<byte> source, IBufferWriter<byte> target);
    public abstract void Reset();

    public virtual void Dispose()
    { }
}
