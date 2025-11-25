namespace ActualLab.Rpc;

public static class RpcRouteStateExt
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsChanged(this RpcRouteState? routeState)
        => routeState is not null && routeState.ChangedToken.IsCancellationRequested;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RerouteIfChanged(this RpcRouteState? routeState, string? reason = null)
    {
        if (routeState.IsChanged())
            throw RpcRerouteException.MustReroute(reason);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task WhenChanged(this RpcRouteState? routeState)
        => routeState is null
            ? TaskExt.NewNeverEndingUnreferenced()
            : TaskExt.NeverEnding(routeState.ChangedToken).SuppressExceptions();

    public static Task WhenChanged(this RpcRouteState? routeState, CancellationToken cancellationToken)
    {
        if (routeState is null)
            return TaskExt.NeverEnding(cancellationToken);

        return cancellationToken.CanBeCanceled
            ? WhenChangedWithCancellationToken(routeState, cancellationToken)
            // ReSharper disable once MethodSupportsCancellation
            : routeState.WhenChanged();

        static async Task WhenChangedWithCancellationToken(RpcRouteState self, CancellationToken cancellationToken) {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, self.ChangedToken);
            await TaskExt.NeverEnding(linkedCts.Token).SilentAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
        }
    }

    public static ValueTask<CancellationTokenSource?> PrepareLocalExecution(
        this RpcRouteState? routeState, RpcMethodDef methodDef, CancellationToken cancellationToken)
    {
        if (methodDef.LocalExecutionMode == RpcLocalExecutionMode.Unconstrained
            || routeState?.LocalExecutionAwaiter is not { } localExecutionAwaiter)
            return default;

        var whenReadyTask = localExecutionAwaiter.Invoke(cancellationToken);
        if (whenReadyTask.IsCompletedSuccessfully) {
            if (methodDef.LocalExecutionMode == RpcLocalExecutionMode.ConstrainedEntry)
                routeState.RerouteIfChanged();
            return default;
        }

        return CompleteAsync(routeState, methodDef, whenReadyTask, cancellationToken);

        static async ValueTask<CancellationTokenSource?> CompleteAsync(
            RpcRouteState routeState, RpcMethodDef methodDef, ValueTask whenReadyTask,
            CancellationToken cancellationToken)
        {
            await whenReadyTask.ConfigureAwait(false);
            if (methodDef.LocalExecutionMode == RpcLocalExecutionMode.ConstrainedEntry) {
                routeState.RerouteIfChanged();
                return null;
            }

            return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, routeState.ChangedToken);
        }
    }

    public static bool MustConvertToRpcRerouteException(
        this RpcRouteState? routeState,
        OperationCanceledException error,
        CancellationTokenSource? commonTokenSource,
        CancellationToken cancellationToken)
    {
        if (routeState is null)
            return false;
        if (cancellationToken.IsCancellationRequested)
            return false;
        if (commonTokenSource is null)
            return false;
        if (error is RpcRerouteException)
            return false;

        return routeState.ChangedToken.IsCancellationRequested;
    }

}
