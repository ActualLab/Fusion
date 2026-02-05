namespace ActualLab.CommandR.Internal;

/// <summary>
/// A <see cref="LocalCommand"/> that wraps a delegate returning a value of type
/// <typeparamref name="T"/>.
/// </summary>
public sealed record LocalFuncCommand<T>(Delegate? Handler) : LocalCommand, ILocalCommand<T>
{
    public override async Task Run(CommandContext context, CancellationToken cancellationToken)
    {
        var typedContext = context.Cast<T>();
        var task = Handler switch {
            Func<CommandContext, CancellationToken, Task<T>> fn => fn.Invoke(context, cancellationToken),
            Func<CancellationToken, Task<T>> fn => fn.Invoke(cancellationToken),
            Func<Task<T>> fn => fn.Invoke(),
            Func<CommandContext, CancellationToken, T> fn => TaskExt.FromResult(fn, context, cancellationToken),
            Func<CancellationToken, T> fn => TaskExt.FromResult(fn, cancellationToken),
            Func<T> fn => TaskExt.FromResult(fn),
            _ => throw Errors.LocalCommandHasNoHandler(),
        };
        var result = await task.ConfigureAwait(false);
        typedContext.SetResult(result);
    }
}
