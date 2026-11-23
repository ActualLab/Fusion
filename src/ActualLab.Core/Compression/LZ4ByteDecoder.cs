using System.Buffers;
using ActualLab.Compression.Internal.LZ4;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Compression;

/// <summary>
/// The decoding counterpart of <see cref="LZ4ByteEncoder"/>.
/// </summary>
public sealed class LZ4ByteDecoder : ByteDecoder
{
    private const int LengthSize = LZ4ByteEncoder.LengthSize;

    private readonly LZ4ChainDecoder _decoder;

    public LZ4ByteCodec Codec { get; }
    // The decoder's own block size - K4os rounds the requested one up, and a block can decode to
    // all of it, so this is what the output span must be sized for
    public int BlockSize { get; }

    // The codec must match the peer encoder's one (block size and dictionary)
    public LZ4ByteDecoder(LZ4ByteCodec? codec = null)
    {
        Codec = codec ??= LZ4ByteCodec.Default;
        _decoder = new LZ4ChainDecoder(codec.BlockSize, codec.ExtraBlocks, codec.Dictionary);
        BlockSize = _decoder.BlockSize;
    }

    public override void Dispose()
        => _decoder.Dispose();

    public override void Decode(ReadOnlyMemory<byte> source, IBufferWriter<byte> target, int maxLength)
    {
        var span = source.Span;
        var length = 0;
        while (span.Length > 0) {
            if (span.Length < LengthSize)
                throw Errors.LZ4DecodeFailed();

            int encoded = span.ReadUInt16LittleEndian();
            span = span[LengthSize..];
            if (encoded == 0 || encoded > span.Length)
                throw Errors.LZ4DecodeFailed();

            int decoded;
            try {
                if (!_decoder.DecodeAndDrain(span[..encoded], target.GetSpan(BlockSize), out decoded))
                    throw Errors.LZ4DecodeFailed();
            }
            catch (InvalidOperationException) {
                // The decoder reports malformed input this way
                throw Errors.LZ4DecodeFailed();
            }

            length += decoded;
            if (length > maxLength)
                throw Errors.SizeLimitExceeded("Decompressed frame");

            target.Advance(decoded);
            span = span[encoded..];
        }
    }

    public override void Reset()
        => _decoder.Reset();
}
