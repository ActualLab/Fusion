using ActualLab.Caching;

namespace ActualLab.CommandR;

/// <summary>
/// Extension methods for <see cref="CommandContext"/>.
/// </summary>
public static class CommandContextExt
{
    public static Task<CommandContext> Run(this CommandContext context, CancellationToken cancellationToken = default)
        => context.Commander.Run(context, cancellationToken);

    public static Task Call(this CommandContext context, CancellationToken cancellationToken = default)
        => GetTypedCallInvoker(context.ResultType).Invoke(context, cancellationToken);

    public static async Task<TResult> Call<TResult>(
        this CommandContext<TResult> context, CancellationToken cancellationToken = default)
    {
        await context.Commander.Run(context, cancellationToken).ConfigureAwait(false);
        return await context.ResultTask.ConfigureAwait(false);
    }

    // Private methods

    private static Func<CommandContext, CancellationToken, Task> GetTypedCallInvoker(Type commandResultType)
        => GenericInstanceCache.GetUnsafe<Func<CommandContext, CancellationToken, Task>>(
            typeof(TypedCallFactory<>),
            commandResultType);

    // Nested types

    /// <summary>
    /// Generic factory that produces a typed Call delegate for a given result type.
    /// </summary>
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
