using ActualLab.Rpc;

namespace Samples.MeshRpc;

public sealed class RpcHostPeerRef : RpcPeerRef, IMeshPeerRef
{
    private static readonly ConcurrentDictionary<HostRef, RpcHostPeerRef> Cache = new();

    public string HostId { get; }

    public static RpcHostPeerRef Get(HostRef hostRef)
        => Cache.GetOrAdd(hostRef, key => new RpcHostPeerRef(key));

    public RpcHostPeerRef(HostRef hostRef) : base(hostRef.ToString())
        => HostId = hostRef.Id;
}
