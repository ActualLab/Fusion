using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion.Operations;

/// <summary>
/// The Operations Framework side of deferred invalidation: it freezes an operation's recorded
/// <see cref="ServiceCall"/>s at commit time and resolves what its committed row must be.
/// </summary>
public static class DeferredInvalidationHelper
{
    // An operation's invalidation calls are frozen at commit time: DbOperationScope.Commit adds
    // the row inside its transaction, so anything collected later never reaches it.
    public static async Task SetInvalidationsAndStoreMode(Operation operation)
    {
        var scope = operation.Scope.RequireActive();
        // One context, one mode - so this is the mode of everything it collected
        var context = scope.CommandContext.OutermostContext.Items.KeylessGet<DeferredInvalidationContext>();
        var mode = context is { BlockCount: > 0 } ? context.Mode : null;
        var isDistributed = mode is DeferredInvalidationMode.Distributed;
        var isReplicated  = mode is DeferredInvalidationMode.Replicated;
        if (mode is { } storedMode && (isDistributed || isReplicated)) {
            // Both modes are only as reliable as their carrier. The row is written inside the same
            // transaction as the mutation; a scope that stores nothing can't carry them at all.
            if (scope.IsTransient || scope.StoreMode == OperationStoreMode.None)
                throw Errors.DeferredInvalidationRequiresStoredOperation(storedMode, scope.GetType());

            // An operation row carries Replicated calls to every host; an event row carries
            // Distributed ones to a single host. One row can't do both.
            if (isReplicated && scope.StoreMode == OperationStoreMode.Event)
                throw Errors.ReplicatedInvalidationRequiresOperationRow();

            var log = scope.CommandContext.Services.LogFor(typeof(DeferredInvalidationContext));
            operation.AddInvalidationCalls(
                await context!.CollectInvalidationCalls(log).ConfigureAwait(false));
        }
        // Distributed pays for its invalidation once, on one host - but only when nothing else in
        // the operation needs the row every host reads, i.e. an event. That degrades it to
        // Operation, which costs more work than necessary and never less invalidation.
        var defaultStoreMode = GetDefaultStoreMode(operation, ignoreInvalidationCalls: isDistributed);
        scope.StoreMode ??= isDistributed && defaultStoreMode is not OperationStoreMode.Operation
            ? OperationStoreMode.Event
            : defaultStoreMode;
    }

    // ignoreInvalidationCalls: those calls are this operation's Distributed ones, i.e. the very
    // thing that travels in an event instead - so they mustn't vote for the operation row
    public static OperationStoreMode GetDefaultStoreMode(
        Operation operation, bool ignoreInvalidationCalls = false)
        => operation.Events.Count != 0
            || (!ignoreInvalidationCalls && !operation.InvalidationCalls.IsEmpty)
            ? OperationStoreMode.Operation
            : OperationStoreMode.None;
}
