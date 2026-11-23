#if NET11_0_OR_GREATER
using System.Buffers;
using System.IO.Compression;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Compression;

/// <summary>
/// A <see cref="ByteEncoder"/> backed by .NET's Zstandard (.NET 11+): all frames are parts of a single
/// zstd frame, and each <see cref="Encode"/> call ends with a flush, so the context carries across frames.
/// </summary>
public sealed class ZstdByteEncoder : ByteEncoder
{
    // A flush ends a block, and the stream's frame header comes with its first block
    private const int FlushOverhead = 32;

    private readonly ZstandardEncoder _encoder;

    public ZstdByteCodec Codec { get; }

    // The codec must match the peer decoder's one (dictionary)
    public ZstdByteEncoder(ZstdByteCodec? codec = null)
    {
        Codec = codec ??= ZstdByteCodec.Default;
        codec.ValidateWindows();
        _encoder = new ZstandardEncoder(new ZstandardCompressionOptions {
            Quality = codec.Quality,
            WindowLog2 = codec.WindowLog2,
            Dictionary = codec.Dictionary,
        });
    }

    public override void Dispose()
        => _encoder.Dispose();

    public override void Encode(ReadOnlySpan<byte> source, IBufferWriter<byte> target)
    {
        if (source.IsEmpty)
            return;

        var span = target.GetSpan(GetMaxEncodedLength(source.Length));
        var status = _encoder.Compress(source, span, out var consumed, out var written, isFinalBlock: false);
        if (status != OperationStatus.Done || consumed != source.Length)
            throw Errors.ZstdEncodeFailed();

        status = _encoder.Flush(span[written..], out var flushed);
        if (status != OperationStatus.Done)
            throw Errors.ZstdEncodeFailed();

        target.Advance(written + flushed);
    }

    public override int GetMaxEncodedLength(int sourceLength)
    {
        if (sourceLength <= 0)
            return 0;

        var maxLength = ZstandardEncoder.GetMaxCompressedLength(sourceLength) + FlushOverhead;
        return maxLength >= int.MaxValue ? int.MaxValue : (int)maxLength;
    }

    // The encoder keeps the dictionary it was created with
    public override void Reset()
        => _encoder.Reset();
}
#endif
