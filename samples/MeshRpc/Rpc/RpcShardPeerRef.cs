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

    public static string GetId(ShardRef shardRef)
    {
        var meshState = MeshState.State.Value;
        return $"rpc://{shardRef}-v{meshState.Version}->{meshState.GetShardHost(shardRef)?.Id ?? "null"}";
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
        : base(GetId(shardRef))
    {
        ShardRef = shardRef;
        HostId = Parsed.Data.Split(" -> ")[1];
    }

    public void TryStart(LazySlim<ShardRef, RpcShardPeerRef> lazy)
    {
        if (_rerouteTokenSource is not null)
            return;

        var rerouteTokenSource = new CancellationTokenSource();
        if (Interlocked.CompareExchange(ref _rerouteTokenSource, rerouteTokenSource, null) is not null)
            return;

        _ = Task.Run(async () => {
            Console.WriteLine($"{Id}: created.".Pastel(ConsoleColor.Green));
            var computed = MeshState.State.Computed;
            if (HostId == "null")
                await computed.When(x => x.Hosts.Length > 0, CancellationToken.None).ConfigureAwait(false);
            else
                await computed.When(x => !x.HostById.ContainsKey(HostId), CancellationToken.None).ConfigureAwait(false);
            Cache.TryRemove(ShardRef, lazy);
            await _rerouteTokenSource.CancelAsync();
            Console.WriteLine($"{Id}: rerouted.".Pastel(ConsoleColor.Yellow));
        }, CancellationToken.None);
    }
}
