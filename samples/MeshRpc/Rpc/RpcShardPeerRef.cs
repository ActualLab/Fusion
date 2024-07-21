using System.Collections.Concurrent;
using ActualLab.Collections;
using ActualLab.Fusion;
using ActualLab.Rpc;
using ActualLab.Text;
using Pastel;

namespace Samples.MeshRpc;

public sealed record RpcShardPeerRef : RpcPeerRef, IMeshPeerRef
{
    private static readonly ConcurrentDictionary<ShardRef, RpcShardPeerRef> Cache = new();

    private int _isStarted;
    private readonly CancellationTokenSource _rerouteTokenSource;

    public int ShardKey { get; }
    public Symbol HostId { get; }
    public override CancellationToken RerouteToken { get; }

    public static Symbol GetKey(ShardRef shardRef)
    {
        var meshState = MeshState.State.Value;
        return $"{shardRef} v{meshState.Version} -> {meshState.GetShardHost(shardRef)?.Id.Value ?? "null"}";
    }

    public static RpcShardPeerRef Get(ShardRef shardRef)
    {
        while (true) {
            var peerRef = Cache.GetOrAdd(shardRef, key => new RpcShardPeerRef(key));
            if (peerRef.RerouteToken.IsCancellationRequested) {
                Cache.TryRemove(shardRef, peerRef);
                continue;
            }

            peerRef.TryStartRerouteTokenUpdater();
            return peerRef;
        }
    }

    public RpcShardPeerRef(ShardRef shardRef)
        : base(GetKey(shardRef))
    {
        ShardKey = shardRef.Key;
        HostId = Key.Value.Split(" -> ")[1];
        _rerouteTokenSource = new CancellationTokenSource();
        RerouteToken = _rerouteTokenSource.Token;
    }

    public void TryStartRerouteTokenUpdater()
    {
        if (Interlocked.CompareExchange(ref _isStarted, 1, 0) != 0)
            return;

        _ = Task.Run(async () => {
            Console.WriteLine($"{Key}: created.".Pastel(ConsoleColor.Green));
            if (HostId == "null")
                await MeshState.State.When(x => x.Hosts.Length > 0).ConfigureAwait(false);
            else
                await MeshState.State.When(x => !x.HostById.ContainsKey(HostId)).ConfigureAwait(false);
            Console.WriteLine($"{Key}: rerouted.".Pastel(ConsoleColor.Yellow));
            await _rerouteTokenSource.CancelAsync();
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
