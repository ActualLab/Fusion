using System.Diagnostics.CodeAnalysis;
using ActualLab.Caching;

namespace ActualLab.CommandR;

public static class CommanderExt
{
    extension(ICommander commander)
    {
        // Start overloads

        public CommandContext Start(ICommand command, CancellationToken cancellationToken = default)
            => commander.Start(command, false, cancellationToken);

        public CommandContext Start(ICommand command, bool isOutermost, CancellationToken cancellationToken = default)
        {
            var context = CommandContext.New(commander, command, isOutermost);
            _ = commander.Run(context, cancellationToken);
            return context;
        }

        // Run overloads

        public Task<CommandContext> Run(ICommand command, CancellationToken cancellationToken = default)
            => commander.Run(command, false, cancellationToken);

        public async Task<CommandContext> Run(ICommand command, bool isOutermost, CancellationToken cancellationToken = default)
        {
            var context = CommandContext.New(commander, command, isOutermost);
            await commander.Run(context, cancellationToken).ConfigureAwait(false);
            return context;
        }

        // Call overloads

        public Task<TResult> Call<TResult>(
            ICommand<TResult> command, bool isOutermost,
            CancellationToken cancellationToken = default)
        {
            var context = CommandContext.New(commander, command, isOutermost);
            return TypedCallFactory<TResult>.TypedCall(commander, context, cancellationToken);
        }

        public Task Call(
            ICommand command, bool isOutermost,
            CancellationToken cancellationToken = default)
        {
            var context = CommandContext.New(commander, command, isOutermost);
            return GetTypedCallInvoker(command.GetResultType()).Invoke(commander, context, cancellationToken);
        }

        public Task<TResult> Call<TResult>(
            ICommand<TResult> command,
            CancellationToken cancellationToken = default)
        {
            var context = CommandContext.New(commander, command, isOutermost: false);
            return TypedCallFactory<TResult>.TypedCall(commander, context, cancellationToken);
        }

        public Task Call(ICommand command, CancellationToken cancellationToken = default)
        {
            var context = CommandContext.New(commander, command, isOutermost: false);
            return GetTypedCallInvoker(command.GetResultType()).Invoke(commander, context, cancellationToken);
        }
    }

    public static Func<ICommander, CommandContext, CancellationToken, Task> GetTypedCallInvoker(Type commandResultType)
        => GenericInstanceCache
            .Get<Func<ICommander, CommandContext, CancellationToken, Task>>(
                typeof(TypedCallFactory<>),
                commandResultType);

    // Nested types

    public sealed class TypedCallFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        public static async Task<T> TypedCall(
            ICommander commander,
            CommandContext context,
            CancellationToken cancellationToken = default)
        {
            await commander.Run(context, cancellationToken).ConfigureAwait(false);
            var typedContext = (CommandContext<T>)context;
            return await typedContext.ResultSource.Task.ConfigureAwait(false);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume Task<T> methods are preserved")]
        public override object Generate()
            => (Func<ICommander, CommandContext, CancellationToken, Task>)TypedCall;
    }
}
