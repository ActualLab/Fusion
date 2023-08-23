namespace Stl.Fusion.Operations.Internal;

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
        var isOperationRequired =
            context.IsOutermost // Should be a top-level command
            && command is not IMetaCommand // No operations for meta commands
            && !Computed.IsInvalidating();
        if (!isOperationRequired) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        var scope = context.Services.Activate<TransientOperationScope>();
        await using var _ = scope.ConfigureAwait(false);

        var operation = scope.Operation;
        operation.Command = command;
        context.Items.Set(scope);
        context.SetOperation(operation);

        try {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            if (!OperationCompletionNotifier.IsReady())
                throw Errors.OperationCompletionNotifierIsNotReady();
            scope.Close(true);
        }
        catch (Exception error) {
            scope.Close(false);
            if (error is OperationCanceledException)
                throw;

            // When this operation scope is used, no reprocessing is possible
            if (scope.IsUsed)
                Log.LogError(error, "Operation failed: {Command}", command);
            throw;
        }

        // Since this is the outermost scope handler, it's reasonable to
        // call OperationCompletionNotifier.NotifyCompleted from it
        var actualOperation = context.Items.Get<IOperation>() ?? operation;
        await OperationCompletionNotifier.NotifyCompleted(actualOperation, context).ConfigureAwait(false);
    }
}
