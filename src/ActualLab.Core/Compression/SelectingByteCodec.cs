namespace ActualLab.Compression;

// Returns the index of the codec (in SelectingByteCodec.Codecs) to encode the frame with
public delegate int ByteCodecSelector(ReadOnlySpan<byte> frame);

/// <summary>
/// A <see cref="ByteCodec"/> that encodes each frame with one of its <see cref="Codecs"/>, picked by
/// <see cref="Selector"/> - e.g. LZ4 for small frames and Zstandard for large ones. Every encoded frame
/// starts with the index of its codec (1 byte), and each codec keeps a context of its own, so it sees
/// only the frames encoded with it. The inner codecs carry their own dictionaries: its own
/// <see cref="ByteCodec.DictionaryData"/> is unused.
/// </summary>
public sealed record SelectingByteCodec : ByteCodec
{
    public const int MaxCodecCount = 256;

#if NET11_0_OR_GREATER
    // LZ4 with 64K hash slots below 1 KB, Zstandard (level 1 or 3) from 1 KB on
    public static SelectingByteCodec LZ4HL16_And_Zstd_At1K => LZ4HL16AndZstdPreset.Instance;
    public static SelectingByteCodec LZ4HL16_And_ZstdQ3_At1K => LZ4HL16AndZstdQ3Preset.Instance;
#endif

    public ImmutableArray<ByteCodec> Codecs {
        get;
        init {
            if (value.IsDefaultOrEmpty || value.Length > MaxCodecCount || value.Any(x => x is null))
                throw new ArgumentOutOfRangeException(nameof(value),
                    $"A selecting codec needs 1..{MaxCodecCount} codecs.");
            field = value;
        }
    }

    public ByteCodecSelector Selector {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    }

    public override string? DefaultName => null;

    public SelectingByteCodec(ByteCodecSelector selector, params ByteCodec[] codecs)
    {
        Selector = selector;
        Codecs = codecs.ToImmutableArray();
    }

    public override string ToString()
        => $"Selecting({string.Join(", ", Codecs)})";

    public override ByteEncoder NewEncoder()
        => new SelectingByteEncoder(this);

    public override ByteDecoder NewDecoder()
        => new SelectingByteDecoder(this);

#if NET11_0_OR_GREATER
    // Nested types

    // Presets are created on first use, each by its own holder
    private static class LZ4HL16AndZstdPreset
    {
        public static readonly SelectingByteCodec Instance
            = new(static frame => frame.Length < 1024 ? 0 : 1, LZ4ByteCodec.HL16, ZstdByteCodec.Default);
    }

    private static class LZ4HL16AndZstdQ3Preset
    {
        public static readonly SelectingByteCodec Instance
            = new(static frame => frame.Length < 1024 ? 0 : 1, LZ4ByteCodec.HL16, ZstdByteCodec.Q3);
    }
#endif
}
