using ActualLab.Fusion.Client.Internal;

namespace ActualLab.Fusion.Client;

public static class RemoteComputedExt
{
    // We don't want the methods below to be declared in generic RemoteComputed<T> class (AOT perf issue),
    // that's why they are extension methods.

    public static bool BindToCall(this IRemoteComputed computed, RpcOutboundComputeCall? call)
    {
        if (!computed.CallSource.TrySetResult(call)) {
            // Another call is already bound
            if (call is null) {
                // Call from OnInvalidated - we need to cancel the old call
                var boundCall = computed.WhenCallBound.GetAwaiter().GetResult();
                boundCall?.SetInvalidated(true);
            }
            else {
                // Normal BindToCall, we cancel the call to ensure its invalidation sub. is gone
                call.SetInvalidated(true);
            }
            return false;
        }
        // If we're here, the computed is bound to the specified call

        if (call is not null) // Otherwise the null call originates from OnInvalidated
            computed.BindWhenInvalidatedToCall(call);
        return true;
    }

    public static void BindWhenInvalidatedToCall(this IRemoteComputed computed, RpcOutboundComputeCall call)
    {
        var whenInvalidated = call.WhenInvalidated;
        if (whenInvalidated.IsCompleted) {
            // No call (call prepare error - e.g. if there is no such RPC service),
            // or the call result is already invalidated
            computed.Invalidate(true);
            return;
        }

        _ = whenInvalidated.ContinueWith(
            _ => computed.Invalidate(true),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
