namespace ActualLab.Fusion;

/// <summary>
/// A disposable handle of a <see cref="DeferInvalidationScope"/>: restores the ambient
/// scope and lets the scope's handler consume the collected blocks.
/// </summary>
public readonly struct DeferInvalidationScopeHandle : IDisposable, IAsyncDisposable
{
    private readonly DeferInvalidationScope? _oldScope;

    public readonly DeferInvalidationScope? Scope;

    internal DeferInvalidationScopeHandle(DeferInvalidationScope? scope, DeferInvalidationScope? oldScope)
    {
        Scope = scope;
        _oldScope = oldScope;
    }

    public void Dispose()
    {
        // IDeferInvalidationHandler.OnScopeExit never throws - invalidation blocks report their own
        // failures - so a block that didn't complete synchronously can be left to finish on its own.
        var whenExited = Exit(null);
        if (whenExited.IsCompleted)
            whenExited.GetAwaiter().GetResult();
    }

    public ValueTask DisposeAsync()
        => new(Exit(null));

    public ValueTask DisposeAsync(Exception? error)
        => new(Exit(error));

    // Private methods

    private Task Exit(Exception? error)
    {
        if (Scope is not { } scope)
            return Task.CompletedTask;

        DeferInvalidationScope.SetCurrent(_oldScope);
        return scope.Handler.OnScopeExit(scope, error);
    }
}
