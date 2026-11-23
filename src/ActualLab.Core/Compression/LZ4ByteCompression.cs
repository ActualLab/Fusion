using ActualLab.Compression.Internal.LZ4;

namespace ActualLab.Compression;

/// <summary>
/// The <see cref="ByteCompression"/> of <see cref="LZ4ByteCompressor"/> and <see cref="LZ4ByteDecompressor"/>.
/// Its dictionaries are LZ4's prepared state of the last 64 KiB of the data they're loaded from.
/// </summary>
public sealed class LZ4ByteCompression : ByteCompression
{
    public static LZ4ByteCompression Default { get; } = new();

    // 0 means LZ4ByteCompressor.DefaultBlockSize; both peers must use the same block size
    public int BlockSize { get; }

    public LZ4ByteCompression(int blockSize = 0)
        => BlockSize = LZ4ByteCompressor.ValidateBlockSize(blockSize);

    public override ByteCompressor NewCompressor(object? dictionary = null)
        => new LZ4ByteCompressor(BlockSize, dictionary);

    public override ByteDecompressor NewDecompressor(object? dictionary = null)
        => new LZ4ByteDecompressor(BlockSize, dictionary);

    public override object LoadDictionary(ReadOnlySpan<byte> data)
        => new LZ4FastDictionary(data);

    public override byte[] SaveDictionary(object dictionary)
        => ToLZ4Dictionary(dictionary)!.Span.ToArray();

    // Internal methods

    internal static LZ4FastDictionary? ToLZ4Dictionary(object? dictionary)
        => dictionary switch {
            null => null,
            LZ4FastDictionary lz4Dictionary => lz4Dictionary,
            _ => throw new ArgumentOutOfRangeException(nameof(dictionary),
                $"Expected a dictionary made by {nameof(LZ4ByteCompression)}, got {dictionary.GetType().GetName()}."),
        };
}
