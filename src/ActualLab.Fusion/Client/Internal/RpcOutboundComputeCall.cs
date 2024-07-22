using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;
using ActualLab.Rpc;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Client.Internal;

public interface IRpcOutboundComputeCall
{
    string? ResultVersion { get; }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    void SetInvalidated(RpcInboundContext context);
}

public class RpcOutboundComputeCall<TResult>(RpcOutboundContext context)
    : RpcOutboundCall<TResult>(context), IRpcOutboundComputeCall
{
    protected readonly TaskCompletionSource<Unit> WhenInvalidatedSource
        = TaskCompletionSourceExt.New<Unit>(); // Must not allow synchronous continuations!

    protected override string DebugTypeName => "=>";

    public string? ResultVersion { get; protected set; }
    // ReSharper disable once InconsistentlySynchronizedField
    public Task WhenInvalidated => WhenInvalidatedSource.Task;

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override Task Reconnect(bool isPeerChanged, CancellationToken cancellationToken)
    {
        // ReSharper disable once InconsistentlySynchronizedField
        if (!isPeerChanged || !ResultSource.Task.IsCompleted)
            return base.Reconnect(isPeerChanged, cancellationToken);

        SetInvalidated(false);
        return Task.CompletedTask;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void SetResult(object? result, RpcInboundContext? context)
    {
        var resultVersion = context.GetResultVersion();
        // We always use Lock to update ResultSource in this type
        lock (Lock) {
            // The code below is a copy of base.SetResult
            // except the Unregister call in the end.
            // We don't unregister the call here, coz
            // we'll need to await for invalidation
            var typedResult = default(TResult)!;
            try {
                if (result != null)
                    typedResult = (TResult)result;
            }
            catch (InvalidCastException) {
                // Intended
            }

            if (!ResultSource.TrySetResult(typedResult)) {
                // Result was set earlier; let's check for non-peer set or version mismatch
                if (resultVersion == null || !resultVersion.Equals(ResultVersion, StringComparison.Ordinal))
                    SetInvalidatedUnsafe(true);
                return;
            }

            Complete();
            ResultVersion = resultVersion;
            if (context != null)
                Context.CacheInfoCapture?.CaptureValue(context.Message);
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void SetMatch(RpcInboundContext? context)
    {
        var resultVersion = context.GetResultVersion();
        // We always use Lock to update ResultSource in this type
        lock (Lock) {
            // The code below is a copy of base.SetResult
            // except the Unregister call in the end.
            // We don't unregister the call here, coz
            // we'll need to await for invalidation
            var cachedEntry = Context.CacheInfoCapture?.CachedEntry as RpcCacheEntry<TResult>;
            if (cachedEntry == null) {
                SetError(Rpc.Internal.Errors.MatchButNoCachedEntry(), null);
                return;
            }

            if (!ResultSource.TrySetResult(cachedEntry.Result)) {
                // Result was set earlier; let's check for non-peer set or version mismatch
                if (resultVersion == null || !resultVersion.Equals(ResultVersion, StringComparison.Ordinal))
                    SetInvalidatedUnsafe(true);
                return;
            }

            Complete();
            ResultVersion = resultVersion;
            if (context != null)
                Context.CacheInfoCapture?.CaptureValue(context.Message);
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override void SetError(Exception error, RpcInboundContext? context, bool assumeCancelled = false)
    {
        var resultVersion = context.GetResultVersion();
        var oce = error as OperationCanceledException;
        if (error is RpcRerouteException)
            oce = null; // RpcRerouteException is OperationCanceledException, but must be exposed as-is here
        var cancellationToken = oce?.CancellationToken ?? default;
        // We always use Lock to update ResultSource in this type
        lock (Lock) {
            var isResultSet = oce != null
                ? ResultSource.TrySetCanceled(cancellationToken)
                : ResultSource.TrySetException(error);
            if (!isResultSet) {
                // Result was set earlier; let's check for non-peer set or version mismatch
                if (resultVersion == null || !resultVersion.Equals(ResultVersion, StringComparison.Ordinal))
                    SetInvalidatedUnsafe(!assumeCancelled);
                return;
            }

            // Result was just set
            Complete();
            ResultVersion = resultVersion;
            Context.CacheInfoCapture?.CaptureValue(oce != null, error, cancellationToken);
            if (context == null) // Non-peer set
                SetInvalidatedUnsafe(!assumeCancelled);
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public override bool Cancel(CancellationToken cancellationToken)
    {
        // We always use Lock to update ResultSource in this type
        lock (Lock) {
            var isCancelled = ResultSource.TrySetCanceled(cancellationToken);
            if (isCancelled)
                Context.CacheInfoCapture?.CaptureValue(cancellationToken);
            WhenInvalidatedSource.TrySetResult(default);
            CompleteAndUnregister(notifyCancelled: true);
            return isCancelled;
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public void SetInvalidated(RpcInboundContext? context)
        // Let's be pessimistic here and ignore version check here
        => SetInvalidated(false);

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public void SetInvalidated(bool notifyCancelled)
    {
        lock (Lock) {
            if (!SetInvalidatedUnsafe(notifyCancelled))
                return;

            if (ResultSource.TrySetCanceled())
                Context.CacheInfoCapture?.CaptureValue(CancellationToken.None);
        }
    }

    // Private methods

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    private bool SetInvalidatedUnsafe(bool notifyCancelled)
    {
        if (!WhenInvalidatedSource.TrySetResult(default))
            return false;

        CompleteAndUnregister(notifyCancelled);
        return true;
    }
}
