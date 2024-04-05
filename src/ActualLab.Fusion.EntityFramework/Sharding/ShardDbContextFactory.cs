using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework;

public interface IShardDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    TDbContext CreateDbContext(DbShard shard);
    ValueTask<TDbContext> CreateDbContextAsync(DbShard shard, CancellationToken cancellationToken = default);
}

public delegate IDbContextFactory<TDbContext> ShardDbContextFactoryBuilder<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbContext>(
    IServiceProvider services, DbShard shard)
    where TDbContext : DbContext;

public class ShardDbContextFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbContext>
    : IShardDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    private readonly ConcurrentDictionary<
        DbShard,
        LazySlim<(ShardDbContextFactory<TDbContext> Self, DbShard Shard), IDbContextFactory<TDbContext>>>
        _factories = new();
    private ShardDbContextFactoryBuilder<TDbContext>? _shardDbContextBuilder;
    private IDbContextFactory<TDbContext>? _defaultDbContextFactory;

    protected IServiceProvider Services { get; }
    protected IDbShardRegistry<TDbContext> ShardRegistry { get; }
    protected ShardDbContextFactoryBuilder<TDbContext> ShardDbContextFactoryBuilder
        => _shardDbContextBuilder ??= Services.GetRequiredService<ShardDbContextFactoryBuilder<TDbContext>>();
    protected IDbContextFactory<TDbContext> DefaultDbContextFactory
        => _defaultDbContextFactory ??= Services.GetRequiredService<IDbContextFactory<TDbContext>>();
    protected bool HasSingleShard { get; }

    public ShardDbContextFactory(IServiceProvider services)
    {
        Services = services;
        ShardRegistry = services.GetRequiredService<IDbShardRegistry<TDbContext>>();
        HasSingleShard = ShardRegistry.HasSingleShard;
    }

    public TDbContext CreateDbContext(DbShard shard)
    {
        var factory = GetDbContextFactory(shard);
        return factory.CreateDbContext();
    }

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
    {
        if (!ShardRegistry.CanUse(shard))
            throw Internal.Errors.NoShard(shard);

        return HasSingleShard
            ? DefaultDbContextFactory
            : _factories.GetOrAdd(shard, static state => {
                var (self, shard1) = state;
                var factory = self.ShardDbContextFactoryBuilder.Invoke(self.Services, shard1);
                self.ShardRegistry.Use(shard1);
                return factory;
            }, (Self: this, Shard: shard));
    }
}
