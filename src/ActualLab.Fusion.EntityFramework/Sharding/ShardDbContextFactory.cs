using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework;

public interface IShardDbContextFactory
{
    public DbContext CreateDbContext(string shard);
    public ValueTask<DbContext> CreateDbContextAsync(string shard, CancellationToken cancellationToken = default);
}

public interface IShardDbContextFactory<TDbContext> : IShardDbContextFactory
    where TDbContext : DbContext
{
    public new TDbContext CreateDbContext(string shard);
    public new ValueTask<TDbContext> CreateDbContextAsync(string shard, CancellationToken cancellationToken = default);
}

// ReSharper disable once TypeParameterCanBeVariant
public delegate IDbContextFactory<TDbContext> ShardDbContextFactoryBuilder<TDbContext>(
    IServiceProvider services,
    string shard)
    where TDbContext : DbContext;

public class ShardDbContextFactory<TDbContext> : IShardDbContextFactory<TDbContext>
    where TDbContext : DbContext
{
    private readonly ConcurrentDictionary<
        string,
        LazySlim<string, ShardDbContextFactory<TDbContext>, IDbContextFactory<TDbContext>>> _factories
        = new(StringComparer.Ordinal);

    protected IServiceProvider Services { get; }
    protected IDbShardRegistry<TDbContext> ShardRegistry { get; }
    [field: AllowNull, MaybeNull]
    protected ShardDbContextFactoryBuilder<TDbContext> ShardDbContextFactoryBuilder
        => field ??= Services.GetRequiredService<ShardDbContextFactoryBuilder<TDbContext>>();
    protected bool HasSingleShard { get; }

    public ShardDbContextFactory(IServiceProvider services)
    {
        Services = services;
        ShardRegistry = services.GetRequiredService<IDbShardRegistry<TDbContext>>();
        HasSingleShard = ShardRegistry.HasSingleShard;
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
        => _factories.GetOrAdd(shard, static (shard1, self) => {
            if (!self.ShardRegistry.CanUse(shard1))
                throw Internal.Errors.NoShard(shard1);

            var factory = self.ShardDbContextFactoryBuilder.Invoke(self.Services, shard1);
            self.ShardRegistry.Use(shard1);
            return factory;
        }, this);
}
