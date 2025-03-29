using System.Diagnostics.CodeAnalysis;
using ActualLab.Caching;

namespace ActualLab.CommandR;

public static class CommanderExt
{
    // Start overloads

    public static CommandContext Start(this ICommander commander,
        ICommand command, CancellationToken cancellationToken = default)
        => commander.Start(command, false, cancellationToken);

    public static CommandContext Start(this ICommander commander,
        ICommand command, bool isOutermost, CancellationToken cancellationToken = default)
    {
        var context = CommandContext.New(commander, command, isOutermost);
        _ = commander.Run(context, cancellationToken);
        return context;
    }

    // Run overloads

    public static Task<CommandContext> Run(this ICommander commander,
        ICommand command, CancellationToken cancellationToken = default)
        => commander.Run(command, false, cancellationToken);

    public static async Task<CommandContext> Run(this ICommander commander,
        ICommand command, bool isOutermost, CancellationToken cancellationToken = default)
    {
        var context = CommandContext.New(commander, command, isOutermost);
        await commander.Run(context, cancellationToken).ConfigureAwait(false);
        return context;
    }

    // Call overloads

    public static Task<TResult> Call<TResult>(this ICommander commander,
        ICommand<TResult> command, bool isOutermost, CancellationToken cancellationToken = default)
        => TypedCallFactory<TResult>.TypedCall(commander, command, isOutermost, cancellationToken);

    public static Task Call(this ICommander commander,
        ICommand command, bool isOutermost, CancellationToken cancellationToken = default)
        => GetTypedCallInvoker(command.GetResultType())
            .Invoke(commander, command, isOutermost, cancellationToken);

    public static Task<TResult> Call<TResult>(this ICommander commander,
        ICommand<TResult> command,
        CancellationToken cancellationToken = default)
        => TypedCallFactory<TResult>.TypedCall(commander, command, false, cancellationToken);

    public static Task Call(this ICommander commander,
        ICommand command,
        CancellationToken cancellationToken = default)
        => GetTypedCallInvoker(command.GetResultType())
            .Invoke(commander, command, false, cancellationToken);

    public static Func<ICommander, ICommand, bool, CancellationToken, Task> GetTypedCallInvoker(Type commandResultType)
        => GenericInstanceCache
            .Get<Func<ICommander, ICommand, bool, CancellationToken, Task>>(
                typeof(TypedCallFactory<>),
                commandResultType);

    // Nested types

    public sealed class TypedCallFactory<TResult> : GenericInstanceFactory, IGenericInstanceFactory<TResult>
    {
        public static async Task<TResult> TypedCall(
            ICommander commander,
            ICommand command,
            bool isOutermost,
            CancellationToken cancellationToken = default)
        {
            var context = await commander.Run(command, isOutermost, cancellationToken).ConfigureAwait(false);
            var typedContext = (CommandContext<TResult>)context;
            return await typedContext.ResultSource.Task.ConfigureAwait(false);
        }

        [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume Task<T> methods are preserved")]
        public override Func<ICommander, ICommand, bool, CancellationToken, Task> Generate()
            => TypedCall;
    }
}
