using ActualLab.Rpc;

namespace Samples.MeshRpc;

public sealed class RpcHostRef : RpcRef, IRpcMeshRef
{
    private static readonly ConcurrentDictionary<HostRef, RpcHostRef> Cache = new();

    public string HostId { get; }

    public static RpcHostRef Get(HostRef hostRef)
        => Cache.GetOrAdd(hostRef, key => new RpcHostRef(key));

    // Constructor is private to ensure all instances are created through the Get method
    private RpcHostRef(HostRef hostRef)
    {
        HostInfo = HostId = hostRef.Id;
        UseReferentialEquality = true;
        Initialize();
    }
}
