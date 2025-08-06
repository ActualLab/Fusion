using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;

namespace Samples.MeshRpc;

public sealed class RpcHelpers(IServiceProvider services) : RpcServiceBase(services)
{
    private static readonly RpcCallTimeouts CallTimeouts = new(null, 60);

    [field: AllowNull, MaybeNull]
    public Host OwnHost => field ??= Services.GetRequiredService<Host>();

    public RpcCallTimeouts GetCallTimeouts(RpcMethodDef method)
        => CallTimeouts;

    public RpcPeerRef RouteCall(RpcMethodDef method, ArgumentList arguments)
    {
        if (method.IsCommand && Invalidation.IsActive)
            return RpcPeerRef.Local; // Commands in invalidation mode must always execute locally

        // Actual routing logic. We don't want too many conditions here: the routing runs per every RPC service call.
        if (arguments.Length == 0)
            return RpcPeerRef.Local;

        var arg0Type = arguments.GetType(0);
        if (arg0Type == typeof(HostRef))
            return RpcHostPeerRef.Get(arguments.Get<HostRef>(0));
        if (typeof(IHasHostRef).IsAssignableFrom(arg0Type))
            return RpcHostPeerRef.Get(arguments.Get<IHasHostRef>(0).HostRef);

        if (arg0Type == typeof(ShardRef))
            return RpcShardPeerRef.Get(arguments.Get<ShardRef>(0));
        if (typeof(IHasShardRef).IsAssignableFrom(arg0Type))
            return RpcShardPeerRef.Get(arguments.Get<IHasShardRef>(0).ShardRef);

        if (arg0Type == typeof(int))
            return RpcShardPeerRef.Get(ShardRef.New(arguments.Get<int>(0)));

        return RpcShardPeerRef.Get(ShardRef.New(arguments.GetUntyped(0)));
    }

    public string GetHostUrl(RpcWebSocketClient client, RpcClientPeer peer)
    {
        if (peer.Ref is not IMeshPeerRef meshPeerRef)
            return "";

        var host = MeshState.State.Value.HostById.GetValueOrDefault(meshPeerRef.HostId);
        return host?.Url ?? "";
    }

    public RpcPeerConnectionKind GetPeerConnectionKind(RpcHub hub, RpcPeerRef peerRef)
    {
        var connectionKind = peerRef.ConnectionKind;
        var hostId = peerRef switch {
            RpcHostPeerRef hostPeerRef => hostPeerRef.HostId,
            RpcShardPeerRef shardPeerRef => shardPeerRef.HostId,
            _ => null
        };
        if (hostId is null || connectionKind != RpcPeerConnectionKind.Remote)
            return connectionKind;

        return hostId == OwnHost.Id
            ? RpcPeerConnectionKind.Local
            : RpcPeerConnectionKind.Remote;
    }
}
