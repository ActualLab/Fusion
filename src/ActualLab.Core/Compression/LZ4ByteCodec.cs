using ActualLab.Compression.Internal.LZ4;

namespace ActualLab.Compression;

/// <summary>
/// The <see cref="ByteCodec"/> of <see cref="LZ4ByteEncoder"/> and <see cref="LZ4ByteDecoder"/>.
/// Its dictionary is LZ4's prepared state of the last 64 KiB of <see cref="ByteCodec.DictionaryData"/>.
/// </summary>
public sealed record LZ4ByteCodec : ByteCodec
{
    // An encoder or decoder buffer holds LZ4's 64 KiB window plus 1 + ExtraBlocks blocks; once
    // it's full, the window slides back with a 64 KiB copy. With no extra blocks that happens on every frame
    // as soon as the window is full (with a dictionary, from the start), so this amortizes the copy.
    public const int DefaultExtraBlocks = 4;

    public static LZ4ByteCodec Default { get; } = new();

    // Both peers must use the same block size; 0 means LZ4ByteEncoder.DefaultBlockSize,
    // and LZ4ByteEncoder.MaxBlockSize is the largest value it accepts
    public int BlockSize {
        get;
        init => field = LZ4ByteEncoder.ValidateBlockSize(value);
    } = LZ4ByteEncoder.DefaultBlockSize;

    // Local to each peer: it sizes the buffer, not the wire format
    public int ExtraBlocks {
        get;
        init => field = value is >= 0 and <= 1024
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), "Extra blocks must be within 0..1024.");
    } = DefaultExtraBlocks;

    internal LZ4FastDictionary? Dictionary
        => GetPreparedDictionary(static c => new LZ4FastDictionary(c.DictionaryData.Span));

    public override string? DefaultName
        => !HasDictionary && BlockSize == LZ4ByteEncoder.DefaultBlockSize ? "lz4" : null;

    public override string ToString()
        => $"LZ4({FormatSize(BlockSize)}{FormatDictionary()})";

    public override ByteEncoder NewEncoder()
        => new LZ4ByteEncoder(this);

    public override ByteDecoder NewDecoder()
        => new LZ4ByteDecoder(this);
}
