using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Resilience;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ActualLab.Fusion.EntityFramework;

public readonly struct DbContextBuilder<TDbContext>
    where TDbContext : DbContext
{
    public IServiceCollection Services { get; }

    internal DbContextBuilder(
        IServiceCollection services,
        Action<DbContextBuilder<TDbContext>>? configure)
    {
        Services = services;
        if (services.HasService<DbHub<TDbContext>>()) {
            configure?.Invoke(this);
            return;
        }

        services.TryAddSingleton<DbHub<TDbContext>>();
        AddSharding(); // Core sharding services
        TryAddTransiencyResolver(_ => TransiencyResolvers.PreferTransient);

        configure?.Invoke(this);
    }

    // Sharding

    public ShardDbContextBuilder<TDbContext> AddSharding()
        => new(this, null);

    public DbContextBuilder<TDbContext> AddSharding(Action<ShardDbContextBuilder<TDbContext>> configure)
        => new ShardDbContextBuilder<TDbContext>(this, configure).DbContext;

    // Transiency resolvers

    public DbContextBuilder<TDbContext> AddTransiencyResolver(Func<IServiceProvider, TransiencyResolver> resolverFactory)
    {
        Services.AddTransiencyResolver<TDbContext>(resolverFactory);
        return this;
    }

    public DbContextBuilder<TDbContext> TryAddTransiencyResolver(Func<IServiceProvider, TransiencyResolver> resolverFactory)
    {
        Services.TryAddTransiencyResolver<TDbContext>(resolverFactory);
        return this;
    }

    // Entity converters

    public DbContextBuilder<TDbContext> AddEntityConverter<TDbEntity, TEntity, TConverter>()
        where TDbEntity : class
        where TEntity : notnull
        where TConverter : class, IDbEntityConverter<TDbEntity, TEntity>
    {
        Services.AddSingleton<IDbEntityConverter<TDbEntity, TEntity>, TConverter>();
        return this;
    }

    public DbContextBuilder<TDbContext> TryAddEntityConverter<TDbEntity, TEntity, TConverter>()
        where TDbEntity : class
        where TEntity : notnull
        where TConverter : class, IDbEntityConverter<TDbEntity, TEntity>
    {
        Services.TryAddSingleton<IDbEntityConverter<TDbEntity, TEntity>, TConverter>();
        return this;
    }

    // Entity resolvers

    public DbContextBuilder<TDbContext> AddEntityResolver<TKey, TDbEntity>(
        Func<IServiceProvider, DbEntityResolver<TDbContext, TKey, TDbEntity>.Options>? optionsFactory = null)
        where TKey : notnull
        where TDbEntity : class
    {
        var services = Services;
        services.AddSingleton(optionsFactory, _ => DbEntityResolver<TDbContext, TKey, TDbEntity>.Options.Default);
        Services.AddSingleton<IDbEntityResolver<TKey, TDbEntity>>(c => new DbEntityResolver<TDbContext, TKey, TDbEntity>(
            c.GetRequiredService<DbEntityResolver<TDbContext, TKey, TDbEntity>.Options>(), c));
        return this;
    }

    public DbContextBuilder<TDbContext> AddEntityResolver<TKey, TDbEntity, TResolver>(
        Func<IServiceProvider, TResolver> resolverFactory)
        where TKey : notnull
        where TDbEntity : class
        where TResolver : class, IDbEntityResolver<TKey, TDbEntity>
    {
        Services.AddSingleton<IDbEntityResolver<TKey, TDbEntity>>(resolverFactory);
        return this;
    }

    public DbContextBuilder<TDbContext> TryAddEntityResolver<TKey, TDbEntity>(
        Func<IServiceProvider, DbEntityResolver<TDbContext, TKey, TDbEntity>.Options>? optionsFactory = null)
        where TKey : notnull
        where TDbEntity : class
    {
        var services = Services;
        services.AddSingleton(optionsFactory, _ => DbEntityResolver<TDbContext, TKey, TDbEntity>.Options.Default);
        Services.TryAddSingleton<IDbEntityResolver<TKey, TDbEntity>>(c => new DbEntityResolver<TDbContext, TKey, TDbEntity>(
            c.GetRequiredService<DbEntityResolver<TDbContext, TKey, TDbEntity>.Options>(), c));
        return this;
    }

    public DbContextBuilder<TDbContext> TryAddEntityResolver<TKey, TDbEntity, TResolver>(
        Func<IServiceProvider, TResolver> resolverFactory)
        where TKey : notnull
        where TDbEntity : class
        where TResolver : class, IDbEntityResolver<TKey, TDbEntity>
    {
        Services.TryAddSingleton<IDbEntityResolver<TKey, TDbEntity>>(resolverFactory);
        return this;
    }

    // Log watchers

    public DbContextBuilder<TDbContext> AddLogWatcher<TDbEntry>(Type implementationGenericType)
        where TDbEntry : class, IDbLogEntry
    {
        var services = Services;
        var implementationType = implementationGenericType.MakeGenericType(typeof(TDbContext), typeof(TDbEntry));
        services.AddSingleton(implementationType);
        services.AddAlias(typeof(IDbLogWatcher<TDbContext, TDbEntry>), implementationType);
        return this;
    }

    public DbContextBuilder<TDbContext> TryAddLogWatcher<TDbEntry>(Type implementationGenericType)
        where TDbEntry : class, IDbLogEntry
    {
        var services = Services;
        var implementationType = implementationGenericType.MakeGenericType(typeof(TDbContext), typeof(TDbEntry));
        if (services.HasService(implementationType))
            return this;

        services.AddSingleton(implementationType);
        services.AddAlias(typeof(IDbLogWatcher<TDbContext, TDbEntry>), implementationType);
        return this;
    }

    // Operations

    public DbOperationsBuilder<TDbContext> AddOperations()
        => new(this, null);

    public DbContextBuilder<TDbContext> AddOperations(Action<DbOperationsBuilder<TDbContext>> configure)
        => new DbOperationsBuilder<TDbContext>(this, configure).DbContext;
}
