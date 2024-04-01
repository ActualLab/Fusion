namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// This handler captures invocations of nested commands inside
/// operations and logs them into context.Operation().NestedCommands
/// so that invalidation for them could be auto-replayed too.
/// </summary>
public class NestedCommandLogger(IServiceProvider services) : ICommandHandler<ICommand>
{
    private PostCompletionInvalidator? _postCompletionInvalidator;
    private ILogger? _log;

    protected IServiceProvider Services { get; } = services;

    protected PostCompletionInvalidator PostCompletionInvalidator
        => _postCompletionInvalidator ??= Services.GetRequiredService<PostCompletionInvalidator>();
    protected ILogger Log => _log ??= Services.LogFor(GetType());

    [CommandFilter(Priority = FusionOperationsCommandHandlerPriority.NestedCommandLogger)]
    public async Task OnCommand(ICommand command, CommandContext context, CancellationToken cancellationToken)
    {
        var operation = context.OuterContext != null ? context.Items.Get<Operation>() : null;
        var mustBeLogged =
            operation != null // Should be a nested context inside a context w/ operation
            && PostCompletionInvalidator.MayRequireInvalidation(command) // Command may require invalidation
            && !Computed.IsInvalidating();
        if (!mustBeLogged) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        var oldOperationItems = operation!.Items;
        var operationItems = operation.Items = new OptionSet();
        Exception? error = null;
        try {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception? e) {
            error = e;
            throw;
        }
        finally {
            operation.Items = oldOperationItems;
            if (error == null) {
                // Downstream handler may change Operation to its own one,
                // current command must be logged as part of that operation.
                operation = context.Operation();
                if (operation.Scope is { IsClosed: false })
                    operation.NestedCommands.Add(new(command, operationItems));
            }
        }
    }
}
