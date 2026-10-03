using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Diagnostics;
using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

/// <summary>
/// The two things one can do with a closed <see cref="DeferredInvalidationContext"/>'s blocks:
/// run them, or capture the <see cref="ServiceCall"/>s they'd make without running them.
/// </summary>
public static class DeferredInvalidationContextExt
{
    public static async Task InvokeBlocks(
        this DeferredInvalidationContext context,
        InvalidationSource source,
        ILogger? log = null)
    {
        var blocks = context.GetBlocks();
        if (blocks.Length == 0)
            return;

        using var _ = Invalidation.Begin(source);
        foreach (var block in blocks)
            await InvokeBlockSafely(block, log).ConfigureAwait(false);
    }

    public static async Task<ImmutableList<ServiceCall>> CollectInvalidationCalls(
        this DeferredInvalidationContext context,
        ILogger? log = null)
    {
        var blocks = context.GetBlocks();
        if (blocks.Length == 0)
            // Not []: ImmutableList<T> only got its CollectionBuilder in .NET 8, and this
            // assembly targets down to netstandard2.0
            return ImmutableList<ServiceCall>.Empty;

        // CaptureInvalidation is what makes a compute call land in this list instead of running,
        // so it is where the collected records come from
        var invalidations = new List<ServiceCall>();
        var computeContext = new ComputeContext(invalidations);
        using (computeContext.Activate())
            foreach (var block in blocks)
                await InvokeBlockSafely(block, log).ConfigureAwait(false);

        lock (invalidations)
            return invalidations.ToImmutableList();
    }

    // Private methods

    private static async Task InvokeBlockSafely(Delegate block, ILogger? log)
    {
        // A deferred block runs after its mutation committed, so failing it is not an option:
        // the write already landed, and all we can do is report the resulting staleness.
        try {
            if (block is Action a)
                a.Invoke();
            else if (block is Func<Task> f)
                await f.Invoke().ConfigureAwait(false);
            else
                throw Errors.DeferredInvalidationBlockTypeIsNotSupported(block.GetType());
        }
        catch (Exception e) {
            FusionInstruments.DeferredInvalidationFailureCount.Add(1);
            log?.LogError(e, "Deferred invalidation block failed - some computed values stay stale");
        }
    }
}
