namespace ActualLab.CommandR.Operations;

public interface IOperationScope : IAsyncDisposable
{
    CommandContext CommandContext { get; }
    Operation Operation { get; }
    bool IsTransient { get; }
    bool IsUsed { get; }
    bool? IsCommitted { get; }

    Task Commit(CancellationToken cancellationToken = default);
}
