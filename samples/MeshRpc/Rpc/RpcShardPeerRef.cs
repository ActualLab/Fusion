using ActualLab.Rpc;
using Pastel;

namespace Samples.MeshRpc;

public sealed class RpcShardPeerRef : RpcPeerRef, IMeshPeerRef
{
    private static readonly ConcurrentDictionary<ShardRef, LazySlim<ShardRef, RpcShardPeerRef>> Cache = new();

    private volatile CancellationTokenSource? _rerouteTokenSource;

    public ShardRef ShardRef { get; }
    public string HostId { get; }
    public override CancellationToken RerouteToken => _rerouteTokenSource?.Token ?? CancellationToken.None;

    public static RpcShardPeerRef Get(ShardRef shardRef)
    {
        var sw = new SpinWait();
        while (true) {
            var lazy = Cache.GetOrAdd(shardRef,
                static shardRef1 => new LazySlim<ShardRef, RpcShardPeerRef>(shardRef1,
                    static shardRef2 => new RpcShardPeerRef(shardRef2)));
            var shardPeerRef = lazy.Value;
            if (!shardPeerRef.RerouteToken.IsCancellationRequested) {
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

        var rerouteTokenSource = new CancellationTokenSource();
        if (Interlocked.CompareExchange(ref _rerouteTokenSource, rerouteTokenSource, null) is not null)
            return;

        _ = Task.Run(async () => {
            Console.WriteLine($"{Address}: created.".Pastel(ConsoleColor.Green));
            var computed = MeshState.State.Computed;
            if (HostId == "null")
                await computed.When(x => x.Hosts.Length > 0, CancellationToken.None).ConfigureAwait(false);
            else
                await computed.When(x => !x.HostById.ContainsKey(HostId), CancellationToken.None).ConfigureAwait(false);
            Cache.TryRemove(ShardRef, lazy);
            await _rerouteTokenSource.CancelAsync();
            Console.WriteLine($"{Address}: rerouted.".Pastel(ConsoleColor.Yellow));
        }, CancellationToken.None);
    }
}
