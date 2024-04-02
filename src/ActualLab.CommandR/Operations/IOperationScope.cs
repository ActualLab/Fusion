namespace ActualLab.CommandR.Operations;

public interface IOperationScope : IAsyncDisposable, IRequirementTarget
{
    CommandContext CommandContext { get; }
    Operation Operation { get; }
    bool IsUsed { get; }
    bool IsClosed { get; }
    bool? IsConfirmed { get; }

    Task Commit(CancellationToken cancellationToken = default);
    Task Rollback();
}
