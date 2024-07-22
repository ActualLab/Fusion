namespace ActualLab.Rpc.Caching;

public abstract class RpcCacheEntry(RpcCacheKey key, RpcCacheValue value)
{
    public RpcCacheKey Key { get; } = key;
    public RpcCacheValue Value { get; } = value;

    public override string ToString()
        => $"{{ {Key} -> {Value} }}";
}

public sealed class RpcCacheEntry<T>(RpcCacheKey key, RpcCacheValue value, T result)
    : RpcCacheEntry(key, value)
{
    public T Result { get; } = result;
}
