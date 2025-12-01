namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// Provides Operation for commands relying on in-memory state
/// to ensure they get <see cref="ICompletion"/>-based notifications.
/// This provider also sends <see cref="ICompletion"/> for any other scope type.
/// </summary>
public class InMemoryOperationScopeProvider(IServiceProvider services) : ICommandHandler<ICommand>
{
    protected IServiceProvider Services { get; } = services;
    protected IOperationCompletionNotifier OperationCompletionNotifier
        => field ??= Services.GetRequiredService<IOperationCompletionNotifier>();
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
                    if (error is null)
                        _ = scope.Commit(cancellationToken); // TransientOperationScope is fully synchronous
                    _ = scope.DisposeAsync(); // TransientOperationScope is fully synchronous
                }
                // If scope is of another type, it's already committed/disposed at this point

                if (scope.IsCommitted == true) {
                    // Since this is the outermost scope handler, it's reasonable to
                    // call OperationCompletionNotifier.NotifyCompleted from it
                    await OperationCompletionNotifier.NotifyCompleted(operation, context).ConfigureAwait(false);
                }
                else if (scope is InMemoryOperationScope) {
                    // No other operation scopes were used, so no reprocessing is possible
                    Log.LogError(error, "Transient operation failed: {Command}", command);
                }
            }
        }
    }
}
