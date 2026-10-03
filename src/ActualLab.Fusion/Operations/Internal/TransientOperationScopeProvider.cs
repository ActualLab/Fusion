using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// Provides Operation for commands relying on in-memory state
/// to ensure they get <see cref="ICompletion"/>-based notifications.
/// This provider also sends <see cref="ICompletion"/> for any other scope type,
/// and it owns the <see cref="DeferredInvalidationContext"/> of every command.
/// </summary>
public class TransientOperationScopeProvider(IServiceProvider services) : ICommandHandler<ICommand>
{
    protected IServiceProvider Services { get; } = services;
    protected IOperationCompletionNotifier OperationCompletionNotifier
        => field ??= Services.GetRequiredService<IOperationCompletionNotifier>();
    protected FusionOperationCompletionHandler FusionOperationCompletionHandler
        => field ??= Services.GetRequiredService<FusionOperationCompletionHandler>();
    protected DeferredInvalidationModeResolver DeferredInvalidationModeResolver
        => field ??= Services.GetRequiredService<DeferredInvalidationModeResolver>();
    protected ILogger DeferredInvalidationLog => field ??= Services.LogFor(typeof(DeferredInvalidationContext));
    protected ILogger Log => field ??= Services.LogFor(GetType());

    [CommandFilter(Priority = FusionOperationsCommandHandlerPriority.TransientOperationScopeProvider)]
    public async Task OnCommand(ICommand command, CommandContext context, CancellationToken cancellationToken)
    {
        var isRequired =
            context.IsOutermost // Should be a top-level command
            && command is not ISystemCommand // No operations for system commands
            && !Invalidation.IsActive;
        if (!isRequired) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        // This is the outermost Operations Framework filter, so it's where the operation's
        // invalidation context is created - but not where it's activated: that's
        // DeferredInvalidationScopeProvider, which runs below every scope provider. Publishing it
        // here is what lets both of them, and DeferredInvalidationHelper, reach the same context.
        // No Mode: the first handler that defers a block is what decides it, via the resolver.
        var deferredInvalidationContext = new DeferredInvalidationContext {
            ModeResolver = DeferredInvalidationModeResolver,
        };
        context.OutermostContext.Items.KeylessSet(deferredInvalidationContext);
        var error = (Exception?)null;
        try {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) {
            error = e;
            throw;
        }
        finally {
            var operation = context.TryGetOperation();
            if (operation?.Scope is { IsUsed: true } scope) {
                if (scope is TransientOperationScope) {
                    try {
                        if (error is null)
                            await scope.Commit(cancellationToken).ConfigureAwait(false);
                    }
                    finally {
                        await scope.DisposeAsync().ConfigureAwait(false);
                    }
                }
                // If scope is of another type, it's already committed/disposed at this point

                if (scope.IsCommitted == true) {
                    // Deferred invalidation is applied before the mutating call returns, unlike
                    // the replay-based pass, which CompletionProducer dispatches via Task.Run
                    await ApplyDeferredInvalidations(deferredInvalidationContext, operation).ConfigureAwait(false);
                    // Since this is the outermost scope handler, it's reasonable to
                    // call OperationCompletionNotifier.NotifyCompleted from it
                    await OperationCompletionNotifier.NotifyCompleted(operation, context).ConfigureAwait(false);
                }
                else if (scope is TransientOperationScope) {
                    // No other operation scopes were used, so no reprocessing is possible
                    Log.LogError(error, "Transient operation failed: {Command}", command);
                }
            }
            else if (error is null) {
                // No operation scope at all: "commit" here just means "the handler didn't throw",
                // and nothing can carry the invalidation anywhere - so Local works here, and neither
                // Replicated nor Distributed can
                if (deferredInvalidationContext is { BlockCount: not 0, Mode: { } mode }
                    && mode is DeferredInvalidationMode.Replicated or DeferredInvalidationMode.Distributed)
#pragma warning disable MA0072 // Safe to throw here, coz we checked that error is null earlier
                    throw Fusion.Internal.Errors.DeferredInvalidationRequiresStoredOperation(mode, null);
#pragma warning restore MA0072

                var invalidationSource = new InvalidationSource($"{command.GetType().GetName()}'s deferred invalidation");
                await deferredInvalidationContext
                    .InvokeBlocks(invalidationSource, DeferredInvalidationLog)
                    .ConfigureAwait(false);
            }
        }
    }

    // Protected methods

    protected virtual async Task RouteAndCompleteDeferredInvalidations(
        Operation operation, IOperationScope scope)
    {
        try {
            await FusionOperationCompletionHandler
                .ApplyRoutedInvalidations(operation.InvalidationCalls)
                .ConfigureAwait(false);
        }
        catch (Exception e) {
            Log.LogWarning(e, "Routed invalidation failed - it will be replayed: {Command}", operation.Command);
            return;
        }
        await scope.TryCompleteStoredEvent().ConfigureAwait(false);
    }

    protected virtual async Task ApplyDeferredInvalidations(DeferredInvalidationContext deferContext, Operation operation)
    {
        var source = new InvalidationSource($"{operation.Command?.GetType().GetName()}'s deferred invalidation");
        // Local blocks are the ones nothing else carries, so this is where they run. The other
        // modes' blocks were already captured into Operation.InvalidationCalls at commit time.
        if (deferContext.Mode is DeferredInvalidationMode.Local) {
            await deferContext.InvokeBlocks(source, DeferredInvalidationLog).ConfigureAwait(false);
            // A Local operation records no calls of its own, but a handler can still have added
            // some by hand - and every host that reads the operation applies those, so the origin
            // has to as well. Without this it would be the one host that doesn't.
            if (!operation.InvalidationCalls.IsEmpty)
                await FusionOperationCompletionHandler
                    .ApplyLocalInvalidations(operation.InvalidationCalls)
                    .ConfigureAwait(false);
            return;
        }
        if (operation.InvalidationCalls.IsEmpty)
            return;

        // StoreMode is what tells the two apart: Event means the calls travel to their owners and
        // the row is only there in case this host dies mid-way, Operation means every host applies
        // them to its own cache when it reads the log - including this one, right now.
        var scope = operation.Scope;
        if (scope?.StoreMode is not OperationStoreMode.Event) {
            await FusionOperationCompletionHandler
                .ApplyLocalInvalidations(operation.InvalidationCalls)
                .ConfigureAwait(false);
            return;
        }

        // This host's own copies are invalidated before the mutating call returns, so read-back
        // here is immediate, exactly as it is under Local. It's also idempotent with the routed
        // pass below, which invalidates an already-invalidated computed at no cost.
        await FusionOperationCompletionHandler
            .ApplyLocalInvalidations(operation.InvalidationCalls)
            .ConfigureAwait(false);

        // Routing is not on that critical path though: each call is a round trip to another host,
        // and a peer that's reconnecting would hold the caller for its connect timeout. The event
        // row is what makes this safe to fire and forget - it stays New until the routing actually
        // lands, so nothing is lost if this host dies or the routing fails.
        _ = Task.Run(() => RouteAndCompleteDeferredInvalidations(operation, scope));
    }
}
