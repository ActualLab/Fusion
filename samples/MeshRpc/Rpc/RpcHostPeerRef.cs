using ActualLab.Rpc;

namespace Samples.MeshRpc;

public sealed record RpcHostPeerRef : RpcPeerRef, IMeshPeerRef
{
    private static readonly ConcurrentDictionary<HostRef, RpcHostPeerRef> Cache = new();

    public Symbol HostId { get; }

    public static RpcHostPeerRef Get(HostRef hostRef)
        => Cache.GetOrAdd(hostRef, key => new RpcHostPeerRef(key));

    public RpcHostPeerRef(HostRef hostRef) : base(hostRef.ToString())
        => HostId = hostRef.Id;

    public override RpcPeerConnectionKind GetConnectionKind(RpcHub hub)
    {
        var ownHost = hub.Services.GetRequiredService<Host>();
        return HostId == ownHost.Id ? RpcPeerConnectionKind.Local : RpcPeerConnectionKind.Remote;
    }
}
