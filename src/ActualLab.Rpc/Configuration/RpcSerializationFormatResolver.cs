namespace ActualLab.Rpc;

public sealed record RpcSerializationFormatResolver(
    Symbol DefaultServerFormatKey,
    Symbol DefaultClientFormatKey,
    IReadOnlyDictionary<Symbol, RpcSerializationFormat> Items
    ) : SimpleResolver<Symbol, RpcSerializationFormat>(Items)
{
    // Static members

    private static ImmutableArray<RpcSerializationFormat>? _defaultFormats;
    private static RpcSerializationFormatResolver? _default;

    public static ImmutableArray<RpcSerializationFormat> DefaultFormats {
        get => _defaultFormats ??= RpcSerializationFormat.All;
        set => _defaultFormats = value;
    }

    public static RpcSerializationFormatResolver Default {
        get => _default ??= NewDefault(
            RpcSerializationFormat.MemoryPackV1.Key, // Default server format (should be this one for backward compatibility)
            RpcSerializationFormat.MemoryPackV2.Key); // Default client format (the newest and fastest one)
        set => _default = value;
    }

    public static RpcSerializationFormatResolver NewDefault(Symbol defaultFormatKey)
        => NewDefault(defaultFormatKey, defaultFormatKey);
    public static RpcSerializationFormatResolver NewDefault(Symbol defaultServerFormatKey, Symbol defaultClientFormatKey)
        => new(defaultServerFormatKey, defaultClientFormatKey, DefaultFormats.ToArray());

    // Instance members

    public RpcSerializationFormatResolver(Symbol defaultFormatKey, params RpcSerializationFormat[] formats)
        : this(defaultFormatKey, defaultFormatKey, formats)
    { }

    public RpcSerializationFormatResolver(
        Symbol defaultServerFormatKey,
        Symbol defaultClientFormatKey,
        params RpcSerializationFormat[] formats)
        : this(defaultServerFormatKey, defaultClientFormatKey, formats.ToDictionary(x => x.Key))
    { }

    public override string ToString()
        => $"{GetType().GetName()}([{Items.Keys.ToDelimitedString()}])";

    public RpcSerializationFormat GetDefault(bool isServer)
        => this.Get(isServer ? DefaultServerFormatKey : DefaultClientFormatKey);

    public RpcSerializationFormat Get(RpcPeerRef peerRef)
    {
        var key = peerRef.GetSerializationFormatKey();
        return Get(key, peerRef.IsServer);
    }

    public RpcSerializationFormat Get(Symbol key, bool isServer)
    {
        if (key.IsEmpty)
            key = isServer ? DefaultServerFormatKey : DefaultClientFormatKey;
        return this.Get(key);
    }
}
