namespace ActualLab.CommandR.Operations;

public interface IOperationScope : IAsyncDisposable
{
    public CommandContext CommandContext { get; }
    public Operation Operation { get; }
    public bool IsTransient { get; }
    public bool IsUsed { get; }
    public bool? IsCommitted { get; }
    public bool MustCreateOperation { get; set; }
    public bool CreatedOperation { get; }
    public bool CreatedEvents { get; }

    public Task Commit(CancellationToken cancellationToken = default);
}
