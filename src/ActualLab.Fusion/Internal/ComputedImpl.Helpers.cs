using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Internal;

public static partial class ComputedImpl
{
    public static void CopyAllDependenciesTo(Computed computed, ref ArrayBuffer<Computed> buffer)
    {
        var startCount = buffer.Count;
        computed.CopyDependenciesTo(ref buffer);
        var endCount = buffer.Count;
        for (var i = startCount; i < endCount; i++) {
            var c = buffer[i];
            c.CopyDependenciesTo(ref buffer);
        }
    }

    public static bool TryUseExisting(Computed? existing, ComputeContext context)
    {
        if (context.CallOptions != 0) // Way less frequent path
            return TryUseExistingWithCallOptions(existing, context);

        // The most frequent path
        if (existing == null || existing.ConsistencyState != ConsistencyState.Consistent)
            return false;

        // Inlined existing.UseNew(context, usedBy)
        context.Computed?.AddDependency(existing);
        existing.RenewTimeouts(false);
        return true;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool TryUseExistingWithCallOptions(Computed? existing, ComputeContext context) {
        var callOptions = context.CallOptions;
        var mustGetExisting = (callOptions & CallOptions.GetExisting) != 0;
        if (existing == null)
            return mustGetExisting;

        var mustInvalidate = (callOptions & CallOptions.Invalidate) == CallOptions.Invalidate;
        if (mustInvalidate) {
            // CallOptions.Invalidate is:
            // - always paired with CallOptions.GetExisting
            // - never paired with CallOptions.Capture
            existing.InvalidateFromCall();
            return true;
        }

        // CallOptions.GetExisting | CallOptions.Capture can be intact from here
        if (mustGetExisting) {
            context.TryCapture(existing);
            existing.RenewTimeouts(false);
            return true;
        }

        // Only CallOptions.Capture can be intact from here

        // The remaining part of this method matches exactly to TryUseExistingFromLock
        if (!existing.IsConsistent())
            return false;

        UseNew(existing, context);
        return true;
    }

    public static bool TryUseExistingFromLock(Computed? existing, ComputeContext context)
    {
        // We know that:
        // - CallOptions.GetExisting is unused here - it always leads to true in TryUseExisting,
        //   so we simply won't get to this method if it was used
        // - Since CallOptions.Invalidate implies GetExisting, it also can't be used here
        // So the only possible option is CallOptions.Capture
        if (existing == null || !existing.IsConsistent())
            return false;

        UseNew(existing, context);
        return true;
    }

    public static void UseNew(Computed computed, ComputeContext context)
    {
        context.Computed?.AddDependency(computed);
        computed.RenewTimeouts(true);
        context.TryCapture(computed);
    }

    public static T Strip<T>(Computed<T>? computed, ComputeContext context)
    {
        if (computed == null)
            return default!;
        if (CallOptions.GetExisting == (context.CallOptions & CallOptions.GetExisting))
            return default!;

        return computed.Value;
    }

    public static Task<T> StripToTask<T>(Computed<T>? computed, ComputeContext context)
    {
        if (computed == null)
            return TaskCache<T>.DefaultResultTask;
        if (CallOptions.GetExisting == (context.CallOptions & CallOptions.GetExisting))
            return TaskCache<T>.DefaultResultTask;

        return computed.OutputAsTask;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static Task FinalizeAndTryReprocessInternalCancellation<T>(
        string methodName,
        Computed<T> computed,
        Exception error,
        CpuTimestamp startedAt,
        ref int tryIndex,
        ILogger log,
        CancellationToken cancellationToken)
    {
        if (error is not OperationCanceledException) {
            // Not a cancellation
            computed.TrySetOutput(Result.Error<T>(error));
            return SpecialTasks.MustReturn;
        }

        if (cancellationToken.IsCancellationRequested || error is RpcRerouteException) {
            // !!! Cancellation of our own token & RpcRerouteException always "pass through"
            computed.Invalidate(true); // Instant invalidation on cancellation
            computed.TrySetOutput(Result.Error<T>(error));
            return SpecialTasks.MustThrow;
        }

        var cancellationReprocessingOptions = computed.Input.GetComputedOptions().CancellationReprocessing;
        if (++tryIndex > cancellationReprocessingOptions.MaxTryCount
            || startedAt.Elapsed > cancellationReprocessingOptions.MaxDuration) {
            // All of reprocessing attempts are exhauseted
            computed.TrySetOutput(Result.Error<T>(error));
            return SpecialTasks.MustReturn;
        }

        // If we're here:
        // - it's an internal cancellation (via CT other than cancellationToken)
        // - we must reprocess it w/ a delay.

        computed.Invalidate(true); // Instant invalidation on cancellation
        computed.TrySetOutput(Result.Error<T>(error));
        var delay = cancellationReprocessingOptions.RetryDelays[tryIndex];
        log.LogWarning(error,
            "{Method} #{TryIndex} for {Category} was cancelled internally, will retry in {Delay}",
            methodName, tryIndex, computed.Input.Category, delay.ToShortString());
        return delay <= TimeSpan.Zero
            ? Task.CompletedTask
            : Task.Delay(delay, cancellationToken);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static bool FinalizeAndTryReturnComputed<T>(
        Computed<T> computed,
        Exception error,
        CancellationToken cancellationToken)
    {
        if (error is not OperationCanceledException) {
            // Not a cancellation
            computed.TrySetOutput(Result.Error<T>(error));
            return true;
        }

        // Cancellation
        computed.Invalidate(true); // Instant invalidation on cancellation
        computed.TrySetOutput(Result.Error<T>(error));
        return !(cancellationToken.IsCancellationRequested || error is RpcRerouteException);
    }

    // Nested types

    private static class TaskCache<T>
    {
        public static readonly Task<T> DefaultResultTask = Task.FromResult(default(T)!);
    }
}
