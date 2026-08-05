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
    // The largest output Compress can produce for a source this long. It must never underestimate!
    public abstract int GetMaxCompressedLength(int sourceLength);
    public abstract void Compress(ReadOnlySpan<byte> source, IBufferWriter<byte> target);
    public abstract void Reset();

    public virtual void Dispose()
    { }

    // The inverse: the longest source whose worst-case output still fits maxLength.
    public int GetMaxSourceLength(int maxLength)
    {
        // Compression can't be guaranteed on arbitrary input, so the answer never exceeds maxLength
        var min = 0;
        var max = Math.Max(0, maxLength);
        while (min < max) {
            var mid = (int)(((long)min + max + 1) >> 1);
            if (GetMaxCompressedLength(mid) <= maxLength)
                min = mid;
            else
                max = mid - 1;
        }
        return min;
    }
}
