using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ActualLab.Fusion.EntityFramework;

public readonly struct ShardDbContextBuilder<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbContext>
    where TDbContext : DbContext
{
    public DbContextBuilder<TDbContext> DbContext { get; }
    public IServiceCollection Services => DbContext.Services;

    internal ShardDbContextBuilder(
        DbContextBuilder<TDbContext> dbContext,
        Action<ShardDbContextBuilder<TDbContext>>? configure)
    {
        DbContext = dbContext;
        var services = Services;
        if (services.HasService<IShardDbContextFactory<TDbContext>>()) {
            configure?.Invoke(this);
            return;
        }

        services.TryAddSingleton<IDbShardResolver, DbShardResolver>();
        services.TryAddSingleton<IDbShardRegistry<TDbContext>>(c => new DbShardRegistry<TDbContext>(c, DbShard.None));
        configure?.Invoke(this);
    }

    // AddShardResolver

    public ShardDbContextBuilder<TDbContext> AddShardResolver(Func<IServiceProvider, IDbShardResolver> factory)
    {
        Services.AddSingleton(factory);
        return this;
    }

    public ShardDbContextBuilder<TDbContext> AddShardResolver<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TShardResolver>()
        where TShardResolver : class, IDbShardResolver
    {
        Services.AddSingleton<IDbShardResolver, TShardResolver>();
        return this;
    }

    // AddShardRegistry

    public ShardDbContextBuilder<TDbContext> AddShardRegistry(IEnumerable<DbShard> shards)
        => AddShardRegistry(shards.ToArray());

    public ShardDbContextBuilder<TDbContext> AddShardRegistry(params DbShard[] shards)
    {
        Services.AddSingleton<IDbShardRegistry<TDbContext>>(c => new DbShardRegistry<TDbContext>(c, shards));
        return this;
    }

    public ShardDbContextBuilder<TDbContext> AddShardRegistry(
        Func<IServiceProvider, IDbShardRegistry<TDbContext>> factory)
    {
        Services.AddSingleton(factory);
        return this;
    }

    public ShardDbContextBuilder<TDbContext> AddShardRegistry<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TShardRegistry>()
        where TShardRegistry : class, IDbShardRegistry<TDbContext>
    {
        Services.AddSingleton<IDbShardRegistry<TDbContext>, TShardRegistry>();
        return this;
    }

    // AddShardDbContextFactory

    public ShardDbContextBuilder<TDbContext> AddPooledShardDbContextFactory(
        Action<IServiceProvider, DbShard, DbContextOptionsBuilder> dbContextOptionsBuilder)
    {
        AddShardDbContextFactory((c, shard, services) => {
            services.AddPooledDbContextFactory<TDbContext>(
                db => {
                    // This ensures logging settings from the main container
                    // are applied to ShardDbContextFactory's DbContexts
                    var loggerFactory = c.GetService<ILoggerFactory>();
                    if (loggerFactory != null)
                        db.UseLoggerFactory(loggerFactory);
                    dbContextOptionsBuilder.Invoke(c, shard, db);
                });
        });
        return this;
    }

    public ShardDbContextBuilder<TDbContext> AddTransientShardDbContextFactory(
        Action<IServiceProvider, DbShard, DbContextOptionsBuilder> dbContextOptionsBuilder)
    {
        AddShardDbContextFactory((c, shard, services) => {
            services.AddTransientDbContextFactory<TDbContext>(
                db => {
                    // This ensures logging settings from the main container
                    // are applied to ShardDbContextFactory's DbContexts
                    var loggerFactory = c.GetService<ILoggerFactory>();
                    if (loggerFactory != null)
                        db.UseLoggerFactory(loggerFactory);
                    dbContextOptionsBuilder.Invoke(c, shard, db);
                });
        });
        return this;
    }

    public ShardDbContextBuilder<TDbContext> AddShardDbContextFactory(
        Action<IServiceProvider, DbShard, ServiceCollection> dbContextServiceCollectionBuilder)
    {
        AddShardDbContextFactory(_ => (c, shard) => {
            var services = new ServiceCollection();
            dbContextServiceCollectionBuilder.Invoke(c, shard, services);
            var serviceProvider = services.BuildServiceProvider();
            var dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<TDbContext>>();
            return dbContextFactory;
        });
        return this;
    }

    public ShardDbContextBuilder<TDbContext> AddShardDbContextFactory(
        Func<IServiceProvider, ShardDbContextFactoryBuilder<TDbContext>> factoryBuilder)
    {
        Services.AddSingleton(factoryBuilder);
        Services.AddSingleton<IShardDbContextFactory<TDbContext>, ShardDbContextFactory<TDbContext>>();
        return this;
    }

    public ShardDbContextBuilder<TDbContext> AddShardDbContextFactory(
        Func<IServiceProvider, IShardDbContextFactory<TDbContext>> factory)
    {
        Services.AddSingleton(factory);
        return this;
    }

    public ShardDbContextBuilder<TDbContext> AddShardDbContextFactory<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TShardDbContextFactory>()
        where TShardDbContextFactory : class, IShardDbContextFactory<TDbContext>
    {
        Services.AddSingleton<IShardDbContextFactory<TDbContext>, TShardDbContextFactory>();
        return this;
    }
}
