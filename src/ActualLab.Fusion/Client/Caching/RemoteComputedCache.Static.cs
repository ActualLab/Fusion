using ActualLab.Fusion.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Client.Caching;

public partial class RemoteComputedCache
{
#if NET9_0_OR_GREATER
    private static readonly Lock StaticLock = new();
#else
    private static readonly object StaticLock = new();
#endif

    public static Func<ComputeMethodInput, RpcPeer, Task?>? HitToCallDelayer {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get;
        set {
            lock (StaticLock)
                field = value;
        }
    }
}
