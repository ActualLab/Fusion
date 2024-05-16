namespace ActualLab.Fusion.EntityFramework;

public interface IDbShardRegistry
{
    bool HasSingleShard { get; }
    IState<ImmutableHashSet<DbShard>> Shards { get; }
    IState<ImmutableHashSet<DbShard>> UsedShards { get; }
    IState<ImmutableHashSet<DbShard>> EventProcessorShards { get; }
    MutableState<Func<DbShard, bool>> EventProcessorShardFilter { get; }

    bool Add(DbShard shard);
    bool Remove(DbShard shard);

    DbShard Use(DbShard shard);
    bool CanUse(DbShard shard);
    bool TryUse(DbShard shard);
}

public interface IDbShardRegistry<TContext> : IDbShardRegistry;

public class DbShardRegistry<TContext> : IDbShardRegistry<TContext>, IDisposable
{
    protected readonly object Lock = new();
    private readonly MutableState<ImmutableHashSet<DbShard>> _shards;
    private readonly MutableState<ImmutableHashSet<DbShard>> _usedShards;
    private readonly IComputedState<ImmutableHashSet<DbShard>> _eventProcessorShards;

    public bool HasSingleShard { get; }
    public IState<ImmutableHashSet<DbShard>> Shards => _shards;
    public IState<ImmutableHashSet<DbShard>> UsedShards => _usedShards;
    public IState<ImmutableHashSet<DbShard>> EventProcessorShards => _eventProcessorShards;
    public MutableState<Func<DbShard, bool>> EventProcessorShardFilter { get; }

    public DbShardRegistry(IServiceProvider services, params DbShard[] initialShards)
    {
        if (initialShards.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(initialShards), "Initial shard set is empty.");

        var shards = initialShards.ToImmutableHashSet();
        HasSingleShard = shards.Contains(DbShard.None);
        if (HasSingleShard) {
            if (shards.Count != 1)
                throw new ArgumentOutOfRangeException(nameof(initialShards),
                    $"Initial shard set containing {nameof(DbShard)}.{nameof(DbShard.None)} should contain just it.");
        }
        else
            shards = shards.Add(DbShard.Template);

        var stateFactory = services.StateFactory();
        EventProcessorShardFilter = stateFactory.NewMutable<Func<DbShard, bool>>(_ => true,
            StateCategories.Get(GetType(), nameof(EventProcessorShardFilter)));
        _shards = stateFactory.NewMutable(shards,
            StateCategories.Get(GetType(), nameof(Shards)));
        _usedShards = stateFactory.NewMutable(ImmutableHashSet<DbShard>.Empty,
            StateCategories.Get(GetType(), nameof(UsedShards)));
        _eventProcessorShards = stateFactory.NewComputed<ImmutableHashSet<DbShard>>(
            FixedDelayer.NoneUnsafe,
            async (_, ct) => {
                var filter = await EventProcessorShardFilter.Use(ct).ConfigureAwait(false);
                var shards1 = await Shards.Use(ct).ConfigureAwait(false);
                return shards1.Where(shard => filter.Invoke(shard) && !shard.IsTemplate).ToImmutableHashSet();
            },
            StateCategories.Get(GetType(), nameof(EventProcessorShards)));
    }

    public void Dispose()
        => _eventProcessorShards.Dispose();

    public bool Add(DbShard shard)
    {
        if (!shard.IsValid())
            return false;

        lock (Lock) {
            var shards = _shards.Value.Add(shard);
            if (_shards.Value == shards)
                return false;

            _shards.Value = shards;
            return true;
        }
    }

    public bool Remove(DbShard shard)
    {
        if (!shard.IsValid())
            return false;

        lock (Lock) {
            var shards = _shards.Value.Remove(shard);
            if (_shards.Value == shards)
                return false;

            _shards.Value = shards;
            _usedShards.Value = _usedShards.Value.Remove(shard);
            return true;
        }
    }

    public DbShard Use(DbShard shard)
    {
        return TryUse(shard) ? shard
            : throw Internal.Errors.NoShard(shard);
    }

    public bool CanUse(DbShard shard)
    {
        if (HasSingleShard) {
            if (!shard.IsNone)
                return false;
        }
        else {
            if (!shard.IsValidOrTemplate())
                return false;
        }
        return Shards.Value.Contains(shard);
    }

    public bool TryUse(DbShard shard)
    {
        if (!CanUse(shard))
            return false;

        // Double-check locking
        if (_usedShards.Value.Contains(shard))
            return true;
        lock (Lock) {
            var usedShards = _usedShards.Value;
            if (usedShards.Contains(shard))
                return true;

            if (!CanUse(shard))
                return false;

            _usedShards.Value = usedShards.Add(shard);
        }
        return true;
    }
}
