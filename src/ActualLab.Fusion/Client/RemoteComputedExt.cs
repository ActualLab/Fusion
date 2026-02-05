using ActualLab.Fusion.Client.Internal;

namespace ActualLab.Fusion.Client;

/// <summary>
/// Extension methods for <see cref="IRemoteComputed"/>.
/// </summary>
public static class RemoteComputedExt
{
    // We don't want the methods below to be declared in generic RemoteComputed<T> class (AOT perf issue),
    // that's why they are extension methods.

    public static bool BindToCall(this IRemoteComputed computed, RpcOutboundComputeCall call)
    {
        if (computed.CallSource.TrySetResult(call)) {
            computed.BindWhenInvalidatedToCall(call);
            return true;
        }

        const string reason =
            $"<FusionRpc>.{nameof(BindToCall)}: {nameof(RpcOutboundComputeCall)} is already bound";
        call.SetInvalidated(true, reason);
        return false;
    }

    public static bool BindToCallFromOnInvalidated(this IRemoteComputed computed)
    {
        if (computed.CallSource.TrySetResult(null))
            return true;

        var boundCall = computed.WhenCallBound.GetAwaiter().GetResult();
        const string reason =
            $"<FusionRpc>.{nameof(BindToCall)}: associated {nameof(IRemoteComputed)} is already invalidated";
        boundCall?.SetInvalidated(true, reason);
        return false;
    }

    public static void BindWhenInvalidatedToCall(this IRemoteComputed computed, RpcOutboundComputeCall call)
    {
        var whenInvalidated = call.WhenInvalidated;
        if (whenInvalidated.IsCompleted) {
            // No call (call prepare error - e.g. if there is no such RPC service),
            // or the call result is already invalidated
            var invalidationSource = new InvalidationSource(whenInvalidated.GetAwaiter().GetResult());
            computed.Invalidate(immediately: true, invalidationSource);
            return;
        }

        _ = whenInvalidated.ContinueWith(
            t => {
                var invalidationSource = new InvalidationSource(t.GetAwaiter().GetResult());
                computed.Invalidate(immediately: true, invalidationSource);
            },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
