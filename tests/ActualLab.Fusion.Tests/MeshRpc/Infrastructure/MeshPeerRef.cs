using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.MeshRpc;

public sealed class MeshPeerRef : RpcPeerRef
{
    private static long _version = 0;

    public MeshMap MeshMap { get; }
    public int RouteKey { get; }
    public MeshHost? Host { get; }
    public override CancellationToken RerouteToken { get; }

    internal MeshPeerRef(MeshMap meshMap, int routeKey, LazySlim<int, MeshMap, MeshPeerRef> lazy)
    {
        MeshMap = meshMap;
        RouteKey = routeKey;
        var hostMapComputed = meshMap.State.Computed;
        Host = hostMapComputed.Value.GetHostByRouteKey(routeKey);
        HostInfo = $"#{routeKey}-{Host?.Id ?? "null"}-v{Interlocked.Increment(ref _version)}";
        Address = Host?.Url ?? "";
        UseReferentialEquality = true;

        var rerouteTokenSource = new CancellationTokenSource();
        RerouteToken = rerouteTokenSource.Token;
        _ = Task.Run(async () => {
            await hostMapComputed
                .When(x => x.GetHostByRouteKey(RouteKey) != Host)
                .ConfigureAwait(false);
            rerouteTokenSource.Cancel();
            meshMap.RemovePeerRef(routeKey, lazy);
        }, CancellationToken.None);
        Initialize();
    }
}
