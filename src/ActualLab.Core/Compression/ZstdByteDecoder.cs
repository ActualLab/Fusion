#if NET11_0_OR_GREATER
using System.Buffers;
using System.Buffers.Binary;
using System.IO.Compression;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Compression;

/// <summary>
/// The decoding counterpart of <see cref="ZstdByteEncoder"/>.
/// </summary>
public sealed class ZstdByteDecoder : ByteDecoder
{
    private const int OutputSpanSizeHint = 16 * 1024;
    private const uint FrameMagic = 0xFD2FB528;

    private readonly ZstandardDecoder _decoder;
    private bool _isAtFrameStart = true;
    private bool _hasChecksum;

    public ZstdByteCodec Codec { get; }

    // The codec must match the peer encoder's one (dictionary)
    public ZstdByteDecoder(ZstdByteCodec? codec = null)
    {
        Codec = codec ??= ZstdByteCodec.Default;
        codec.ValidateWindows();
        _decoder = codec.Dictionary is { } dictionary
            ? new ZstandardDecoder(dictionary, codec.MaxWindowLog2)
            : new ZstandardDecoder(codec.MaxWindowLog2);
    }

    public override void Dispose()
        => _decoder.Dispose();

    public override void Decode(ReadOnlyMemory<byte> source, IBufferWriter<byte> target, int maxLength)
    {
        var span = source.Span;
        // zstd buffers an incomplete block silently, so a frame cut mid-block would decode "fine" and leak
        // its tail into the next one; every frame must therefore consist of whole blocks.
        if (!ScanBlocks(span, ref _isAtFrameStart, ref _hasChecksum))
            throw Errors.ZstdDecodeFailed();

        if (maxLength < 0)
            throw new ArgumentOutOfRangeException(nameof(maxLength));

        var length = 0;
        while (true) {
            // Never offer the decoder more room than the limit allows, plus one byte to detect an overrun
            var allowance = (int)Math.Min((long)maxLength - length + 1, int.MaxValue);
            var output = target.GetSpan(Math.Min(allowance, OutputSpanSizeHint));
            if (output.Length > allowance)
                output = output[..allowance];
            OperationStatus status;
            int consumed, written;
            try {
                status = _decoder.Decompress(span, output, out consumed, out written);
            }
            catch (IOException) { // E.g., a window larger than MaxWindowLog2 allows
                throw Errors.ZstdDecodeFailed();
            }
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
    {
        _decoder.Reset();
        _isAtFrameStart = true;
        _hasChecksum = false;
    }

    // Private methods

    // Checks that source is a sequence of whole zstd blocks (preceded by a frame header when a zstd frame
    // starts) and updates the frame state only if it is. See RFC 8878, sections 3.1.1.1 and 3.1.1.2.
    private static bool ScanBlocks(ReadOnlySpan<byte> source, ref bool isAtFrameStart, ref bool hasChecksum)
    {
        var atFrameStart = isAtFrameStart;
        var checksum = hasChecksum;
        var endsWithBlock = source.IsEmpty;
        var i = 0;
        while (i < source.Length) {
            var rest = source[i..];
            if (atFrameStart) {
                if (rest.Length < 5 || BinaryPrimitives.ReadUInt32LittleEndian(rest) != FrameMagic)
                    return false;

                var descriptor = rest[4];
                var isSingleSegment = (descriptor & 0x20) != 0;
                checksum = (descriptor & 0x04) != 0;
                var dictionaryIdSize = (descriptor & 0x03) switch { 0 => 0, 1 => 1, 2 => 2, _ => 4 };
                var contentSizeSize = (descriptor >> 6) switch {
                    0 => isSingleSegment ? 1 : 0,
                    1 => 2,
                    2 => 4,
                    _ => 8,
                };
                i += 5 + (isSingleSegment ? 0 : 1) + dictionaryIdSize + contentSizeSize;
                atFrameStart = false;
                endsWithBlock = false;
                continue;
            }

            if (rest.Length < 3)
                return false;

            var header = rest[0] | (rest[1] << 8) | (rest[2] << 16);
            var blockType = (header >> 1) & 3;
            if (blockType == 3) // Reserved
                return false;

            i += 3 + (blockType == 1 ? 1 : header >> 3); // An RLE block stores a single byte
            endsWithBlock = true;
            if ((header & 1) != 0) { // The last block of a zstd frame
                if (checksum)
                    i += 4;
                atFrameStart = true;
            }
        }
        // A non-empty frame always ends with a block: a frame header alone means it was cut
        if (i != source.Length || !endsWithBlock)
            return false;

        isAtFrameStart = atFrameStart;
        hasChecksum = checksum;
        return true;
    }
}
#endif
