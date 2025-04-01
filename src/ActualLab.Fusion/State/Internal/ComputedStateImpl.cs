namespace ActualLab.Fusion.Internal;

public static class ComputedStateImpl
{
    public static async Task UpdateCycle(
        IComputedState state,
        CancellationTokenSource? gracefulDisposeTokenSource)
    {
        var cancellationToken = state.DisposeToken;
        try {
            await state.Computed.UpdateUntyped(cancellationToken).ConfigureAwait(false);
            while (true) {
                var snapshot = state.Snapshot;
                var computed = snapshot.UntypedComputed;
                if (!computed.IsInvalidated())
                    await computed.WhenInvalidated(cancellationToken).ConfigureAwait(false);

                await state.UpdateDelayer.Delay(snapshot.RetryCount, cancellationToken).ConfigureAwait(false);

                if (!snapshot.WhenUpdated().IsCompleted)
                    // GracefulDisposeToken here allows Update to take some extra after DisposeToken cancellation.
                    // This, in particular, lets RPC calls to complete, cache entries to populate, etc.
                    await computed.UpdateUntyped(state.GracefulDisposeToken).ConfigureAwait(false);
            }
        }
        catch (Exception e) {
            if (!e.IsCancellationOf(cancellationToken))
                state.Services
                    .LogFor(state.GetType())
                    .LogError(e, "UpdateCycle() failed and stopped for {Category}", state.Category);
        }
        finally {
            gracefulDisposeTokenSource.CancelAndDisposeSilently();
        }
    }
}
