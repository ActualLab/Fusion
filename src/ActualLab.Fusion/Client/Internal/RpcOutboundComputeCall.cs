using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;

namespace ActualLab.Fusion.Client.Internal;

/// <summary>
/// An outbound RPC call for Fusion compute methods that tracks invalidation state
/// from the remote server.
/// </summary>
public abstract class RpcOutboundComputeCall : RpcOutboundCall
{
    protected readonly AsyncTaskMethodBuilder<string> WhenInvalidatedSource
        = AsyncTaskMethodBuilderExt.New<string>(); // Must not allow synchronous continuations!

    public override string DebugTypeName => "=>";
    public override int CompletedStage
        => ResultTask.IsCompleted
            ? WhenInvalidated.IsCompleted
                ? RpcCallStage.Invalidated | RpcCallStage.Unregistered
                : RpcCallStage.ResultReady
            : 0;

    // ReSharper disable once InconsistentlySynchronizedField
    public Task<string> WhenInvalidated => WhenInvalidatedSource.Task;

    protected RpcOutboundComputeCall(RpcOutboundContext context) : base(context)
        => IsLongLiving = true;

    public override int? GetReconnectStage(bool isPeerChanged)
    {
        lock (Lock) {
            var completedStage = CompletedStage;
            if ((completedStage & RpcCallStage.Unregistered) != 0 || ServiceDef.Type == typeof(IRpcSystemCalls))
                return null;

            if (isPeerChanged && completedStage != 0) {
                // ResultTask is already set, but it originates from another peer.
                // The best we can do is to invalidate it.
                const string reason =
                    $"<FusionRpc>.{nameof(GetReconnectStage)}: peer change for an already started call";
                SetInvalidated(notifyCancelled: false, reason);
                return null;
            }

            StartedAt = CpuTimestamp.Now;
            return completedStage;
        }
    }

    public override void SetResult(object? result, RpcInboundContext? context)
    {
        // We always use Lock to update ResultSource and call CacheInfoCapture.CaptureXxx
        lock (Lock) {
            // The code below is a copy of base.SetResult
            // except the Unregister call in the end.
            // We don't unregister the call here, coz
            // we'll need to await for invalidation
#if DEBUG
            if (!MethodDef.IsInstanceOfUnwrappedReturnType(result)) {
                var error = Errors.InvalidResultType(MethodDef.UnwrappedReturnType, result?.GetType());
                SetError(error, context);
                Peer.InternalServices.Log.LogError(error, "Got incorrect call result type: {Call}", this);
                return;
            }
#endif
            if (ResultSource.TrySetResult(result)) {
                CompleteKeepRegistered();
                Context.CacheInfoCapture?.CaptureValueFromLock(context!.Message);
            }
        }
    }

    public override void SetMatch(RpcInboundContext? context)
    {
        // We always use Lock to update ResultSource and call CacheInfoCapture.CaptureXxx
        lock (Lock) {
            // The code below is a copy of base.SetResult
            // except the Unregister call in the end.
            // We don't unregister the call here, coz
            // we'll need to await for invalidation
            var cacheInfoCapture = Context.CacheInfoCapture;
            var cacheEntry = cacheInfoCapture?.CacheEntry;
            if (cacheEntry is null) {
                var error = Errors.MatchButNoCachedEntry();
                SetError(error, context: null);
                Peer.InternalServices.Log.LogError(error,
                    "Got 'Match', but the outbound call has no cached entry: {Call}", this);
                return;
            }

            var result = cacheEntry.DeserializedValue;
#if DEBUG
            if (!MethodDef.IsInstanceOfUnwrappedReturnType(result)) {
                var error = Errors.InvalidResultType(MethodDef.UnwrappedReturnType, result?.GetType());
                SetError(error, context);
                Peer.InternalServices.Log.LogError(error,
                    "Got 'Match', but cache entry's serialized value has incorrect type: {Call}", this);
                return;
            }
#endif
            if (ResultSource.TrySetResult(result)) {
                CompleteKeepRegistered();
                cacheInfoCapture?.CaptureValueFromLock(cacheEntry.Value);
            }
        }
    }

    public override void SetError(Exception error, RpcInboundContext? context, bool assumeCancelled = false)
    {
        // SetError call not only sets the error, but also invalidates computed method calls
        // awaiting the invalidation if context is null.

        // WriteLine($"C-Error: {error.GetType().GetName()}, {error.Message}");
        var oce = error as OperationCanceledException;
        if (error is RpcRerouteException)
            oce = null; // RpcRerouteException is OperationCanceledException, but must be exposed as-is here
        var cancellationToken = oce?.CancellationToken ?? default;

        // We always use Lock to update ResultSource and call CacheInfoCapture.CaptureXxx
        lock (Lock) {
            var isResultSet = oce is not null
                ? ResultSource.TrySetCanceled(cancellationToken)
                : ResultSource.TrySetException(error);
            if (isResultSet) {
                CompleteKeepRegistered();
                Context.CacheInfoCapture?.CaptureErrorFromLock(oce is not null, error, cancellationToken);
            }
            if (context is null) {
                // Non-peer set
                const string reason =
                    $"<FusionRpc>.{nameof(SetError)}: non-peer error";
                SetInvalidatedUnsafe(!assumeCancelled, reason);
            }
        }
    }

    public override bool Cancel(CancellationToken cancellationToken)
    {
        // WriteLine("C-Cancel");
        // We always use Lock to update ResultSource and call CacheInfoCapture.CaptureXxx
        lock (Lock) {
            var isResultSet = ResultSource.TrySetCanceled(cancellationToken);
            if (isResultSet)
                Context.CacheInfoCapture?.CaptureCancellationFromLock(cancellationToken);
            WhenInvalidatedSource.TrySetResult($"{nameof(RpcOutboundCall)} got cancelled");
            CompleteAndUnregister(notifyCancelled: true);
            return isResultSet;
        }
    }

    public void SetInvalidated(RpcInboundContext? context, string reason)
        // Let's be pessimistic here and ignore version check here
        => SetInvalidated(false, reason);

    public void SetInvalidated(bool notifyCancelled, string reason)
    {
        lock (Lock) {
            if (!SetInvalidatedUnsafe(notifyCancelled, reason))
                return;

            if (ResultSource.TrySetCanceled(CancellationTokenExt.Canceled))
                Context.CacheInfoCapture?.CaptureCancellationFromLock(CancellationToken.None);
        }
    }

    // Private methods

    private bool SetInvalidatedUnsafe(bool notifyCancelled, string reason)
    {
        if (!WhenInvalidatedSource.TrySetResult(reason))
            return false;

        CompleteAndUnregister(notifyCancelled);
        return true;
    }
}

/// <summary>
/// A strongly-typed <see cref="RpcOutboundComputeCall"/> for a specific result type.
/// </summary>
public sealed class RpcOutboundComputeCall<TResult> : RpcOutboundComputeCall
{
    public RpcOutboundComputeCall(RpcOutboundContext context) : base(context)
        => ResultSource = CreateResultSource<TResult>();
}
