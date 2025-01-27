using ActualLab.Fusion.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Client.Caching;

public partial class RemoteComputedCache
{
    private static readonly Lock StaticLock = LockFactory.Create();

    public static Func<ComputeMethodInput, RpcPeer, Task>? UpdateDelayer {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        set {
            lock (StaticLock)
                field = value;
        }
    }
}
