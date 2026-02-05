namespace ActualLab.Fusion.Operations.Internal;

/// <summary>
/// A terminal command handler for <see cref="ICompletion"/> that completes the pipeline with no action.
/// </summary>
public class CompletionTerminator : ICommandHandler<ICompletion>
{
    [CommandHandler(Priority = FusionOperationsCommandHandlerPriority.CompletionTerminator, IsFilter = false)]
    public Task OnCommand(ICompletion completion, CommandContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
