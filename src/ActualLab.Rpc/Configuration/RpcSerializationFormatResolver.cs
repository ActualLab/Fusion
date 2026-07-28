namespace ActualLab.Rpc;

/// <summary>
/// Resolves <see cref="RpcSerializationFormat"/> instances by key, with a configurable default.
/// </summary>
public sealed record RpcSerializationFormatResolver
{
    // Static members

    public static ImmutableList<RpcSerializationFormat> DefaultFormats {
        get => field ??= RpcSerializationFormat.All; // Default format set
        set;
    }

    // Newtonsoft-backed formats deserialize with TypeNameHandling.Auto and no SerializationBinder,
    // so they additionally honor nested "$type" markers - a gadget surface the other formats lack.
    // Defence in depth rather than a complete fix: polymorphic RPC arguments carry an out-of-band
    // type name that TypeRef.Resolve turns into a Type under every format.
    // To re-enable them:
    //   RpcSerializationFormatResolver.DefaultClientDeniedFormatKeys = ImmutableHashSet<string>.Empty;
    public static ImmutableHashSet<string> DefaultClientDeniedFormatKeys { get; set; }
        = ImmutableHashSet.Create(StringComparer.Ordinal,
            RpcSerializationFormat.NewtonsoftJsonV5.Key,
            RpcSerializationFormat.NewtonsoftJsonV5NP.Key);

    public static RpcSerializationFormatResolver Default {
        get => field ??= new(RpcSerializationFormat.MemoryPackV6.Key); // Default format
        set;
    }

    // Instance members

    public string DefaultFormatKey { get; init; }
    public RpcSerializationFormat DefaultFormat => Get(DefaultFormatKey);
    public IReadOnlyDictionary<string, RpcSerializationFormat> Formats { get; init; }
    public ImmutableHashSet<string> ClientDeniedFormatKeys { get; init; } = DefaultClientDeniedFormatKeys;

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

    // Client-selected formats

    public bool IsClientSelectable(string key)
        => !ClientDeniedFormatKeys.Contains(key);

    // Servers call this to validate the format key a client pinned in its connection URL;
    // an empty key means "server picks", so it's always fine.
    public string? GetClientRejectionReason(string key)
    {
        if (!TryGet(key, out _))
            return $"unsupported RPC serialization format '{key}'";
        if (!IsClientSelectable(key))
            return $"RPC serialization format '{key}' can't be selected by a client - set "
                + $"{nameof(RpcSerializationFormatResolver)}.{nameof(DefaultClientDeniedFormatKeys)} "
                + "to ImmutableHashSet<string>.Empty to allow it";

        return null;
    }
}
