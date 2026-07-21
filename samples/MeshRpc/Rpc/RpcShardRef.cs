using ActualLab.Rpc;

namespace Samples.MeshRpc;

public sealed class RpcShardRef : RpcRef
{
    private static readonly ConcurrentDictionary<ShardRef, RpcShardRef> Cache = new();

    public ShardRef ShardRef { get; }

    public static RpcShardRef Get(ShardRef shardRef)
        => Cache.GetOrAdd(shardRef, static key => new RpcShardRef(key));

    // Constructor is private to ensure all instances are created through the Get method
    private RpcShardRef(ShardRef shardRef)
    {
        ShardRef = shardRef;
        HostInfo = shardRef.ToString();
        UseReferentialEquality = true;
        Initialize();
    }

    // Protected methods

    protected override RpcRoute CreateRoute()
        => new RpcShardRoute(this);
}
