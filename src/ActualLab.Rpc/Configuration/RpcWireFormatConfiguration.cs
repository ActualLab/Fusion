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

    public bool TryGetValue(string key, [MaybeNullWhen(false)] out RpcWireFormat value)
        => _formats.TryGetValue(key, out value);

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

    // Adds the LZ4 variants of every format:
    // "-lz4" for RpcCompressionMode.ServerToClient (the default when no mode is passed)
    // "-lz4f" for RpcCompressionMode.Full
    public void AddLZ4Variants(params ReadOnlySpan<RpcCompressionMode> compressionModes)
        => AddLZ4Variants(static _ => true, compressionModes);

    // Same, but only for the formats the filter accepts; text and compressed formats are always skipped,
    // since they can't be compressed
    public void AddLZ4Variants(
        Func<RpcWireFormat, bool> filter,
        params ReadOnlySpan<RpcCompressionMode> compressionModes)
    {
        if (compressionModes.IsEmpty)
            compressionModes = [RpcCompressionMode.ServerToClient];
        var variants = new List<RpcWireFormat>();
        foreach (var format in _formats.Values) {
            if (format.CompressionFormat is not null || format.DataFormat != DataFormat.Bytes || !filter.Invoke(format))
                continue;

            foreach (var compressionMode in compressionModes) {
                var variant = format.WithLZ4(compressionMode);
                if (!_formats.ContainsKey(variant.Key))
                    variants.Add(variant);
            }
        }
        Add(variants);
    }

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

    private void AssertNotFrozen()
    {
        if (IsFrozen)
            throw Errors.AlreadyReadOnly<RpcWireFormatConfiguration>();
    }
}
