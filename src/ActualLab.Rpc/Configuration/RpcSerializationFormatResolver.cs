using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Rpc;

public sealed record RpcSerializationFormatResolver(
    string DefaultServerFormatKey,
    string DefaultClientFormatKey,
    IReadOnlyDictionary<string, RpcSerializationFormat> Items
    ) : SimpleResolver<string, RpcSerializationFormat>(Items)
{
    // Static members

    [field: AllowNull, MaybeNull]
    public static ImmutableList<RpcSerializationFormat> DefaultFormats {
        get => field ??= RpcSerializationFormat.All;
        set;
    }

    [field: AllowNull, MaybeNull]
    public static RpcSerializationFormatResolver Default {
        get => field ??= NewDefault(
            RpcSerializationFormat.MemoryPackV1.Key, // Default server format, MemoryPackV1 to support pre-v8 Fusion clients
            RpcSerializationFormat.MemoryPackV2.Key); // Default client format
        set;
    }

    public static RpcSerializationFormatResolver NewDefault(string defaultFormatKey)
        => NewDefault(defaultFormatKey, defaultFormatKey);
    public static RpcSerializationFormatResolver NewDefault(string defaultServerFormatKey, string defaultClientFormatKey)
        => new(defaultServerFormatKey, defaultClientFormatKey, DefaultFormats.ToArray());

    // Instance members

    public RpcSerializationFormatResolver(string defaultFormatKey, params RpcSerializationFormat[] formats)
        : this(defaultFormatKey, defaultFormatKey, formats)
    { }

    public RpcSerializationFormatResolver(
        string defaultServerFormatKey,
        string defaultClientFormatKey,
        params RpcSerializationFormat[] formats)
        : this(defaultServerFormatKey, defaultClientFormatKey, formats.ToDictionary(x => x.Key, StringComparer.Ordinal))
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

    public RpcSerializationFormat Get(string key, bool isServer)
    {
        if (key.IsNullOrEmpty())
            key = isServer ? DefaultServerFormatKey : DefaultClientFormatKey;
        return this.Get(key);
    }
}
