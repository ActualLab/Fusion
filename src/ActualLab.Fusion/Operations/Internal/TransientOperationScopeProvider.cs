namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// The outermost, "catch-all" operation provider for commands
/// that don't use any other operation scopes. Such commands may still
/// complete successfully & thus require an <see cref="ICompletion"/>-based
/// notification.
/// In addition, this scope actually "sends" this notification from
/// any other (nested) scope.
/// </summary>
public class TransientOperationScopeProvider(IServiceProvider services) : ICommandHandler<ICommand>
{
    private ILogger? _log;
    private IOperationCompletionNotifier? _operationCompletionNotifier;

    protected IServiceProvider Services { get; } = services;
    protected IOperationCompletionNotifier OperationCompletionNotifier
        => _operationCompletionNotifier ??= Services.GetRequiredService<IOperationCompletionNotifier>();
    protected ILogger Log => _log ??= Services.LogFor(GetType());

    [CommandFilter(Priority = FusionOperationsCommandHandlerPriority.TransientOperationScopeProvider)]
    public async Task OnCommand(ICommand command, CommandContext context, CancellationToken cancellationToken)
    {
        var isRequired =
            context.IsOutermost // Should be a top-level command
            && command is not ISystemCommand // No operations for system commands
            && !InvalidationMode.IsOn;
        if (!isRequired) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        var scope = new TransientOperationScope(context);
        var error = (Exception?)null;
        try {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            _ = scope.Commit(cancellationToken); // TransientOperationScope "commits" synchronously
        }
        catch (Exception e) {
            error = e;
            throw;
        }
        finally {
            // ReSharper disable once MethodHasAsyncOverload
            _ = scope.DisposeAsync();

            // The operation could be changed by one of nested operation scope providers
            var operation = context.Operation;
            if (operation.Scope is { IsUsed: true } operationScope) {
                if (scope.IsCommitted == true) {
                    // Since this is the outermost scope handler, it's reasonable to
                    // call OperationCompletionNotifier.NotifyCompleted from it
                    await OperationCompletionNotifier.NotifyCompleted(operation, context).ConfigureAwait(false);
                }
                else if (operationScope == scope) {
                    // No other operation scopes were used, so no reprocessing is possible
                    Log.LogError(error, "Transient operation failed: {Command}", command);
                }
            }
        }
    }
}
