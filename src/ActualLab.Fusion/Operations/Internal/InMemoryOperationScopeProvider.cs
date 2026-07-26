using ActualLab.CommandR.Operations;

namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// Provides Operation for commands relying on in-memory state
/// to ensure they get <see cref="ICompletion"/>-based notifications.
/// This provider also sends <see cref="ICompletion"/> for any other scope type,
/// and it owns the <see cref="DeferInvalidationScope"/> of every command.
/// </summary>
public class InMemoryOperationScopeProvider(IServiceProvider services) : ICommandHandler<ICommand>
{
    protected IServiceProvider Services { get; } = services;
    protected IOperationCompletionNotifier OperationCompletionNotifier
        => field ??= Services.GetRequiredService<IOperationCompletionNotifier>();
    protected InvalidationCallApplier InvalidationCallApplier
        => field ??= Services.GetRequiredService<InvalidationCallApplier>();
    protected Func<InvalidationMode> InvalidationModeResolver
        => field ??= DeferredInvalidation.NewModeResolver(Services);
    protected ILogger DeferInvalidationLog => field ??= Services.LogFor(typeof(DeferredInvalidation));
    protected ILogger Log => field ??= Services.LogFor(GetType());

    [CommandFilter(Priority = FusionOperationsCommandHandlerPriority.InMemoryOperationScopeProvider)]
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

        // The capture scope is opened here so that Defer(...) works from a handler's first statement
        var deferScope = new DeferInvalidationScope(ExternallyDrivenDeferInvalidationHandler.Instance) {
            ModeResolver = InvalidationModeResolver,
            Log = DeferInvalidationLog,
        };
        var deferScopeHandle = DeferInvalidationScope.Begin(deferScope);
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
                if (scope is InMemoryOperationScope) {
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
                    await ApplyDeferredInvalidations(deferScope, operation!).ConfigureAwait(false);
                    // Since this is the outermost scope handler, it's reasonable to
                    // call OperationCompletionNotifier.NotifyCompleted from it
                    await OperationCompletionNotifier.NotifyCompleted(operation!, context).ConfigureAwait(false);
                }
                else if (scope is InMemoryOperationScope) {
                    // No other operation scopes were used, so no reprocessing is possible
                    Log.LogError(error, "Transient operation failed: {Command}", command);
                }
            }
            else if (error is null) {
                // No operation scope at all: "commit" here just means "the handler didn't throw",
                // and nothing can replicate the invalidation - so Local works here and Replicated can't
                if (deferScope.HasEntries(InvalidationMode.Replicated))
                    throw Fusion.Internal.Errors.ReplicatedInvalidationRequiresStoredOperation(null);

                await deferScope
                    .Run(new InvalidationSource($"{command.GetType().GetName()}'s deferred invalidation"))
                    .ConfigureAwait(false);
            }
            await deferScopeHandle.DisposeAsync().ConfigureAwait(false);
        }
    }

    // Protected methods

    protected virtual async Task ApplyDeferredInvalidations(DeferInvalidationScope deferScope, Operation operation)
    {
        var source = new InvalidationSource($"{operation.Command?.GetType().GetName()}'s deferred invalidation");
        await deferScope.Run(source).ConfigureAwait(false);
        await InvalidationCallApplier.Apply(operation.InvalidationCalls, source).ConfigureAwait(false);
    }
}
