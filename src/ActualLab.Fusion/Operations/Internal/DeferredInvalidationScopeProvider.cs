namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// Activates the <see cref="DeferredInvalidationContext"/> that
/// <see cref="TransientOperationScopeProvider"/> created for the command, and closes it on the way
/// out. This is the lowest Operations Framework filter, so every operation scope is already in
/// place by the time a handler can defer anything.
/// </summary>
public class DeferredInvalidationScopeProvider : ICommandHandler<ICommand>
{
    [CommandFilter(Priority = FusionOperationsCommandHandlerPriority.DeferredInvalidationScopeProvider)]
    public async Task OnCommand(ICommand command, CommandContext context, CancellationToken cancellationToken)
    {
        // Items are the outermost context's, so a nested command sees the same context - but one
        // context gets one scope, and the outermost command is the one that owns it
        var deferredInvalidationContext = context.IsOutermost
            ? context.OutermostContext.Items.KeylessGet<DeferredInvalidationContext>()
            : null;
        if (deferredInvalidationContext is null) {
            await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
            return;
        }

        // Activating here is what makes Defer(...) work from a handler's first statement
        using var _ = deferredInvalidationContext.Activate();
        await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
    }
}
