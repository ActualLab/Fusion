using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.MeshRpc;

public sealed class RpcShardRef : RpcRef
{
    public const int ShardCount = 2 * 3 * 4 * 5;

    public MeshMap MeshMap { get; }
    public int ShardIndex { get; }

    internal RpcShardRef(MeshMap meshMap, int shardIndex)
    {
        MeshMap = meshMap;
        ShardIndex = shardIndex;
        HostInfo = $"#{shardIndex}";
        UseReferentialEquality = true;
        Initialize();
    }

    // Protected methods

    protected override RpcRoute CreateRoute()
        => new RpcShardRoute(this);
}
