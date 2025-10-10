using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR.Operations;

public interface IOperationScope : IAsyncDisposable
{
    public CommandContext CommandContext { get; }
    public Operation Operation { get; }
    public bool IsTransient { get; }
    public bool IsUsed { get; }
    public bool? IsCommitted { get; }
    public bool MustStoreOperation { get; set; }
    public bool HasStoredOperation { get; }
    public bool HasStoredEvents { get; }
    public ImmutableList<Func<IOperationScope, Task>> CompletionHandlers { get; set; }

    public Task Commit(CancellationToken cancellationToken = default);
}

public static class OperationScopeExt
{
    public static IOperationScope RequireActive(this IOperationScope? operationScope)
        => operationScope is { IsUsed: true, IsCommitted: null }
            ? operationScope
            : throw Errors.ActiveOperationRequired();
}
