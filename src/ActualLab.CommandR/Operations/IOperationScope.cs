namespace ActualLab.CommandR.Operations;

public interface IOperationScope : IAsyncDisposable
{
    CommandContext CommandContext { get; }
    Operation Operation { get; }
    bool IsUsed { get; }
    bool IsClosed { get; }
    bool? IsConfirmed { get; }
    bool AllowsEvents { get; }

    Task Commit(CancellationToken cancellationToken = default);
    Task Rollback();
}
