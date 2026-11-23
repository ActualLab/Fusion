using System.Globalization;

namespace ActualLab.Compression;

/// <summary>
/// A configured compression codec: an immutable record of its settings plus an optional preset dictionary
/// (<see cref="DictionaryData"/>), which it prepares once, on first use, and shares with every
/// <see cref="ByteEncoder"/> and <see cref="ByteDecoder"/> it creates. Configure it with
/// <c>with { ... }</c>: a copy prepares its dictionary again, for its own settings.
/// </summary>
public abstract record ByteCodec
{
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private object? _preparedDictionary;

    // The dictionary's source: a sample of typical data, or a dictionary trained for the codec.
    // It's used as is, so it must not change afterwards.
    public ReadOnlyMemory<byte> DictionaryData { get; init; }

    public bool HasDictionary => !DictionaryData.IsEmpty;

    // The name of this codec in its default configuration (e.g. "lz4"), or null if it's customized
    // in a way the peer must match, or has a dictionary: RPC reserves these names as key suffixes
    public abstract string? DefaultName { get; }

    protected ByteCodec()
    { }

    // A copy doesn't share the prepared dictionary: its settings may differ.
    // Field initializers don't run in a record's copy constructor, so _lock is set here.
    protected ByteCodec(ByteCodec original)
    {
        _lock = new();
        DictionaryData = original.DictionaryData;
    }

    public abstract ByteEncoder NewEncoder();
    public abstract ByteDecoder NewDecoder();

    // Reference equality: a codec is an identity, and two equal configurations
    // still prepare their dictionaries separately
    public virtual bool Equals(ByteCodec? other)
        => ReferenceEquals(this, other);

    public override int GetHashCode()
        => RuntimeHelpers.GetHashCode(this);

    // Protected methods

    // ToString helpers: e.g. "LZ4(16K+dict)"
    protected string FormatDictionary()
        => HasDictionary ? "+dict" : "";

    protected static string FormatSize(int size)
        => size % 1024 == 0 ? $"{size / 1024}K" : size.ToString(CultureInfo.InvariantCulture);

    // Prepares the dictionary once, on first use, with the settings of this instance
    protected T? GetPreparedDictionary<T>(Func<ByteCodec, T> factory)
        where T : class
    {
        if (!HasDictionary)
            return null;
        if (Volatile.Read(ref _preparedDictionary) is T prepared)
            return prepared;

        lock (_lock) {
            if (_preparedDictionary is T prepared1)
                return prepared1;

            prepared = factory.Invoke(this);
            Volatile.Write(ref _preparedDictionary, prepared);
            return prepared;
        }
    }
}
