using System.Diagnostics.CodeAnalysis;

namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// This handler captures invocations of nested commands inside
/// operations and logs them into context.Operation.NestedOperations
/// so that invalidation for them could be auto-replayed too.
/// </summary>
public class NestedOperationLogger(IServiceProvider services) : ICommandHandler<ICommand>
{
    protected IServiceProvider Services { get; } = services;

    [field: AllowNull, MaybeNull]
    protected InvalidatingCommandCompletionHandler InvalidatingCommandCompletionHandler
        => field ??= Services.GetRequiredService<InvalidatingCommandCompletionHandler>();
    [field: AllowNull, MaybeNull]
    protected ILogger Log => field ??= Services.LogFor(GetType());

    [CommandFilter(Priority = FusionOperationsCommandHandlerPriority.NestedCommandLogger)]
    public async Task OnCommand(ICommand command, CommandContext context, CancellationToken cancellationToken)
    {
        var mustBeUsed =
            context.OuterContext is not null // Should be a nested context
            && InvalidatingCommandCompletionHandler.IsRequired(command, out _)
            && !Invalidation.IsActive;
        if (!mustBeUsed) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        var operation = context.TryGetOperation();
        var operationItemsBackup = PropertyBag.Empty;
        if (operation is not null) {
            operationItemsBackup = operation.Items.Snapshot;
            operation.Items.Snapshot = PropertyBag.Empty;
        }

        try {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
        }
        finally {
            if (operation is not null) {
                // There was an operation already
                var operationItems = operation.Items;
                operation.NestedOperations = operation.NestedOperations.Add(new(command, operationItems.Snapshot));
                operationItems.Snapshot = operationItemsBackup;
            }
            else {
                // There was no operation, but it could be requested inside one of the nested commands
                operation = context.TryGetOperation();
                if (operation is not null) {
                    // The operation is requested inside the nested command, so we have to make a couple fixes:
                    // - Add a nested operation that corresponds to the current command
                    // - Replace its Items with an empty bag
                    var operationItems = operation.Items;
                    operation.NestedOperations = operation.NestedOperations.Add(new(command, operationItems.Snapshot));
                    operationItems.Snapshot = PropertyBag.Empty;
                }
            }
        }
    }
}
