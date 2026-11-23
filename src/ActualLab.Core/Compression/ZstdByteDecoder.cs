#if NET11_0_OR_GREATER
using System.Buffers;
using System.IO.Compression;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Compression;

/// <summary>
/// The decoding counterpart of <see cref="ZstdByteEncoder"/>.
/// </summary>
public sealed class ZstdByteDecoder : ByteDecoder
{
    private const int MinOutputSpanSize = 16 * 1024;

    private readonly ZstandardDecoder _decoder;

    public ZstdByteCodec Codec { get; }

    // The compression must match the peer encoder's one (dictionary)
    public ZstdByteDecoder(ZstdByteCodec? codec = null)
    {
        Codec = codec ??= ZstdByteCodec.Default;
        _decoder = codec.Dictionary is { } dictionary
            ? new ZstandardDecoder(dictionary, codec.MaxWindowLog2)
            : new ZstandardDecoder(codec.MaxWindowLog2);
    }

    public override void Dispose()
        => _decoder.Dispose();

    public override void Decode(ReadOnlyMemory<byte> source, IBufferWriter<byte> target, int maxLength)
    {
        var span = source.Span;
        var length = 0;
        while (true) {
            var output = target.GetSpan(MinOutputSpanSize);
            var status = _decoder.Decompress(span, output, out var consumed, out var written);
            if (status == OperationStatus.InvalidData)
                throw Errors.ZstdDecodeFailed();

            length += written;
            if (length > maxLength)
                throw Errors.SizeLimitExceeded("Decompressed frame");

            target.Advance(written);
            span = span[consumed..];
            if (status == OperationStatus.DestinationTooSmall)
                continue;
            // A frame ends with a flush, so once its input is consumed, everything it encodes is out;
            // the zstd frame itself never ends (Done), since the encoder never finishes it
            if (status != OperationStatus.NeedMoreData)
                throw Errors.ZstdDecodeFailed();
            if (span.IsEmpty)
                return;
            if (consumed == 0 && written == 0)
                throw Errors.ZstdDecodeFailed();
        }
    }

    // The decoder keeps the dictionary it was created with
    public override void Reset()
        => _decoder.Reset();
}
#endif
