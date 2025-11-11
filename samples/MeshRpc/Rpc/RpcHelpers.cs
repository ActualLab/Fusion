using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Infrastructure;

namespace Samples.MeshRpc;

public sealed class RpcHelpers(IServiceProvider services) : RpcServiceBase(services)
{
    [field: AllowNull, MaybeNull]
    public Host OwnHost => field ??= Services.GetRequiredService<Host>();

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
        if (hostId is null || connectionKind is not RpcPeerConnectionKind.Remote)
            return connectionKind;

        return hostId == OwnHost.Id
            ? RpcPeerConnectionKind.Local
            : RpcPeerConnectionKind.Remote;
    }
}
