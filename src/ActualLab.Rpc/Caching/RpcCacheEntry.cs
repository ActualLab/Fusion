namespace ActualLab.Rpc.Caching;

public class RpcCacheEntry(RpcCacheKey key, RpcCacheValue value, object? deserializedValue = null)
{
    public static readonly RpcCacheEntry RequestHash = new RequestHashEntry();

    public RpcCacheKey Key { get; } = key;
    public RpcCacheValue Value { get; } = value;
    public object? DeserializedValue { get; } = deserializedValue;

    public override string ToString()
        => $"{{ {Key} -> {Value} }}";

    // Nested types

    private sealed class RequestHashEntry() : RpcCacheEntry(null!, new RpcCacheValue(default, ""))
    {
        public override string ToString()
            => nameof(RequestHash);
    }
}
