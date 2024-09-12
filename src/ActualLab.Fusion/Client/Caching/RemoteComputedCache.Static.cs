using ActualLab.Fusion.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Client.Caching;

public partial class RemoteComputedCache
{
    private static Func<ComputeMethodInput, RpcPeer, Task>? _updateDelayer;
#if NET9_0_OR_GREATER
    private static readonly Lock Lock = new();
#else
    private static readonly object Lock = new();
#endif

    public static Func<ComputeMethodInput, RpcPeer, Task>? UpdateDelayer {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _updateDelayer;
        set {
            lock (Lock)
                _updateDelayer = value;
        }
    }
}
