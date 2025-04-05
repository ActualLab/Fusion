namespace ActualLab.Fusion.EntityFramework;

public interface IDbShardRegistry
{
    public bool HasSingleShard { get; }
    public IState<ImmutableHashSet<string>> Shards { get; }
    public IState<ImmutableHashSet<string>> UsedShards { get; }
    public IState<ImmutableHashSet<string>> EventProcessorShards { get; }
    public MutableState<Func<string, bool>> EventProcessorShardFilter { get; }

    public bool Add(string shard);
    public bool Remove(string shard);

    public string Use(string shard);
    public bool CanUse(string shard);
    public bool TryUse(string shard);
}

public interface IDbShardRegistry<TContext> : IDbShardRegistry;

public class DbShardRegistry<TContext> : IDbShardRegistry<TContext>, IDisposable
{
    protected readonly object Lock = new();
    private readonly MutableState<ImmutableHashSet<string>> _shards;
    private readonly MutableState<ImmutableHashSet<string>> _usedShards;
    private readonly ComputedState<ImmutableHashSet<string>> _eventProcessorShards;

    public bool HasSingleShard { get; }
    public IState<ImmutableHashSet<string>> Shards => _shards;
    public IState<ImmutableHashSet<string>> UsedShards => _usedShards;
    public IState<ImmutableHashSet<string>> EventProcessorShards => _eventProcessorShards;
    public MutableState<Func<string, bool>> EventProcessorShardFilter { get; }

    public DbShardRegistry(IServiceProvider services, params string[] initialShards)
    {
        if (initialShards.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(initialShards), "Initial shard set is empty.");

        var shards = initialShards.ToImmutableHashSet();
        HasSingleShard = shards.Contains(DbShard.Single);
        if (HasSingleShard) {
            if (shards.Count != 1)
                throw new ArgumentOutOfRangeException(nameof(initialShards),
                    $"Initial shard set containing {nameof(DbShard)}.{nameof(DbShard.Single)} should contain just it.");
        }
        else
            shards = shards.Add(DbShard.Template);

        var stateFactory = services.StateFactory();
        EventProcessorShardFilter = stateFactory.NewMutable<Func<string, bool>>(_ => true,
            StateCategories.Get(GetType(), nameof(EventProcessorShardFilter)));
        _shards = stateFactory.NewMutable(shards,
            StateCategories.Get(GetType(), nameof(Shards)));
        _usedShards = stateFactory.NewMutable(ImmutableHashSet<string>.Empty,
            StateCategories.Get(GetType(), nameof(UsedShards)));
        _eventProcessorShards = stateFactory.NewComputed<ImmutableHashSet<string>>(
            FixedDelayer.NoneUnsafe,
            async ct => {
                var filter = await EventProcessorShardFilter.Use(ct).ConfigureAwait(false);
                var shards1 = await Shards.Use(ct).ConfigureAwait(false);
                return shards1.Where(shard => filter.Invoke(shard) && !DbShard.IsTemplate(shard)).ToImmutableHashSet();
            },
            StateCategories.Get(GetType(), nameof(EventProcessorShards)));
    }

    public void Dispose()
        => _eventProcessorShards.Dispose();

    public bool Add(string shard)
    {
        if (!DbShard.IsValid(shard))
            return false;

        lock (Lock) {
            var shards = _shards.Value.Add(shard);
            if (_shards.Value == shards)
                return false;

            _shards.Value = shards;
            return true;
        }
    }

    public bool Remove(string shard)
    {
        if (!DbShard.IsValid(shard))
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

    public string Use(string shard)
    {
        return TryUse(shard) ? shard
            : throw Internal.Errors.NoShard(shard);
    }

    public bool CanUse(string shard)
    {
        if (HasSingleShard) {
            if (!DbShard.IsSingle(shard))
                return false;
        }
        else {
            if (!DbShard.IsValidOrTemplate(shard))
                return false;
        }
        return Shards.Value.Contains(shard);
    }

    public bool TryUse(string shard)
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
