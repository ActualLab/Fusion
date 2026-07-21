using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.MeshRpc;

public sealed class RpcShardRoute : RpcRoute
{
    public MeshHost? Host { get; }

    internal RpcShardRoute(RpcShardRef rpcShardRef) : base(rpcShardRef)
    {
        var hostMapComputed = rpcShardRef.MeshMap.State.Computed;
        Host = hostMapComputed.Value.GetHostByShardIndex(rpcShardRef.ShardIndex);
        LocalExecutionAwaiter = async (addDependency, ct) => {
            // Gates local execution on current shard ownership: if the shard moved, but this
            // route's watcher hasn't fired MarkChanged yet, the call must still reroute.
            // With addDependency = true, locally-produced computeds also get a mesh state
            // dependency, so they invalidate when the mesh changes.
            var meshMap = rpcShardRef.MeshMap;
            var hosts = addDependency
                ? await meshMap.State.Use(ct).ConfigureAwait(false)
                : meshMap.State.Value;
            if (hosts.GetHostByShardIndex(rpcShardRef.ShardIndex) != Host)
                throw RpcRerouteException.MustReroute();
        };
        _ = Task.Run(async () => {
            try {
                await hostMapComputed
                    .When(x => x.GetHostByShardIndex(rpcShardRef.ShardIndex) != Host, ChangedToken)
                    .ConfigureAwait(false);
                MarkChanged();
            }
            catch (OperationCanceledException) {
                // The route was marked as changed by other means
            }
        }, CancellationToken.None);
    }

    protected override string GetTargetString()
        => Host?.Id ?? "";
}
