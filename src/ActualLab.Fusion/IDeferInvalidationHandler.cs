namespace ActualLab.Fusion;

/// <summary>
/// Decides when a <see cref="DeferInvalidationScope"/>'s blocks are consumed,
/// and whether they are run or harvested.
/// </summary>
public interface IDeferInvalidationHandler
{
    public Task OnScopeExit(DeferInvalidationScope scope, Exception? error);
}

/// <summary>
/// The default <see cref="IDeferInvalidationHandler"/>: runs the collected blocks
/// at scope exit, unless the scope was discarded.
/// </summary>
public sealed class DeferInvalidationHandler : IDeferInvalidationHandler
{
    public static readonly DeferInvalidationHandler Instance = new();

    public Task OnScopeExit(DeferInvalidationScope scope, Exception? error)
        => error is null
            ? scope.Run(new InvalidationSource($"{nameof(DeferInvalidationScope)} exit"))
            : Task.CompletedTask;
}

/// <summary>
/// An <see cref="IDeferInvalidationHandler"/> that does nothing at scope exit -
/// used when something else (e.g. the Operations Framework) drives the consumption.
/// </summary>
public sealed class ExternallyDrivenDeferInvalidationHandler : IDeferInvalidationHandler
{
    public static readonly ExternallyDrivenDeferInvalidationHandler Instance = new();

    public Task OnScopeExit(DeferInvalidationScope scope, Exception? error)
        => Task.CompletedTask;
}
