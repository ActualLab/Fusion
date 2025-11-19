using ActualLab.Rpc;
using Pastel;

namespace Samples.MeshRpc;

public sealed class RpcShardPeerRef : RpcPeerRef, IMeshPeerRef
{
    private static readonly ConcurrentDictionary<ShardRef, LazySlim<ShardRef, RpcShardPeerRef>> Cache = new();

    private volatile CancellationTokenSource? _rerouteTokenSource;

    public ShardRef ShardRef { get; }
    public string HostId { get; }

    public static RpcShardPeerRef Get(ShardRef shardRef)
    {
        var sw = new SpinWait();
        while (true) {
            var lazy = Cache.GetOrAdd(shardRef,
                static shardRef1 => new LazySlim<ShardRef, RpcShardPeerRef>(shardRef1,
                    static shardRef2 => new RpcShardPeerRef(shardRef2)));
            var shardPeerRef = lazy.Value;
            if (!shardPeerRef.RouteState.IsChanged()) {
                shardPeerRef.TryStart(lazy);
                return shardPeerRef;
            }

            sw.SpinOnce(); // Safe for WASM
        }
    }

    // Constructor is private to ensure all instances are created through the Get method
    private RpcShardPeerRef(ShardRef shardRef)
    {
        var meshState = MeshState.State.Value;
        ShardRef = shardRef;
        HostId = meshState.GetShardHost(shardRef)?.Id ?? "null";
        HostInfo = $"{shardRef}-v{meshState.Version}->{HostId}";
        UseReferentialEquality = true;
        Initialize();
    }

    public void TryStart(LazySlim<ShardRef, RpcShardPeerRef> lazy)
    {
        if (_rerouteTokenSource is not null)
            return;

        var changeTokenSource = new CancellationTokenSource();
        if (Interlocked.CompareExchange(ref _rerouteTokenSource, changeTokenSource, null) is not null)
            return;

        // Initialize RouteState once we have a token source
        var changeToken = changeTokenSource.Token;
        var shardLockDelayTask = Task.Delay(1000, changeToken);
        RouteState = new RpcShardRouteState(WhenShardLocked, changeToken);
        _ = Task.Run(CancelChangeTokenSourceWhenRerouted, CancellationToken.None);
        return;

        async ValueTask<CancellationToken> WhenShardLocked(CancellationToken cancellationToken) {
            await shardLockDelayTask.WaitAsync(cancellationToken).SilentAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return changeToken;
        }

        async Task? CancelChangeTokenSourceWhenRerouted() {
            Console.WriteLine($"{Address}: created.".Pastel(ConsoleColor.Green));
            var computed = MeshState.State.Computed;
            if (HostId == "null")
                await computed.When(x => x.Hosts.Length > 0, CancellationToken.None).ConfigureAwait(false);
            else
                await computed.When(x => !x.HostById.ContainsKey(HostId), CancellationToken.None).ConfigureAwait(false);
            Cache.TryRemove(ShardRef, lazy);
            await _rerouteTokenSource.CancelAsync();
            Console.WriteLine($"{Address}: rerouted.".Pastel(ConsoleColor.Yellow));
        }
    }
}
