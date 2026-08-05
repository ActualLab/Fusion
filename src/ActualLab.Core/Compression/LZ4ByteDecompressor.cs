using System.Buffers;
using K4os.Compression.LZ4.Encoders;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Compression;

/// <summary>
/// The decoding counterpart of <see cref="LZ4ByteCompressor"/>.
/// </summary>
public sealed class LZ4ByteDecompressor : ByteDecompressor
{
    private const int LengthSize = LZ4ByteCompressor.LengthSize;

    private ILZ4Decoder _decoder;

    // The decoder's own block size - K4os rounds the requested one up, and a block can decode to
    // all of it, so this is what the output span must be sized for
    public int BlockSize { get; }

    // Must match the peer compressor's block size; 0 means LZ4ByteCompressor.DefaultBlockSize
    public LZ4ByteDecompressor(int blockSize = 0)
    {
        _decoder = new LZ4ChainDecoder(LZ4ByteCompressor.ValidateBlockSize(blockSize), extraBlocks: 0);
        BlockSize = _decoder.BlockSize;
    }

    public override void Dispose()
        => _decoder.Dispose();

    public override void Decompress(ReadOnlyMemory<byte> source, IBufferWriter<byte> target, int maxLength)
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

            if (!_decoder.DecodeAndDrain(span[..encoded], target.GetSpan(BlockSize), out var decoded))
                throw Errors.LZ4DecodeFailed();

            length += decoded;
            if (length > maxLength)
                throw Errors.SizeLimitExceeded("Decompressed frame");

            target.Advance(decoded);
            span = span[encoded..];
        }
    }

    public override void Reset()
    {
        _decoder.Dispose();
        // BlockSize is already the rounded-up value, so it maps to itself
        _decoder = new LZ4ChainDecoder(BlockSize, extraBlocks: 0);
    }
}
