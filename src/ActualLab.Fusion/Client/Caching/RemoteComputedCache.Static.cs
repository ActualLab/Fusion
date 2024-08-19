using ActualLab.Fusion.Interception;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Client.Caching;

public partial class RemoteComputedCache
{
    private static Func<ComputeMethodInput, RpcPeer, Task>? _updateDelayer;
    private static readonly object Lock = new();

    public static Func<ComputeMethodInput, RpcPeer, Task>? UpdateDelayer {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _updateDelayer;
        set {
            lock (Lock)
                _updateDelayer = value;
        }
    }
}
