namespace ActualLab.Fusion.EntityFramework.Operations;

public abstract class DbShardWatcher(DbShard shard) : ProcessorBase
{
    private volatile AsyncState<Unit> _state = new(default);

    public DbShard Shard { get; } = shard;
    public Task WhenChanged => _state.WhenNext();

    public abstract Task NotifyChanged(CancellationToken cancellationToken);

    protected void MarkChanged()
    {
        lock (Lock)
            _state = _state.SetNext(default);
    }
}
