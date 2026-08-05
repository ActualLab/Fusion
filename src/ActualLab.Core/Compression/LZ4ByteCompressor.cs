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
/// the largest block whose worst-case LZ4 output still fits that length prefix.
/// </remarks>
public sealed class LZ4ByteCompressor : ByteCompressor
{
    public const int LengthSize = sizeof(ushort);
    // A frame is a batch of messages capped at RpcFrameBasedTransport's FrameSize (12,000 by
    // default), so this holds a typical frame in one block - past which a larger block changes
    // nothing for that traffic, and only helps the occasional oversized frame.
    public const int DefaultBlockSize = 16 * 1024;
    // Derived from LZ4's own worst-case expansion rather than assumed, so it tracks the codec
    public static readonly int MaxBlockSize = ComputeMaxBlockSize();

    private readonly int _maxBlockOutputSize;
    private ILZ4Encoder _encoder;

    // The encoder's own block size, which is what every bound here is sized for - see the ctor
    public int BlockSize { get; }

    // blockSize 0 means DefaultBlockSize; MaxBlockSize is the largest value it accepts
    public LZ4ByteCompressor(int blockSize = 0)
    {
        _encoder = new LZ4FastChainEncoder(ValidateBlockSize(blockSize), extraBlocks: 0);
        // K4os rounds the requested block size up to a 1 KiB multiple, so the block it actually
        // fills is not necessarily the one we asked for - and it's the real one the length prefix
        // has to hold. Reading it back is what keeps the two in step.
        // Block size doesn't bound the match window: that's LZ4's own 64 KiB, and it spans blocks,
        // so a smaller block costs flush points rather than dictionary reach.
        BlockSize = _encoder.BlockSize;
        _maxBlockOutputSize = LZ4Codec.MaximumOutputSize(BlockSize);
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

    // Compress splits its input into ceil(sourceLength / BlockSize) blocks and never stores one,
    // so the bound is exact rather than an allowance - and it grows as BlockSize shrinks, which a
    // fixed per-frame estimate would miss.
    public override int GetMaxCompressedLength(int sourceLength)
    {
        if (sourceLength <= 0)
            return 0;

        var blockCount = ((long)sourceLength + BlockSize - 1) / BlockSize;
        var maxLength = blockCount * (LengthSize + _maxBlockOutputSize);
        return maxLength >= int.MaxValue ? int.MaxValue : (int)maxLength;
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
            return DefaultBlockSize;
        if (blockSize < 0 || blockSize > MaxBlockSize)
            throw new ArgumentOutOfRangeException(nameof(blockSize),
                $"Block size must be 0 (= {DefaultBlockSize}) or within 1..{MaxBlockSize}.");

        return blockSize;
    }

    // Private methods

    private static int ComputeMaxBlockSize()
    {
        // The largest block whose worst-case encoding still fits a ushort length prefix. K4os
        // rounds a request up to a 1 KiB multiple, so only those are candidates - and what the
        // prefix must hold is the size the encoder reports, not the one we asked for. Hence the
        // probe rather than a search over MaximumOutputSize alone. 64 KiB, LZ4's match window, is
        // the most a block could ever be worth; the answer is the 1 KiB step below it.
        const int Step = 1024;
        var min = 1;
        var max = 64 * 1024 / Step;
        while (min < max) {
            var mid = (min + max + 1) >> 1;
            if (GetMaxOutputSize(mid * Step) <= ushort.MaxValue)
                min = mid;
            else
                max = mid - 1;
        }
        return min * Step;

        static int GetMaxOutputSize(int blockSize)
        {
            using var encoder = new LZ4FastChainEncoder(blockSize, extraBlocks: 0);
            return LZ4Codec.MaximumOutputSize(encoder.BlockSize);
        }
    }
}
