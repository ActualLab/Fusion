namespace ActualLab.CommandR.Internal;

public sealed record LocalActionCommand(Delegate? Handler) : LocalCommand, ILocalCommand<Unit>
{
    public override Task Run(CommandContext context, CancellationToken cancellationToken)
        => Handler switch {
            Func<CommandContext, CancellationToken, Task> fn => fn.Invoke(context, cancellationToken),
            Func<CancellationToken, Task> fn => fn.Invoke(cancellationToken),
            Func<Task> fn => fn.Invoke(),
            Action<CommandContext, CancellationToken> fn => TaskExt.FromResult(fn, context, cancellationToken),
            Action<CancellationToken> fn => TaskExt.FromResult(fn, cancellationToken),
            Action fn => TaskExt.FromResult(fn),
            _ => throw Errors.LocalCommandHasNoHandler(),
        };
}
