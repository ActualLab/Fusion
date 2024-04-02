namespace ActualLab.CommandR.Internal;

public sealed record LocalActionCommand : LocalCommand, ICommand<Unit>
{
    public Func<CancellationToken, Task>? Handler { get; init; }

    public override Task Run(CancellationToken cancellationToken)
    {
        if (Handler == null)
            throw Errors.LocalCommandHasNoHandler();

        return Handler(cancellationToken);
    }
}
