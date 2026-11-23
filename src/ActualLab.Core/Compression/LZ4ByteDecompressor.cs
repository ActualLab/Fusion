using System.Buffers;
using ActualLab.Compression.Internal.LZ4;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Compression;

/// <summary>
/// The decoding counterpart of <see cref="LZ4ByteCompressor"/>.
/// </summary>
public sealed class LZ4ByteDecompressor : ByteDecompressor
{
    private const int LengthSize = LZ4ByteCompressor.LengthSize;

    private readonly LZ4ChainDecoder _decoder;

    // The decoder's own block size - K4os rounds the requested one up, and a block can decode to
    // all of it, so this is what the output span must be sized for
    public int BlockSize { get; }

    // Must match the peer compressor's block size and dictionary; blockSize 0 means LZ4ByteCompressor.DefaultBlockSize
    public LZ4ByteDecompressor(int blockSize = 0, object? dictionary = null)
    {
        var blockSize1 = LZ4ByteCompressor.ValidateBlockSize(blockSize);
        _decoder = new LZ4ChainDecoder(blockSize1, extraBlocks: 0, LZ4ByteCompression.ToLZ4Dictionary(dictionary));
        Dictionary = dictionary;
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

    // Protected methods

    protected override void ResetCore(object? dictionary)
        => _decoder.Reset(LZ4ByteCompression.ToLZ4Dictionary(dictionary));
}
