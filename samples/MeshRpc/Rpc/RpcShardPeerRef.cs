using ActualLab.Rpc;
using Pastel;

namespace Samples.MeshRpc;

public sealed class RpcShardPeerRef : RpcPeerRef, IMeshPeerRef
{
    private static readonly ConcurrentDictionary<ShardRef, LazySlim<ShardRef, RpcShardPeerRef>> Cache = new();

    public ShardRef ShardRef { get; }
    public string HostId { get; }

    public static RpcShardPeerRef Get(ShardRef shardRef)
        => Cache.GetOrAdd(shardRef, static (shardRef, lazy) => new RpcShardPeerRef(shardRef, lazy));

    // Constructor is private to ensure all instances are created through the Get method
    private RpcShardPeerRef(ShardRef shardRef, LazySlim<ShardRef, RpcShardPeerRef> lazy)
    {
        var meshState = MeshState.State.Value;
        ShardRef = shardRef;
        HostId = meshState.GetShardHost(shardRef)?.Id ?? "null";
        HostInfo = $"{shardRef}-v{meshState.Version}->{HostId}";
        UseReferentialEquality = true;

        RouteState = new RpcRouteState();
        var initialExecutionDelayTask = Task.Delay(1000, RouteState.ChangedToken).SuppressExceptions();
        RouteState.LocalExecutionAwaiter =
            ct => initialExecutionDelayTask.IsCompleted
                ? default // Fast path
                : initialExecutionDelayTask.WaitAsync(ct).ToValueTask(); // Slow path
        _ = Task.Run(MarkRouteStateChanged, CancellationToken.None);
        Initialize();
        return;

        async Task? MarkRouteStateChanged() {
            Console.WriteLine($"{Address}: created.".Pastel(ConsoleColor.Green));
            var computed = MeshState.State.Computed;
            if (HostId == "null")
                await computed.When(x => x.Hosts.Length > 0, CancellationToken.None).ConfigureAwait(false);
            else
                await computed.When(x => !x.HostById.ContainsKey(HostId), CancellationToken.None).ConfigureAwait(false);
            Cache.TryRemove(ShardRef, lazy);
            RouteState.MarkChanged();
            Console.WriteLine($"{Address}: rerouted.".Pastel(ConsoleColor.Yellow));
        }
    }
}
