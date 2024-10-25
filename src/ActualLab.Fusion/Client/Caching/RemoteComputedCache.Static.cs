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
    private static Func<ComputeMethodInput, RpcPeer, Task>? _updateDelayer;

    public static Func<ComputeMethodInput, RpcPeer, Task>? UpdateDelayer {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _updateDelayer;
        set {
            lock (StaticLock)
                _updateDelayer = value;
        }
    }
}
