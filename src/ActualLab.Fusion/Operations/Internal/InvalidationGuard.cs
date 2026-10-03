namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// Rejects any command that's about to run while an invalidation or capture pass is active.
/// Nothing replays a command handler anymore, so such a command is always a bug - and a quiet one,
/// since every operation scope provider down the chain opts out while a pass is active.
/// </summary>
public sealed class InvalidationGuard : ICommandHandler<ICommand>
{
    [CommandFilter(Priority = FusionOperationsCommandHandlerPriority.InvalidationGuard)]
    public Task OnCommand(ICommand command, CommandContext context, CancellationToken cancellationToken)
        // A capture pass counts: a Replicated or Distributed handler's blocks run under one, and a
        // command started from there would otherwise run for real inside the open transaction.
        // Only a nested command gets here - Commander suppresses an outermost one's context flow.
        => Invalidation.IsActive || Invalidation.IsCapturing
            ? throw Fusion.Internal.Errors.CommandCannotRunDuringInvalidation(command)
            : context.InvokeRemainingHandlers(cancellationToken);
}
