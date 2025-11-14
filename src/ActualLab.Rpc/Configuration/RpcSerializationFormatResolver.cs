using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Rpc;

public sealed record RpcSerializationFormatResolver
{
    // Static members

    [field: AllowNull, MaybeNull]
    public static ImmutableList<RpcSerializationFormat> DefaultFormats {
        get => field ??= RpcSerializationFormat.All; // Default format set
        set;
    }

    [field: AllowNull, MaybeNull]
    public static RpcSerializationFormatResolver Default {
        get => field ??= new(RpcSerializationFormat.MemoryPackV5.Key); // Default format
        set;
    }

    // Instance members

    public string DefaultFormatKey { get; init; }
    public RpcSerializationFormat DefaultFormat => Get(DefaultFormatKey);
    public IReadOnlyDictionary<string, RpcSerializationFormat> Formats { get; init; }

    public RpcSerializationFormatResolver(string defaultFormatKey)
        : this(defaultFormatKey, DefaultFormats)
    { }

    public RpcSerializationFormatResolver(string defaultFormatKey, IEnumerable<RpcSerializationFormat> formats)
        : this(defaultFormatKey, formats.ToDictionary(x => x.Key, StringComparer.Ordinal))
    { }

    public RpcSerializationFormatResolver(string defaultFormatKey, IReadOnlyDictionary<string, RpcSerializationFormat> formats)
    {
        if (defaultFormatKey.IsNullOrEmpty())
            throw new ArgumentException("defaultFormatKey is null or empty.", nameof(defaultFormatKey));
        if (!formats.ContainsKey(defaultFormatKey))
            throw new ArgumentException($"No format with key '{defaultFormatKey}'.", nameof(formats));

        DefaultFormatKey = defaultFormatKey;
        Formats = formats;
    }

    public override string ToString()
        => $"{GetType().GetName()}({DefaultFormatKey}, [{Formats.Keys.ToDelimitedString()}])";

    // Get and TryGet

    public RpcSerializationFormat Get(string key)
        => TryGet(key, out var value)
            ? value
            : throw new KeyNotFoundException($"No format with key '{key}'.");

    public bool TryGet(string key, [MaybeNullWhen(false)] out RpcSerializationFormat value)
    {
        if (key.IsNullOrEmpty())
            key = DefaultFormatKey;

        return Formats.TryGetValue(key, out value);
    }
}
