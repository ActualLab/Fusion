namespace ActualLab.Compression;

// Returns the index of the codec (in SelectingByteCodec.Codecs) to encode the frame with
public delegate int ByteCodecSelector(ReadOnlySpan<byte> frame);

/// <summary>
/// A <see cref="ByteCodec"/> that encodes each frame with one of its <see cref="Codecs"/>, picked by
/// <see cref="Selector"/> - e.g. LZ4 for small frames and Zstandard for large ones. Every encoded frame
/// starts with the index of its codec (1 byte), and each codec keeps a context of its own, so it sees
/// only the frames encoded with it. The inner codecs carry their own dictionaries.
/// </summary>
public sealed record SelectingByteCodec : ByteCodec
{
    public const int MaxCodecCount = 256;

    public ImmutableArray<ByteCodec> Codecs {
        get;
        init {
            if (value.IsDefaultOrEmpty || value.Length > MaxCodecCount || value.Any(x => x is null))
                throw new ArgumentOutOfRangeException(nameof(value),
                    $"A selecting codec needs 1..{MaxCodecCount} codecs.");
            field = value;
        }
    }

    public ByteCodecSelector Selector { get; init; }

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
}
