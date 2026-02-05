namespace ActualLab.Rpc.Caching;

/// <summary>
/// Represents a cached RPC call result, pairing a <see cref="RpcCacheKey"/> with its <see cref="RpcCacheValue"/>.
/// </summary>
public class RpcCacheEntry(RpcCacheKey key, RpcCacheValue value, object? deserializedValue = null)
{
    public static readonly RpcCacheEntry RequestHash = new RequestHashEntry();

    public RpcCacheKey Key { get; } = key;
    public RpcCacheValue Value { get; } = value;
    public object? DeserializedValue { get; } = deserializedValue;

    public override string ToString()
        => $"{{ {Key} -> {Value} }}";

    // Nested types

    /// <summary>
    /// Sentinel entry representing a request hash marker rather than an actual cached value.
    /// </summary>
    private sealed class RequestHashEntry() : RpcCacheEntry(null!, new RpcCacheValue(default, ""))
    {
        public override string ToString()
            => nameof(RequestHash);
    }
}
