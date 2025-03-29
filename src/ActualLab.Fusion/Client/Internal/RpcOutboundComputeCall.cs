using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Internal;

public abstract class RpcOutboundComputeCall(RpcOutboundContext context) : RpcOutboundCall(context)
{
    protected readonly AsyncTaskMethodBuilder WhenInvalidatedSource = AsyncTaskMethodBuilderExt.New(); // Must not allow synchronous continuations!

    public override string DebugTypeName => "=>";
    public override int CompletedStage
        => ResultTask.IsCompleted
            ? WhenInvalidated.IsCompleted
                ? RpcCallStage.Invalidated | RpcCallStage.Unregistered
                : RpcCallStage.ResultReady
            : 0;

    // ReSharper disable once InconsistentlySynchronizedField
    public Task WhenInvalidated => WhenInvalidatedSource.Task;

    public override int? GetReconnectStage(bool isPeerChanged)
    {
        lock (Lock) {
            var completedStage = CompletedStage;
            if ((completedStage & RpcCallStage.Unregistered) != 0 || ServiceDef.Type == typeof(IRpcSystemCalls))
                return null;

            if (isPeerChanged && completedStage != 0) {
                // ResultTask is already set, but it originates from another peer.
                // The best we can do is to invalidate it.
                SetInvalidated(false);
                return null;
            }

            StartedAt = CpuTimestamp.Now;
            return completedStage;
        }
    }

    public override void SetResult(object? result, RpcInboundContext? context)
    {
        // We always use Lock to update ResultSource in this type
        lock (Lock) {
            // The code below is a copy of base.SetResult
            // except the Unregister call in the end.
            // We don't unregister the call here, coz
            // we'll need to await for invalidation
            if (result == null || !MethodDef.UnwrappedReturnType.IsInstanceOfType(result))
                result = MethodDef.DefaultResult;
            if (ResultSource.TrySetResult(result)) {
                CompleteKeepRegistered();
                Context.CacheInfoCapture?.CaptureValue(context!.Message);
            }
        }
    }

    public override void SetMatch(RpcInboundContext? context)
    {
        // We always use Lock to update ResultSource in this type
        lock (Lock) {
            // The code below is a copy of base.SetResult
            // except the Unregister call in the end.
            // We don't unregister the call here, coz
            // we'll need to await for invalidation
            var cacheEntry = Context.CacheInfoCapture?.CacheEntry;
            if (cacheEntry == null) {
                var error = Rpc.Internal.Errors.MatchButNoCachedEntry();
                SetError(error, context: null);
                Peer.InternalServices.Log.LogError(error,
                    "Got 'Match', but the outbound call has no cached entry: {Call}", this);
                return;
            }
            if (ResultSource.TrySetResult(cacheEntry.DeserializedValue)) {
                CompleteKeepRegistered();
                Context.CacheInfoCapture?.CaptureValue(cacheEntry.Value);
            }
        }
    }

    public override void SetError(Exception error, RpcInboundContext? context, bool assumeCancelled = false)
    {
        // SetError call not only sets the error, but also
        // invalidates computed method calls awaiting the invalidation, if context == null.

        var oce = error as OperationCanceledException;
        if (error is RpcRerouteException)
            oce = null; // RpcRerouteException is OperationCanceledException, but must be exposed as-is here
        var cancellationToken = oce?.CancellationToken ?? default;
        // We always use Lock to update ResultSource in this type
        lock (Lock) {
            var isResultSet = oce != null
                ? ResultSource.TrySetCanceled(cancellationToken)
                : ResultSource.TrySetException(error);
            if (isResultSet) {
                CompleteKeepRegistered();
                Context.CacheInfoCapture?.CaptureValue(oce != null, error, cancellationToken);
            }
            if (context == null) // Non-peer set
                SetInvalidatedUnsafe(!assumeCancelled);
        }
    }

    public override bool Cancel(CancellationToken cancellationToken)
    {
        // We always use Lock to update ResultSource in this type
        lock (Lock) {
            var isResultSet = ResultSource.TrySetCanceled(cancellationToken);
            if (isResultSet)
                Context.CacheInfoCapture?.CaptureValue(cancellationToken);
            WhenInvalidatedSource.TrySetResult();
            CompleteAndUnregister(notifyCancelled: true);
            return isResultSet;
        }
    }

    public void SetInvalidated(RpcInboundContext? context)
        // Let's be pessimistic here and ignore version check here
        => SetInvalidated(false);

    public void SetInvalidated(bool notifyCancelled)
    {
        lock (Lock) {
            if (!SetInvalidatedUnsafe(notifyCancelled))
                return;

            if (ResultSource.TrySetCanceled(CancellationTokenExt.Canceled))
                Context.CacheInfoCapture?.CaptureValue(CancellationToken.None);
        }
    }

    // Private methods

    private bool SetInvalidatedUnsafe(bool notifyCancelled)
    {
        if (!WhenInvalidatedSource.TrySetResult())
            return false;

        CompleteAndUnregister(notifyCancelled);
        return true;
    }
}

public sealed class RpcOutboundComputeCall<TResult> : RpcOutboundComputeCall
{
    public RpcOutboundComputeCall(RpcOutboundContext context) : base(context)
        => ResultSource = NewResultSource<TResult>();
}
