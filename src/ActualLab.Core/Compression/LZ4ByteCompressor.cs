using System.Buffers;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Encoders;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Compression;

/// <summary>
/// A <see cref="ByteCompressor"/> backed by chained LZ4 blocks: every block is encoded against
/// the sliding window the earlier ones filled, which is what carries the dictionary across frames.
/// </summary>
/// <remarks>
/// A block is written as <c>[ushort encodedLength][data]</c>, so <see cref="MaxBlockSize"/> is
/// the largest input whose worst-case LZ4 output still fits that length prefix.
/// </remarks>
public sealed class LZ4ByteCompressor : ByteCompressor
{
    public const int LengthSize = sizeof(ushort);
    // Derived from LZ4's own worst-case expansion rather than assumed, so it tracks the codec.
    // It lands just under 64 KiB, which is also LZ4's match window - a larger block would cost
    // memory and buy nothing.
    public static readonly int MaxBlockSize = ComputeMaxBlockSize();

    private readonly int _maxBlockOutputSize;
    private ILZ4Encoder _encoder;

    public int BlockSize { get; }

    // blockSize 0 means MaxBlockSize, which is also the largest value it accepts
    public LZ4ByteCompressor(int blockSize = 0)
    {
        BlockSize = ValidateBlockSize(blockSize);
        _maxBlockOutputSize = LZ4Codec.MaximumOutputSize(BlockSize);
        _encoder = new LZ4FastChainEncoder(BlockSize, extraBlocks: 0);
    }

    public override void Dispose()
        => _encoder.Dispose();

    public override void Compress(ReadOnlySpan<byte> source, IBufferWriter<byte> target)
    {
        while (source.Length > 0) {
            // allowCopy: false plus a maximum-output-size target means the encoder always encodes,
            // so the decoder never has to Inject a stored block to keep its window in sync.
            var span = target.GetSpan(LengthSize + _maxBlockOutputSize);
            _encoder.TopupAndEncode(
                source, span[LengthSize..],
                forceEncode: true, allowCopy: false,
                out var loaded, out var encoded);
            // encoded can't exceed ushort.MaxValue - that's exactly what MaxBlockSize guarantees
            if (loaded <= 0 || encoded <= 0 || encoded > ushort.MaxValue)
                throw Errors.LZ4EncodeFailed();

            span.WriteLittleEndian((ushort)encoded);
            target.Advance(LengthSize + encoded);
            source = source[loaded..];
        }
    }

    public override void Reset()
    {
        _encoder.Dispose();
        _encoder = new LZ4FastChainEncoder(BlockSize, extraBlocks: 0);
    }

    // Internal methods

    internal static int ValidateBlockSize(int blockSize)
    {
        if (blockSize == 0)
            return MaxBlockSize;
        if (blockSize < 0 || blockSize > MaxBlockSize)
            throw new ArgumentOutOfRangeException(nameof(blockSize),
                $"Block size must be 0 (= {MaxBlockSize}) or within 1..{MaxBlockSize}.");

        return blockSize;
    }

    // Private methods

    private static int ComputeMaxBlockSize()
    {
        // The largest block whose worst-case encoding still fits a ushort length prefix.
        // MaximumOutputSize is monotonic, so a binary search over it is exact.
        var min = 1;
        var max = 64 * 1024;
        while (min < max) {
            var mid = (min + max + 1) >> 1;
            if (LZ4Codec.MaximumOutputSize(mid) <= ushort.MaxValue)
                min = mid;
            else
                max = mid - 1;
        }
        return min;
    }
}
