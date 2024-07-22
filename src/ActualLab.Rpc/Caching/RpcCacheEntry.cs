namespace ActualLab.Rpc.Caching;

public abstract class RpcCacheEntry(RpcCacheKey key, RpcCacheValue value)
{
    public static readonly RpcCacheEntry RequestHash = new RequestHashEntry();

    public RpcCacheKey Key { get; } = key;
    public RpcCacheValue Value { get; } = value;

    public override string ToString()
        => $"{{ {Key} -> {Value} }}";

    // Nested types

    private sealed class RequestHashEntry() : RpcCacheEntry(null!, new RpcCacheValue(default, ""))
    {
        public override string ToString()
            => nameof(RequestHash);
    }
}

public sealed class RpcCacheEntry<T>(RpcCacheKey key, RpcCacheValue value, T result)
    : RpcCacheEntry(key, value)
{
    public T Result { get; } = result;
}
