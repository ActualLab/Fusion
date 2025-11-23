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

        var changeTokenSource = new CancellationTokenSource();
        var changeToken = changeTokenSource.Token;
        var shardLockDelayTask = Task.Delay(1000, changeToken);
        RouteState = new RpcShardRouteState(ShardLockAwaiter, changeToken);
        _ = Task.Run(CancelChangeTokenSource, CancellationToken.None);
        Initialize();
        return;

        async ValueTask<CancellationToken> ShardLockAwaiter(CancellationToken cancellationToken) {
            await shardLockDelayTask.WaitAsync(cancellationToken).SilentAwait(false);
            return changeToken;
        }

        async Task? CancelChangeTokenSource() {
            Console.WriteLine($"{Address}: created.".Pastel(ConsoleColor.Green));
            var computed = MeshState.State.Computed;
            if (HostId == "null")
                await computed.When(x => x.Hosts.Length > 0, CancellationToken.None).ConfigureAwait(false);
            else
                await computed.When(x => !x.HostById.ContainsKey(HostId), CancellationToken.None).ConfigureAwait(false);
            Cache.TryRemove(ShardRef, lazy);
            await changeTokenSource.CancelAsync();
            Console.WriteLine($"{Address}: rerouted.".Pastel(ConsoleColor.Yellow));
        }
    }
}
