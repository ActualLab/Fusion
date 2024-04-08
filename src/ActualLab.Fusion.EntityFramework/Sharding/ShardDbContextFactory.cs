using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework;

public interface IShardDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    TDbContext CreateDbContext(DbShard shard);
    ValueTask<TDbContext> CreateDbContextAsync(DbShard shard, CancellationToken cancellationToken = default);
}

// ReSharper disable once TypeParameterCanBeVariant
public delegate IDbContextFactory<TDbContext> ShardDbContextFactoryBuilder<TDbContext>(
    IServiceProvider services,
    DbShard shard)
    where TDbContext : DbContext;

public class ShardDbContextFactory<TDbContext> : IShardDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    private readonly ConcurrentDictionary<
        DbShard,
        LazySlim<DbShard, ShardDbContextFactory<TDbContext>, IDbContextFactory<TDbContext>>> _factories = new();
    private ShardDbContextFactoryBuilder<TDbContext>? _shardDbContextBuilder;

    protected IServiceProvider Services { get; }
    protected IDbShardRegistry<TDbContext> ShardRegistry { get; }
    protected ShardDbContextFactoryBuilder<TDbContext> ShardDbContextFactoryBuilder
        => _shardDbContextBuilder ??= Services.GetRequiredService<ShardDbContextFactoryBuilder<TDbContext>>();
    protected bool HasSingleShard { get; }

    public ShardDbContextFactory(IServiceProvider services)
    {
        Services = services;
        ShardRegistry = services.GetRequiredService<IDbShardRegistry<TDbContext>>();
        HasSingleShard = ShardRegistry.HasSingleShard;
    }

    public TDbContext CreateDbContext(DbShard shard)
        => GetDbContextFactory(shard).CreateDbContext();

    public ValueTask<TDbContext> CreateDbContextAsync(DbShard shard, CancellationToken cancellationToken = default)
    {
        var factory = GetDbContextFactory(shard);
#if NET6_0_OR_GREATER
        return factory.CreateDbContextAsync(cancellationToken).ToValueTask();
#else
        return new ValueTask<TDbContext>(factory.CreateDbContext());
#endif
    }

    // Protected methods

    protected virtual IDbContextFactory<TDbContext> GetDbContextFactory(DbShard shard)
        => _factories.GetOrAdd(shard, static (shard1, self) => {
            if (!self.ShardRegistry.CanUse(shard1))
                throw Internal.Errors.NoShard(shard1);

            var factory = self.ShardDbContextFactoryBuilder.Invoke(self.Services, shard1);
            self.ShardRegistry.Use(shard1);
            return factory;
        }, this);
}
