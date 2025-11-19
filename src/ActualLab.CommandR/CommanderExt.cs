namespace ActualLab.CommandR;

public static class CommanderExt
{
    extension(ICommander commander)
    {
        // Start overloads

        public CommandContext Start(ICommand command, CancellationToken cancellationToken = default)
            => commander.Start(command, isOutermost: false, cancellationToken);

        public CommandContext Start(ICommand command, bool isOutermost, CancellationToken cancellationToken = default)
        {
            var context = CommandContext.New(commander, command, isOutermost);
            _ = context.Run(cancellationToken);
            return context;
        }

        // Run overloads

        public Task<CommandContext> Run(ICommand command, CancellationToken cancellationToken = default)
            => CommandContext.New(commander, command, isOutermost: false).Run(cancellationToken);

        public Task<CommandContext> Run(ICommand command, bool isOutermost, CancellationToken cancellationToken = default)
            => CommandContext.New(commander, command, isOutermost).Run(cancellationToken);

        // Typed call overloads

        public Task<TResult> Call<TResult>(ICommand<TResult> command, CancellationToken cancellationToken = default)
            => CommandContext.New(commander, command, isOutermost: false).Call(cancellationToken);

        public Task<TResult> Call<TResult>(ICommand<TResult> command, bool isOutermost, CancellationToken cancellationToken = default)
            => CommandContext.New(commander, command, isOutermost).Call(cancellationToken);

        // Untyped call overloads

        public Task Call(ICommand command, CancellationToken cancellationToken = default)
            => CommandContext.New(commander, command, isOutermost: false).Call(cancellationToken);

        public Task Call(ICommand command, bool isOutermost, CancellationToken cancellationToken = default)
            => CommandContext.New(commander, command, isOutermost).Call(cancellationToken);
    }
}
