using ActualLab.Rpc;

namespace Samples.MeshRpc;

public sealed class RpcHostPeerRef(HostRef hostRef)
    : RpcPeerRef(hostRef.ToString()), IMeshPeerRef
{
    private static readonly ConcurrentDictionary<HostRef, RpcHostPeerRef> Cache = new();

    public string HostId { get; } = hostRef.Id;

    public static RpcHostPeerRef Get(HostRef hostRef)
        => Cache.GetOrAdd(hostRef, key => new RpcHostPeerRef(key));
}
