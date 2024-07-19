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

    public int ShardKey { get; }
    public Symbol HostId { get; }
    public override CancellationToken RerouteToken { get; }

    public static RpcShardPeerRef Get(ShardRef shardRef)
    {
        while (true) {
            var peerRef = Cache.GetOrAdd(shardRef, key => new RpcShardPeerRef(key));
            if (!peerRef.RerouteToken.IsCancellationRequested)
                return peerRef;

            Cache.TryRemove(shardRef, peerRef);
        }
    }

    public RpcShardPeerRef(ShardRef shardRef)
        : base($"{shardRef} -> {MeshState.State.Value.GetShardHost(shardRef)?.Id.Value ?? "null"}")
    {
        ShardKey = shardRef.Key;
        HostId = Key.Value.Split(" -> ")[1];
        var rerouteTokenSource = new CancellationTokenSource();
        RerouteToken = rerouteTokenSource.Token;
        Console.WriteLine($"{Key}: created.".Pastel(ConsoleColor.Green));
        _ = Task.Run(async () => {
            if (HostId == "null")
                await MeshState.State.When(x => x.Hosts.Count > 0).ConfigureAwait(false);
            else
                await MeshState.State.When(x => !x.HostById.ContainsKey(HostId)).ConfigureAwait(false);
            Console.WriteLine($"{Key}: rerouted.".Pastel(ConsoleColor.Yellow));
            rerouteTokenSource.Cancel();
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
