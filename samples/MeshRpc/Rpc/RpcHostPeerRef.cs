using ActualLab.Rpc;

namespace Samples.MeshRpc;

public sealed class RpcHostPeerRef : RpcPeerRef, IMeshPeerRef
{
    private static readonly ConcurrentDictionary<HostRef, RpcHostPeerRef> Cache = new();

    public static RpcHostPeerRef Get(HostRef hostRef)
        => Cache.GetOrAdd(hostRef, key => new RpcHostPeerRef(key));

    // Constructor is private to ensure all instances are created through the Get method
    private RpcHostPeerRef(HostRef hostRef)
    {
        HostId = hostRef.Id;
        Initialize();
    }
}
