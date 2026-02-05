namespace ActualLab.CommandR;

/// <summary>
/// Extension methods for <see cref="ICommander"/> providing convenience overloads
/// for starting, running, and calling commands.
/// </summary>
public static class CommanderExt
{
    // Start

    public static CommandContext Start(this ICommander commander, ICommand command, CancellationToken cancellationToken = default)
        => commander.Start(command, isOutermost: false, cancellationToken);

    public static CommandContext Start(this ICommander commander, ICommand command, bool isOutermost, CancellationToken cancellationToken = default)
    {
        var context = CommandContext.New(commander, command, isOutermost);
        _ = context.Run(cancellationToken);
        return context;
    }

    // Run

    public static Task<CommandContext> Run(this ICommander commander, ICommand command, CancellationToken cancellationToken = default)
        => CommandContext.New(commander, command, isOutermost: false).Run(cancellationToken);

    public static Task<CommandContext> Run(this ICommander commander, ICommand command, bool isOutermost, CancellationToken cancellationToken = default)
        => CommandContext.New(commander, command, isOutermost).Run(cancellationToken);

    // Call

    public static Task Call(this ICommander commander, ICommand command, CancellationToken cancellationToken = default)
        => CommandContext.New(commander, command, isOutermost: false).Call(cancellationToken);

    public static Task Call(this ICommander commander, ICommand command, bool isOutermost, CancellationToken cancellationToken = default)
        => CommandContext.New(commander, command, isOutermost).Call(cancellationToken);

    // Typed Call

    public static Task<TResult> Call<TResult>(this ICommander commander, ICommand<TResult> command, CancellationToken cancellationToken = default)
        => CommandContext.New(commander, command, isOutermost: false).Call(cancellationToken);

    public static Task<TResult> Call<TResult>(this ICommander commander, ICommand<TResult> command, bool isOutermost, CancellationToken cancellationToken = default)
        => CommandContext.New(commander, command, isOutermost).Call(cancellationToken);
}
