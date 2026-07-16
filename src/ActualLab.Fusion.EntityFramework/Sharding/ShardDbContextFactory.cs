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
#if NET9_0_OR_GREATER
    private readonly Lock _lock = new();
#else
    private readonly object _lock = new();
#endif
    private readonly ConcurrentDictionary<string, CacheEntry> _factories = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<Task, Unit> _disposeTasks = new();
    private TaskCompletionSource<Unit>? _whenDisposed;
    private int _isDisposed;

    protected IServiceProvider Services { get; }
    protected IDbShardRegistry<TDbContext> ShardRegistry { get; }
    protected ShardDbContextFactoryBuilder<TDbContext> ShardDbContextFactoryBuilder
        => field ??= Services.GetRequiredService<ShardDbContextFactoryBuilder<TDbContext>>();
    protected bool HasSingleShard { get; }
    private ILogger Log { get; }

    public ShardDbContextFactory(IServiceProvider services)
    {
        Services = services;
        ShardRegistry = services.GetRequiredService<IDbShardRegistry<TDbContext>>();
        HasSingleShard = ShardRegistry.HasSingleShard;
        Log = services.LogFor(GetType());
        ShardRegistry.Shards.Updated += OnShardsUpdated;
    }

    public void Dispose()
    {
        var (whenDisposed, mustDispose) = BeginDispose();
        if (!mustDispose)
            return;

        try {
            DisposeEntries().GetAwaiter().GetResult();
            whenDisposed.TrySetResult(default);
        }
        catch (Exception e) {
            whenDisposed.TrySetException(e);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        var (whenDisposed, mustDispose) = BeginDispose();
        if (!mustDispose) {
            await whenDisposed.Task.ConfigureAwait(false);
            return;
        }

        try {
            await DisposeEntries().ConfigureAwait(false);
            whenDisposed.TrySetResult(default);
        }
        catch (Exception e) {
            whenDisposed.TrySetException(e);
            throw;
        }
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

    private (TaskCompletionSource<Unit> WhenDisposed, bool MustDispose) BeginDispose()
    {
        var whenDisposed = TaskCompletionSourceExt.New<Unit>();
        var existingWhenDisposed = Interlocked.CompareExchange(ref _whenDisposed, whenDisposed, null);
        if (existingWhenDisposed is not null)
            return (existingWhenDisposed, false);

        Volatile.Write(ref _isDisposed, 1);
        ShardRegistry.Shards.Updated -= OnShardsUpdated;
        return (whenDisposed, true);
    }

    private IDbContextFactory<TDbContext> GetDbContextFactorySlow(string shard)
    {
        CacheEntry entry;
        lock (_lock) {
            if (Volatile.Read(ref _isDisposed) != 0)
                throw ActualLab.Internal.Errors.AlreadyDisposed<ShardDbContextFactory<TDbContext>>();

            entry = _factories.GetOrAdd(
                shard,
                static (shard1, self) => new CacheEntry(shard1, self),
                this);
        }
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
        lock (_lock) {
            if (Volatile.Read(ref _isDisposed) != 0)
                return;

            var shards = ShardRegistry.Shards.Value;
            foreach (var pair in _factories) {
                if (shards.Contains(pair.Key)
                    || !_factories.TryRemove(pair.Key, pair.Value))
                    continue;
                _ = TrackDisposal(pair.Value);
            }
        }
    }

    private Task DisposeEntries()
    {
        lock (_lock) {
            var tasks = new List<Task>(_disposeTasks.Count + _factories.Count);
            tasks.AddRange(_disposeTasks.Keys);
            foreach (var pair in _factories)
                if (_factories.TryRemove(pair.Key, pair.Value))
                    tasks.Add(TrackDisposal(pair.Value));
            return Task.WhenAll(tasks);
        }
    }

    private Task TrackDisposal(CacheEntry entry)
    {
        var task = Task.Run(async () => await entry.DisposeAsync().ConfigureAwait(false));
        _disposeTasks.TryAdd(task, default);
        _ = task.ContinueWith(
            static (completedTask, state) =>
                ((ShardDbContextFactory<TDbContext>)state!).OnDisposalCompleted(completedTask),
            this,
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
        return task;
    }

    private void OnDisposalCompleted(Task task)
    {
        _disposeTasks.TryRemove(task, out _);
        if (task.Exception is { } error)
            Log.LogError(error.GetBaseException(), "Per-shard service provider disposal failed.");
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
        private TaskCompletionSource<Unit>? _whenInitialized;
        private TaskCompletionSource<Unit>? _whenDisposed;
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
            var (value, whenInitialized, whenDisposed, mustDispose) = BeginDispose();
            if (!mustDispose)
                return;

            try {
                if (value is not null)
                    ShardDbContextFactory<TDbContext>.Dispose(value);
                if (whenInitialized is null)
                    whenDisposed.TrySetResult(default);
                else
                    _ = CompleteDispose(whenInitialized, whenDisposed);
            }
            catch (Exception e) {
                whenDisposed.TrySetException(e);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            var (value, whenInitialized, whenDisposed, mustDispose) = BeginDispose();
            if (!mustDispose) {
                await whenDisposed.Task.ConfigureAwait(false);
                return;
            }

            try {
                if (value is not null)
                    await ShardDbContextFactory<TDbContext>.DisposeAsync(value).ConfigureAwait(false);
                if (whenInitialized is not null)
                    await whenInitialized.ConfigureAwait(false);
                whenDisposed.TrySetResult(default);
            }
            catch (Exception e) {
                whenDisposed.TrySetException(e);
                throw;
            }
        }

        private IDbContextFactory<TDbContext> Initialize()
        {
            while (true) {
                TaskCompletionSource<Unit> whenInitialized;
                var mustInitialize = false;
                lock (_lock) {
                    if (_isDisposed != 0)
                        throw ActualLab.Internal.Errors.AlreadyDisposed<CacheEntry>();
                    if (_value is { } value)
                        return value;

                    if (_whenInitialized is { } existingWhenInitialized)
                        whenInitialized = existingWhenInitialized;
                    else {
                        whenInitialized = TaskCompletionSourceExt.New<Unit>();
                        _whenInitialized = whenInitialized;
                        mustInitialize = true;
                    }
                }
                if (mustInitialize)
                    return Initialize(whenInitialized);

                whenInitialized.Task.GetAwaiter().GetResult();
            }
        }

        private IDbContextFactory<TDbContext> Initialize(TaskCompletionSource<Unit> whenInitialized)
        {
            IDbContextFactory<TDbContext> value;
            try {
                value = owner.CreateDbContextFactory(shard);
            }
            catch {
                lock (_lock)
                    _whenInitialized = null;
                whenInitialized.TrySetResult(default);
                throw;
            }

            bool isDisposed;
            lock (_lock) {
                isDisposed = _isDisposed != 0;
                if (!isDisposed)
                    Volatile.Write(ref _value, value);
                _whenInitialized = null;
            }
            if (isDisposed) {
                try {
                    ShardDbContextFactory<TDbContext>.Dispose(value);
                }
                catch (Exception e) {
                    whenInitialized.TrySetException(e);
                    throw;
                }
                whenInitialized.TrySetResult(default);
                throw ActualLab.Internal.Errors.AlreadyDisposed<CacheEntry>();
            }
            whenInitialized.TrySetResult(default);
            return value;
        }

        private (
            IDbContextFactory<TDbContext>? Value,
            Task? WhenInitialized,
            TaskCompletionSource<Unit> WhenDisposed,
            bool MustDispose
            ) BeginDispose()
        {
            lock (_lock) {
                if (_whenDisposed is { } existingWhenDisposed)
                    return (null, null, existingWhenDisposed, false);

                Volatile.Write(ref _isDisposed, 1);
                var whenDisposed = TaskCompletionSourceExt.New<Unit>();
                _whenDisposed = whenDisposed;
                var value = _value;
                Volatile.Write(ref _value, null);
                return (value, _whenInitialized?.Task, whenDisposed, true);
            }
        }

        private static async Task CompleteDispose(Task whenInitialized, TaskCompletionSource<Unit> whenDisposed)
        {
            try {
                await whenInitialized.ConfigureAwait(false);
                whenDisposed.TrySetResult(default);
            }
            catch (Exception e) {
                whenDisposed.TrySetException(e);
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
        => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public ValueTask DisposeAsync()
        => Interlocked.Exchange(ref _isDisposed, 1) == 0
            ? serviceProvider.DisposeAsync()
            : default;
}
