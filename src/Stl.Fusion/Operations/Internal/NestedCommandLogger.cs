namespace Stl.Fusion.Operations.Internal;

/// <summary>
/// This handler captures invocations of nested commands inside
/// operations and logs them into context.Operation().Items
/// so that invalidation for them could be auto-replayed too.
/// </summary>
public class NestedCommandLogger(IServiceProvider services) : ICommandHandler<ICommand>
{
    private InvalidationInfoProvider? _invalidationInfoProvider;
    private ILogger? _log;

    protected IServiceProvider Services { get; } = services;

    protected InvalidationInfoProvider InvalidationInfoProvider
        => _invalidationInfoProvider ??= Services.GetRequiredService<InvalidationInfoProvider>();
    protected ILogger Log => _log ??= Services.LogFor(GetType());

    [CommandFilter(Priority = FusionOperationsCommandHandlerPriority.NestedCommandLogger)]
    public async Task OnCommand(ICommand command, CommandContext context, CancellationToken cancellationToken)
    {
        var operation = context.OuterContext != null ? context.Items.Get<IOperation>() : null;
        var mustBeLogged =
            operation != null // Should be a nested context inside a context w/ operation
            && InvalidationInfoProvider.RequiresInvalidation(command) // Command requires invalidation
            && !Computed.IsInvalidating();
        if (!mustBeLogged) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        var operationItems = operation!.Items;
        var commandItems = new OptionSet();
        operation.Items = commandItems;
        Exception? error = null;
        try {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception? e) {
            error = e;
            throw;
        }
        finally {
            operation.Items = operationItems;
            if (error == null) {
                var newOperation = context.Operation();
                if (newOperation != operation) {
                    // The operation might be changed by nested command in case
                    // it's the one that started to use DbOperationScope first
                    commandItems = newOperation.Items;
                    newOperation.Items = operationItems = new OptionSet();
                }
                var nestedCommands = operationItems.GetOrDefault(ImmutableList<NestedCommandEntry>.Empty);
                nestedCommands = nestedCommands.Add(new NestedCommandEntry(command, commandItems));
                operationItems.Set(nestedCommands);
            }
        }
    }
}
