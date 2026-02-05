namespace ActualLab.Fusion.EntityFramework.Operations;

/// <summary>
/// Watches a single database shard for changes and exposes a <see cref="WhenChanged"/>
/// task that completes when the shard's log is updated.
/// </summary>
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
