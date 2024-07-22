namespace ActualLab.Fusion.Operations.Internal;

public class CompletionTerminator : ICommandHandler<ICompletion>
{
    [CommandHandler(Priority = FusionOperationsCommandHandlerPriority.CompletionTerminator, IsFilter = false)]
    public Task OnCommand(ICompletion completion, CommandContext context, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
