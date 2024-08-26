using ActualLab.Rpc;
using Pastel;

namespace Samples.MeshRpc;

public sealed record RpcShardPeerRef : RpcPeerRef, IMeshPeerRef
{
    private static readonly ConcurrentDictionary<ShardRef, LazySlim<ShardRef, RpcShardPeerRef>> Cache = new();

    private volatile CancellationTokenSource? _rerouteTokenSource;

    public ShardRef ShardRef { get; }
    public Symbol HostId { get; }
    public override CancellationToken RerouteToken => _rerouteTokenSource?.Token ?? CancellationToken.None;

    public static Symbol GetKey(ShardRef shardRef)
    {
        var meshState = MeshState.State.Value;
        return $"{shardRef} v{meshState.Version} -> {meshState.GetShardHost(shardRef)?.Id.Value ?? "null"}";
    }

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

            sw.SpinOnce();
        }
    }

    private RpcShardPeerRef(ShardRef shardRef)
        : base(GetKey(shardRef))
    {
        ShardRef = shardRef;
        HostId = Key.Value.Split(" -> ")[1];
    }

    public void TryStart(LazySlim<ShardRef, RpcShardPeerRef> lazy)
    {
        if (_rerouteTokenSource != null)
            return;

        var rerouteTokenSource = new CancellationTokenSource();
        if (Interlocked.CompareExchange(ref _rerouteTokenSource, rerouteTokenSource, null) != null)
            return;

        _ = Task.Run(async () => {
            Console.WriteLine($"{Key}: created.".Pastel(ConsoleColor.Green));
            if (HostId == "null")
                await MeshState.State.When(x => x.Hosts.Length > 0).ConfigureAwait(false);
            else
                await MeshState.State.When(x => !x.HostById.ContainsKey(HostId)).ConfigureAwait(false);
            Cache.TryRemove(ShardRef, lazy);
            await _rerouteTokenSource.CancelAsync();
            Console.WriteLine($"{Key}: rerouted.".Pastel(ConsoleColor.Yellow));
        });
    }

    public override RpcPeerConnectionKind GetConnectionKind(RpcHub hub)
    {
        var ownHost = hub.Services.GetRequiredService<Host>();
        return HostId == ownHost.Id
            ? RpcPeerConnectionKind.Local
            : RpcPeerConnectionKind.Remote;
    }
}
