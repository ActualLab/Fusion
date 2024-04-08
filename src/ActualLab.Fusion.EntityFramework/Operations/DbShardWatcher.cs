namespace ActualLab.Fusion.EntityFramework.Operations;

public abstract class DbShardWatcher(DbShard shard) : ProcessorBase
{
    private volatile AsyncState<Unit> _state = new(default, true);

    public DbShard Shard { get; } = shard;
    public Task WhenChanged => _state.WhenNext();

    protected void MarkChanged()
    {
        lock (Lock)
            _state = _state.SetNext(default);
    }
}
