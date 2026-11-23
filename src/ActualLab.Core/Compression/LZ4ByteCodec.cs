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
    public const int DefaultHashLog = 12;
    public const int MinHashLog = 10;
    public const int MaxHashLog = 16;

    public static LZ4ByteCodec Default { get; } = new();
    // 64K hash slots: finds more matches than the default 4K ones; the table takes 256 KB per encoder
    public static LZ4ByteCodec HL16 => Presets.HL16;

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

    // Local to the encoder: the match finder's hash table has 2^HashLog slots, and decoding doesn't
    // depend on it, so peers don't have to agree on it
    public int HashLog {
        get;
        init => field = value is >= MinHashLog and <= MaxHashLog
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value),
                $"Hash log must be within {MinHashLog}..{MaxHashLog}.");
    } = DefaultHashLog;

    internal LZ4FastDictionary? Dictionary
        => GetPreparedDictionary(static c
            => new LZ4FastDictionary(c.DictionaryData.Span, ((LZ4ByteCodec)c).HashLog));

    public override string? DefaultName
        => !HasDictionary && BlockSize == LZ4ByteEncoder.DefaultBlockSize ? "lz4" : null;

    public override string ToString()
        => HashLog == DefaultHashLog
            ? $"LZ4({FormatSize(BlockSize)}{FormatDictionary()})"
            : $"LZ4({FormatSize(BlockSize)}, HL{HashLog}{FormatDictionary()})";

    public override ByteEncoder NewEncoder()
        => new LZ4ByteEncoder(this);

    public override ByteDecoder NewDecoder()
        => new LZ4ByteDecoder(this);

    // Nested types

    // Presets are created on first use
    private static class Presets
    {
        public static readonly LZ4ByteCodec HL16 = new() { HashLog = 16 };
    }
}
