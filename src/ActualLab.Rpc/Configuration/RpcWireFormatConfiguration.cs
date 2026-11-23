using ActualLab.Compression;
using ActualLab.Internal;
using ActualLab.Rpc.Compression;

namespace ActualLab.Rpc;

/// <summary>
/// The wire formats of a DI container, keyed by <see cref="RpcWireFormat.Key"/>,
/// plus its default format and the backend-only formats clients can't select.
/// It's <see cref="RpcConfiguration.WireFormats"/>, so configure it with
/// <see cref="RpcBuilder.ConfigureWireFormats"/>; it's frozen together with the configuration.
/// </summary>
public sealed class RpcWireFormatConfiguration : IReadOnlyDictionary<string, RpcWireFormat>
{
    private static readonly ImmutableDictionary<string, RpcWireFormat> DefaultFormats
        = RpcDefaultWireFormats.All.ToImmutableDictionary(x => x.Key, StringComparer.Ordinal);

    private ImmutableDictionary<string, RpcWireFormat> _formats = DefaultFormats;

    // Volatile on both sides: AssertNotFrozen reads it without a lock
    public bool IsFrozen {
        get => Volatile.Read(ref field);
        private set => Volatile.Write(ref field, value);
    }

    public RpcWireFormat DefaultFormat {
        get;
        set {
            AssertNotFrozen();
            field = value;
        }
    } = RpcDefaultWireFormats.DefaultFormat;

    public ImmutableHashSet<string> BackendOnlyFormatKeys {
        get;
        set {
            AssertNotFrozen();
            field = value;
        }
    } = RpcDefaultWireFormats.BackendOnlyFormatKeys;

    // IReadOnlyDictionary

    public int Count => _formats.Count;
    public RpcWireFormat this[string key] => _formats[key];
    public IEnumerable<string> Keys => _formats.Keys;
    public IEnumerable<RpcWireFormat> Values => _formats.Values;

    public bool ContainsKey(string key)
        => _formats.ContainsKey(key);

#pragma warning disable CS8767 // IReadOnlyDictionary<,> isn't null-annotated in .NET Standard
    public bool TryGetValue(string key, [MaybeNullWhen(false)] out RpcWireFormat value)
        => _formats.TryGetValue(key, out value);
#pragma warning restore CS8767

    public IEnumerator<KeyValuePair<string, RpcWireFormat>> GetEnumerator()
        => _formats.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    // Freeze

    // Every member is immutable, so freezing just stops further changes
    public void Freeze()
        => IsFrozen = true;

    // Add, Remove, Clear, Reset

    // A format with the key of an existing one replaces it
    public void Add(params ReadOnlySpan<RpcWireFormat> formats)
    {
        AssertNotFrozen();
        var result = _formats;
        foreach (var format in formats)
            result = result.SetItem(format.Key, format);
        SetFormats(result);
    }

    public void Add(IEnumerable<RpcWireFormat> formats)
    {
        AssertNotFrozen();
        var result = _formats;
        foreach (var format in formats)
            result = result.SetItem(format.Key, format);
        SetFormats(result);
    }

    // Replacing the default format with a same-key one makes the replacement the default
    private void SetFormats(ImmutableDictionary<string, RpcWireFormat> formats)
    {
        var defaultFormat = DefaultFormat;
        if (_formats.TryGetValue(defaultFormat.Key, out var oldFormat)
            && ReferenceEquals(oldFormat, defaultFormat)
            && formats.TryGetValue(defaultFormat.Key, out var newFormat))
            DefaultFormat = newFormat;
        _formats = formats;
    }

    // Adds the "<key>-<suffix>" variant of every format the filter accepts (all if there's no filter),
    // compressed with the compression in the given mode: e.g. ("lz4", LZ4ByteCodec.Default),
    // ("lz4f", LZ4ByteCodec.Default, RpcCompressionMode.Full), ("lz4d1", lz4WithDictionary).
    // The "lz4" / "lz4f" / "zstd" / "zstdf" suffixes are reserved for the default codecs in these modes.
    // Text and compressed formats are always skipped, since they can't be compressed;
    // a variant whose key is already taken fails the whole call.
    public void AddCompressedVariants(
        string suffix,
        ByteCodec codec,
        RpcCompressionMode compressionMode = RpcCompressionMode.ServerToClient,
        Func<RpcWireFormat, bool>? filter = null,
        RpcCompressionOptions? options = null)
        => AddCompressedVariants(
            suffix,
            format => filter is null || filter.Invoke(format) ? codec : null,
            compressionMode,
            options);

    // Same, but the factory picks the compression for each format (e.g. a dictionary per serializer);
    // null skips the format
    public void AddCompressedVariants(
        string suffix,
        Func<RpcWireFormat, ByteCodec?> codecFactory,
        RpcCompressionMode compressionMode = RpcCompressionMode.ServerToClient,
        RpcCompressionOptions? options = null)
    {
        if (suffix.IsNullOrEmpty())
            throw new ArgumentOutOfRangeException(nameof(suffix));

        var compressionFormats = new List<RpcCompressionFormat>();
        var variants = new List<RpcWireFormat>();
        foreach (var format in _formats.Values) {
            if (format.CompressionFormat is not null || format.DataFormat != DataFormat.Bytes)
                continue;
            if (codecFactory.Invoke(format) is not { } codec)
                continue;

            RpcWireFormat.ValidateSuffix(suffix, codec, compressionMode);
            var compressionFormat = compressionFormats.FirstOrDefault(x => ReferenceEquals(x.Codec, codec));
            if (compressionFormat is null) {
                compressionFormat = RpcCompressionFormat.Get(suffix, codec, options);
                compressionFormats.Add(compressionFormat);
            }
            var variant = format.WithCompressionFormat($"{format.Key}-{suffix}", compressionFormat, compressionMode);
            if (_formats.ContainsKey(variant.Key))
                throw new InvalidOperationException($"Format '{variant.Key}' is already added.");

            variants.Add(variant);
        }
        Add(variants);
    }

    // Adds "msgpack6k" and "msgpack6ck" (keyed MessagePack, see RpcDefaultWireFormats.MessagePackV6K)
    public void AddKeyedMessagePack()
        => AddNew(RpcDefaultWireFormats.MessagePackV6K, RpcDefaultWireFormats.MessagePackV6CK);

    public void Remove(params ReadOnlySpan<string> keys)
    {
        AssertNotFrozen();
        var result = _formats;
        foreach (var key in keys)
            result = result.Remove(key);
        _formats = result;
    }

    public void Clear()
    {
        AssertNotFrozen();
        _formats = _formats.Clear();
    }

    // Restores RpcDefaultWireFormats: the formats, the default format and the backend-only formats
    public void Reset()
    {
        AssertNotFrozen();
        _formats = DefaultFormats;
        DefaultFormat = RpcDefaultWireFormats.DefaultFormat;
        BackendOnlyFormatKeys = RpcDefaultWireFormats.BackendOnlyFormatKeys;
    }

    // Private methods

    // Like Add, but fails (adding nothing) if a key is already taken
    private void AddNew(params ReadOnlySpan<RpcWireFormat> formats)
    {
        foreach (var format in formats)
            if (_formats.ContainsKey(format.Key))
                throw new InvalidOperationException($"Format '{format.Key}' is already added.");
        Add(formats);
    }

    private void AssertNotFrozen()
    {
        if (IsFrozen)
            throw Errors.AlreadyReadOnly<RpcWireFormatConfiguration>();
    }
}
