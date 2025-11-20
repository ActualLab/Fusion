using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace Samples.MeshRpc;

public sealed class RpcHelpers(IServiceProvider services) : RpcServiceBase(services)
{
    private static readonly RpcCallTimeouts OutboundCallTimeouts = new(null, 60);

    [field: AllowNull, MaybeNull]
    public Host OwnHost => field ??= Services.GetRequiredService<Host>();

    public string HostUrlResolver(RpcClientPeer peer)
    {
        if (peer.Ref is not IMeshPeerRef meshPeerRef)
            return "";

        var host = MeshState.State.Value.HostById.GetValueOrDefault(meshPeerRef.HostId);
        return host?.Url ?? "";
    }

    public Func<ArgumentList, RpcPeerRef> RouterFactory(RpcMethodDef methodDef)
        => args => {
            if (args.Length == 0)
                return RpcPeerRef.Local;

            var arg0Type = args.GetType(0);
            if (arg0Type == typeof(HostRef))
                return RpcHostPeerRef.Get(args.Get<HostRef>(0));
            if (typeof(IHasHostRef).IsAssignableFrom(arg0Type))
                return RpcHostPeerRef.Get(args.Get<IHasHostRef>(0).HostRef);

            if (arg0Type == typeof(ShardRef))
                return RpcShardPeerRef.Get(args.Get<ShardRef>(0));
            if (typeof(IHasShardRef).IsAssignableFrom(arg0Type))
                return RpcShardPeerRef.Get(args.Get<IHasShardRef>(0).ShardRef);

            if (arg0Type == typeof(int))
                return RpcShardPeerRef.Get(ShardRef.New(args.Get<int>(0)));

            return RpcShardPeerRef.Get(ShardRef.New(args.GetUntyped(0)));
        };

    public RpcPeerConnectionKind ConnectionKindDetector(RpcPeerRef peerRef)
    {
        var connectionKind = peerRef.ConnectionKind;
        var hostId = peerRef switch {
            RpcHostPeerRef hostPeerRef => hostPeerRef.HostId,
            RpcShardPeerRef shardPeerRef => shardPeerRef.HostId,
            _ => null
        };
        if (hostId is null || connectionKind is not RpcPeerConnectionKind.Remote)
            return connectionKind;

        return hostId == OwnHost.Id
            ? RpcPeerConnectionKind.Local
            : RpcPeerConnectionKind.Remote;
    }

    public RpcCallTimeouts TimeoutsProvider(RpcMethodDef method)
        => OutboundCallTimeouts;
}
