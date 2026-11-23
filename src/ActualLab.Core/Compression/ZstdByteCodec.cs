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
    public const int DefaultWindowLog2 = 17;
    public const int DefaultMaxWindowLog2 = 23;

    public static ZstdByteCodec Default { get; } = new();

    // zstd's compression level: lower is faster, and negative levels trade ratio for speed
    public int Quality {
        get;
        init {
            if (value < ZstandardCompressionOptions.MinQuality || value > ZstandardCompressionOptions.MaxQuality)
                throw new ArgumentOutOfRangeException(nameof(value));
            field = value;
        }
    } = DefaultQuality;

    // The encoder's window, i.e. how far back a match may reach. The default (128 KB) matches the
    // default RpcCompressionOptions.ContextResetBytes, so the dictionary stays reachable until a reset.
    public int WindowLog2 {
        get;
        init => field = ValidateWindowLog2(value);
    } = DefaultWindowLog2;

    // The largest window a decoder accepts: a peer can't make it keep a longer history than this
    public int MaxWindowLog2 {
        get;
        init => field = ValidateWindowLog2(value);
    } = DefaultMaxWindowLog2;

    internal ZstandardDictionary? Dictionary
        => GetPreparedDictionary(static c
            => ZstandardDictionary.Create(c.DictionaryData.Span, ((ZstdByteCodec)c).Quality));

    public override string? DefaultName
        => !HasDictionary && Quality == DefaultQuality && WindowLog2 == DefaultWindowLog2
            && MaxWindowLog2 == DefaultMaxWindowLog2
            ? "zstd" : null;

    public override string ToString()
        => $"Zstd(q{Quality}, {FormatSize(1 << WindowLog2)}{FormatDictionary()})";

    public override ByteEncoder NewEncoder()
        => new ZstdByteEncoder(this);

    public override ByteDecoder NewDecoder()
        => new ZstdByteDecoder(this);

    // Internal methods

    // Both peers usually share the codec, so a window its own decoder rejects is a configuration error
    internal void ValidateWindows()
    {
        if (WindowLog2 > MaxWindowLog2)
            throw new ArgumentOutOfRangeException(nameof(WindowLog2),
                $"{nameof(WindowLog2)} ({WindowLog2}) exceeds {nameof(MaxWindowLog2)} ({MaxWindowLog2}).");
    }

    // Private methods

    private static int ValidateWindowLog2(int value)
        => value < ZstandardCompressionOptions.MinWindowLog2 || value > ZstandardCompressionOptions.MaxWindowLog2
            ? throw new ArgumentOutOfRangeException(nameof(value))
            : value;
}
#endif
