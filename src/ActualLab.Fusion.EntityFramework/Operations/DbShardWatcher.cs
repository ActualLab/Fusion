namespace ActualLab.Fusion.EntityFramework.Operations;

public abstract class DbShardWatcher(string shard) : ProcessorBase
{
    private volatile AsyncState<Unit> _state = new(default);

    public string Shard { get; } = shard;
    public Task WhenChanged => _state.WhenNext();

    public abstract Task NotifyChanged(CancellationToken cancellationToken);

    protected void MarkChanged()
    {
        lock (Lock)
            _state = _state.SetNext(default);
    }
}
