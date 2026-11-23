#if NET11_0_OR_GREATER
using System.IO.Compression;

namespace ActualLab.Compression;

/// <summary>
/// The <see cref="ByteCodec"/> of <see cref="ZstdByteEncoder"/> and <see cref="ZstdByteDecoder"/>,
/// backed by .NET's own Zstandard (.NET 11+). Its dictionary is a <see cref="ZstandardDictionary"/> digested
/// once, for <see cref="Quality"/>, from <see cref="ByteCodec.DictionaryData"/> (a sample of typical data
/// or a trained dictionary), and attached to every context.
/// </summary>
public sealed record ZstdByteCodec : ByteCodec
{
    public const int DefaultQuality = 1;
    public const int DefaultMaxWindowLog2 = 23;

    public static ZstdByteCodec Default { get; } = new();

    // zstd's compression level: lower is faster, and negative levels trade ratio for speed
    public int Quality { get; init; } = DefaultQuality;
    // The largest window a decoder accepts: a peer can't make it allocate more than this
    public int MaxWindowLog2 { get; init; } = DefaultMaxWindowLog2;
    public ZstandardDictionary? Dictionary
        => GetPreparedDictionary(static c
            => ZstandardDictionary.Create(c.DictionaryData.Span, ((ZstdByteCodec)c).Quality));

    public override string? DefaultName
        => !HasDictionary && Quality == DefaultQuality && MaxWindowLog2 == DefaultMaxWindowLog2 ? "zstd" : null;

    public override string ToString()
        => $"Zstd(q{Quality}{FormatDictionary()})";

    public override ByteEncoder NewEncoder()
        => new ZstdByteEncoder(this);

    public override ByteDecoder NewDecoder()
        => new ZstdByteDecoder(this);
}
#endif
