using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace Samples.MeshRpc;

public sealed class RpcHelpers(IServiceProvider services) : RpcServiceBase(services)
{
    private static readonly RpcCallTimeouts OutboundCallTimeouts = new(null, 60);

    public Host OwnHost => field ??= Services.GetRequiredService<Host>();

    public string HostUrlResolver(RpcClientPeer peer)
    {
        var hostId = peer.Route is RpcShardRoute shardRoute
            ? shardRoute.HostId
            : (peer.Ref as IRpcMeshRef)?.HostId;
        if (hostId is null)
            return "";

        var host = MeshState.State.Value.HostById.GetValueOrDefault(hostId);
        return host?.Url ?? "";
    }

    public Func<ArgumentList, RpcRef> RouterFactory(RpcMethodDef methodDef)
        => args => {
            if (args.Length == 0)
                return RpcRef.Local;

            var arg0Type = args.GetType(0);
            if (arg0Type == typeof(HostRef))
                return RpcHostRef.Get(args.Get<HostRef>(0));
            if (typeof(IHasHostRef).IsAssignableFrom(arg0Type))
                return RpcHostRef.Get(args.Get<IHasHostRef>(0).HostRef);

            if (arg0Type == typeof(ShardRef))
                return RpcShardRef.Get(args.Get<ShardRef>(0));
            if (typeof(IHasShardRef).IsAssignableFrom(arg0Type))
                return RpcShardRef.Get(args.Get<IHasShardRef>(0).ShardRef);

            if (arg0Type == typeof(int))
                return RpcShardRef.Get(ShardRef.New(args.Get<int>(0)));

            return RpcShardRef.Get(ShardRef.New(args.GetUntyped(0)));
        };

    public RpcPeerConnectionKind ConnectionKindDetector(RpcRoute route)
    {
        var rpcRef = route.Ref;
        var connectionKind = rpcRef.ConnectionKind;
        var hostId = route is RpcShardRoute shardRoute
            ? shardRoute.HostId
            : (rpcRef as IRpcMeshRef)?.HostId;
        if (hostId is null || connectionKind is not RpcPeerConnectionKind.Remote)
            return connectionKind;

        return hostId == OwnHost.Id
            ? RpcPeerConnectionKind.Local
            : RpcPeerConnectionKind.Remote;
    }

    public RpcCallTimeouts TimeoutsProvider(RpcMethodDef method)
        => OutboundCallTimeouts;
}
