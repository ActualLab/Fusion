using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework;

/// <summary>
/// Defines the contract for creating <see cref="DbContext"/> instances for a specific shard.
/// </summary>
public interface IShardDbContextFactory
{
    public DbContext CreateDbContext(string shard);
    public ValueTask<DbContext> CreateDbContextAsync(string shard, CancellationToken cancellationToken = default);
}

/// <summary>
/// A typed <see cref="IShardDbContextFactory"/> that creates strongly-typed
/// <typeparamref name="TDbContext"/> instances for a specific shard.
/// </summary>
public interface IShardDbContextFactory<TDbContext> : IShardDbContextFactory
    where TDbContext : DbContext
{
    public new TDbContext CreateDbContext(string shard);
    public new ValueTask<TDbContext> CreateDbContextAsync(string shard, CancellationToken cancellationToken = default);
}

/// <summary>
/// A delegate that builds an <see cref="IDbContextFactory{TDbContext}"/> for a given shard.
/// </summary>
// ReSharper disable once TypeParameterCanBeVariant
public delegate IDbContextFactory<TDbContext> ShardDbContextFactoryBuilder<TDbContext>(
    IServiceProvider services,
    string shard)
    where TDbContext : DbContext;

/// <summary>
/// Default <see cref="IShardDbContextFactory{TDbContext}"/> implementation that caches
/// per-shard <see cref="IDbContextFactory{TDbContext}"/> instances built by a
/// <see cref="ShardDbContextFactoryBuilder{TDbContext}"/>.
/// </summary>
public class ShardDbContextFactory<TDbContext> : IShardDbContextFactory<TDbContext>, IDisposable, IAsyncDisposable
    where TDbContext : DbContext
{
    private readonly ConcurrentDictionary<string, CacheEntry> _factories = new(StringComparer.Ordinal);
    private int _isDisposed;

    protected IServiceProvider Services { get; }
    protected IDbShardRegistry<TDbContext> ShardRegistry { get; }
    protected ShardDbContextFactoryBuilder<TDbContext> ShardDbContextFactoryBuilder
        => field ??= Services.GetRequiredService<ShardDbContextFactoryBuilder<TDbContext>>();
    protected bool HasSingleShard { get; }

    public ShardDbContextFactory(IServiceProvider services)
    {
        Services = services;
        ShardRegistry = services.GetRequiredService<IDbShardRegistry<TDbContext>>();
        HasSingleShard = ShardRegistry.HasSingleShard;
        ShardRegistry.Shards.Updated += OnShardsUpdated;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        ShardRegistry.Shards.Updated -= OnShardsUpdated;
        foreach (var pair in _factories)
            if (_factories.TryRemove(pair.Key, pair.Value))
                pair.Value.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            return;

        ShardRegistry.Shards.Updated -= OnShardsUpdated;
        foreach (var pair in _factories)
            if (_factories.TryRemove(pair.Key, pair.Value))
                await pair.Value.DisposeAsync().ConfigureAwait(false);
    }

    public TDbContext CreateDbContext(string shard)
        => GetDbContextFactory(shard).CreateDbContext();

    public ValueTask<TDbContext> CreateDbContextAsync(string shard, CancellationToken cancellationToken = default)
    {
        var factory = GetDbContextFactory(shard);
#if NET6_0_OR_GREATER
        return factory.CreateDbContextAsync(cancellationToken).ToValueTask();
#else
        return new ValueTask<TDbContext>(factory.CreateDbContext());
#endif
    }

    // Explicit interface implementations

    DbContext IShardDbContextFactory.CreateDbContext(string shard)
        => CreateDbContext(shard);
    async ValueTask<DbContext> IShardDbContextFactory.CreateDbContextAsync(string shard,
        CancellationToken cancellationToken)
        => await CreateDbContextAsync(shard, cancellationToken).ConfigureAwait(false);

    // Protected methods

    protected virtual IDbContextFactory<TDbContext> GetDbContextFactory(string shard)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
            throw ActualLab.Internal.Errors.AlreadyDisposed<ShardDbContextFactory<TDbContext>>();

        return _factories.TryGetValue(shard, out var entry)
            ? entry.Value
            : GetDbContextFactorySlow(shard);
    }

    // Private methods

    private IDbContextFactory<TDbContext> GetDbContextFactorySlow(string shard)
    {
        if (Volatile.Read(ref _isDisposed) != 0)
            throw ActualLab.Internal.Errors.AlreadyDisposed<ShardDbContextFactory<TDbContext>>();

        var entry = _factories.GetOrAdd(
            shard,
            static (shard1, self) => new CacheEntry(shard1, self),
            this);
        if (Volatile.Read(ref _isDisposed) == 0)
            return entry.Value;

        Remove(shard, entry);
        throw ActualLab.Internal.Errors.AlreadyDisposed<ShardDbContextFactory<TDbContext>>();
    }

    private IDbContextFactory<TDbContext> CreateDbContextFactory(string shard)
    {
        if (!ShardRegistry.CanUse(shard))
            throw Internal.Errors.NoShard(shard);

        var factory = ShardDbContextFactoryBuilder.Invoke(Services, shard);
        try {
            ShardRegistry.Use(shard);
            return factory;
        }
        catch {
            Dispose(factory);
            throw;
        }
    }

    private void OnShardsUpdated(State state, StateEventKind eventKind)
    {
        var shards = ShardRegistry.Shards.Value;
        foreach (var pair in _factories) {
            if (shards.Contains(pair.Key)
                || !_factories.TryRemove(pair.Key, pair.Value))
                continue;
            pair.Value.Dispose();
        }
    }

    private void Remove(string shard, CacheEntry entry)
    {
        if (_factories.TryRemove(shard, entry))
            entry.Dispose();
    }

    private static void Dispose(IDbContextFactory<TDbContext> factory)
    {
        if (factory is ShardDbContextFactoryEntry<TDbContext> entry)
            entry.Dispose();
    }

    private static ValueTask DisposeAsync(IDbContextFactory<TDbContext> factory)
        => factory is ShardDbContextFactoryEntry<TDbContext> entry
            ? entry.DisposeAsync()
            : default;

    // Nested types

    private sealed class CacheEntry(string shard, ShardDbContextFactory<TDbContext> owner)
        : IDisposable, IAsyncDisposable
    {
#if NET9_0_OR_GREATER
        private readonly Lock _lock = new();
#else
        private readonly object _lock = new();
#endif
        private IDbContextFactory<TDbContext>? _value;
        private int _isDisposed;

        public IDbContextFactory<TDbContext> Value {
            get {
                if (Volatile.Read(ref _isDisposed) != 0)
                    throw ActualLab.Internal.Errors.AlreadyDisposed<CacheEntry>();

                return Volatile.Read(ref _value) ?? Initialize();
            }
        }

        public void Dispose()
        {
            IDbContextFactory<TDbContext>? value;
            lock (_lock) {
                if (_isDisposed != 0)
                    return;

                _isDisposed = 1;
                value = _value;
            }
            if (value is not null)
                ShardDbContextFactory<TDbContext>.Dispose(value);
        }

        public ValueTask DisposeAsync()
        {
            IDbContextFactory<TDbContext>? value;
            lock (_lock) {
                if (_isDisposed != 0)
                    return default;

                _isDisposed = 1;
                value = _value;
            }
            return value is not null
                ? ShardDbContextFactory<TDbContext>.DisposeAsync(value)
                : default;
        }

        private IDbContextFactory<TDbContext> Initialize()
        {
            lock (_lock) {
                if (_isDisposed != 0)
                    throw ActualLab.Internal.Errors.AlreadyDisposed<CacheEntry>();

                return _value ??= owner.CreateDbContextFactory(shard);
            }
        }
    }
}

internal sealed class ShardDbContextFactoryEntry<TDbContext>(
    IDbContextFactory<TDbContext> factory,
    ServiceProvider serviceProvider)
    : IDbContextFactory<TDbContext>, IDisposable, IAsyncDisposable
    where TDbContext : DbContext
{
    private int _isDisposed;

    public TDbContext CreateDbContext()
        => factory.CreateDbContext();

#if NET6_0_OR_GREATER
    public Task<TDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        => factory.CreateDbContextAsync(cancellationToken);
#endif

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) == 0)
            serviceProvider.Dispose();
    }

    public ValueTask DisposeAsync()
        => Interlocked.Exchange(ref _isDisposed, 1) == 0
            ? serviceProvider.DisposeAsync()
            : default;
}
