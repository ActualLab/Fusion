using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.MeshRpc;

public sealed class ShardPeerRef : RpcPeerRef
{
    public const int ShardCount = 2 * 3 * 4 * 5;

    private static long _version = 0;

    public MeshMap MeshMap { get; }
    public int ShardIndex { get; }
    public MeshHost? Host { get; }

    internal ShardPeerRef(MeshMap meshMap, int shardIndex, LazySlim<int, MeshMap, ShardPeerRef> entry)
    {
        MeshMap = meshMap;
        ShardIndex = shardIndex;
        var hostMapComputed = meshMap.State.Computed;
        Host = hostMapComputed.Value.GetHostByShardIndex(shardIndex);
        HostInfo = $"#{shardIndex}-{Host?.Id ?? "null"}-v{Interlocked.Increment(ref _version)}";
        Address = Host?.Url ?? "";
        UseReferentialEquality = true;

        var rerouteTokenSource = new CancellationTokenSource();
        RouteState = new RpcRouteState(rerouteTokenSource.Token);
        _ = Task.Run(async () => {
            await hostMapComputed
                .When(x => x.GetHostByShardIndex(ShardIndex) != Host)
                .ConfigureAwait(false);
            rerouteTokenSource.Cancel();
            meshMap.RemoveShardPeerRef(shardIndex, entry);
        }, CancellationToken.None);
        Initialize();
    }
}
