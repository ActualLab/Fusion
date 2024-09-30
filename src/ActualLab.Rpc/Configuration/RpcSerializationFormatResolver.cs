namespace ActualLab.Rpc;

public sealed class RpcSerializationFormatResolver(
    Symbol defaultServerFormatKey,
    Symbol defaultClientFormatKey,
    params RpcSerializationFormat[] formats
    ) : SimpleResolver<Symbol, RpcSerializationFormat>(formats.ToDictionary(x => x.Key))
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

    public Symbol DefaultServerFormatKey { get; init; } = defaultServerFormatKey;
    public Symbol DefaultClientFormatKey { get; } = defaultClientFormatKey;

    public RpcSerializationFormatResolver(Symbol defaultFormatKey, params RpcSerializationFormat[] formats)
        : this(defaultFormatKey, defaultFormatKey, formats)
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
