using System.Diagnostics.CodeAnalysis;
using ActualLab.Caching;

namespace ActualLab.CommandR;

public static class CommandContextExt
{
    extension(CommandContext context)
    {
        public Task<CommandContext> Run(CancellationToken cancellationToken = default)
            => context.Commander.Run(context, cancellationToken);

        public Task Call(CancellationToken cancellationToken = default)
            => GetTypedCallInvoker(context.ResultType).Invoke(context, cancellationToken);
    }

    extension<TResult>(CommandContext<TResult> context)
    {
        public async Task<TResult> Call(CancellationToken cancellationToken = default)
        {
            await context.Commander.Run(context, cancellationToken).ConfigureAwait(false);
            return await context.ResultTask.ConfigureAwait(false);
        }
    }

    // Private methods

    private static Func<CommandContext, CancellationToken, Task> GetTypedCallInvoker(Type commandResultType)
        => GenericInstanceCache.Get<Func<CommandContext, CancellationToken, Task>>(
            typeof(TypedCallFactory<>),
            commandResultType);

    // Nested types

    public sealed class TypedCallFactory<T> : GenericInstanceFactory, IGenericInstanceFactory<T>
    {
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public static Task<T> Call(CommandContext context, CancellationToken cancellationToken = default)
            => ((CommandContext<T>)context).Call(cancellationToken);

        [UnconditionalSuppressMessage("Trimming", "IL2060", Justification = "We assume Task<T> methods are preserved")]
        public override object Generate()
            => (Func<CommandContext, CancellationToken, Task>)Call;
    }
}
