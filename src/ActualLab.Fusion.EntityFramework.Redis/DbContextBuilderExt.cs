using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using ActualLab.Redis;

namespace ActualLab.Fusion.EntityFramework.Redis;

/// <summary>
/// Extension methods for <see cref="DbContextBuilder{TDbContext}"/> to register
/// Redis database connections for use with Fusion EntityFramework services.
/// </summary>
public static class DbContextBuilderExt
{
    // AddRedisDb

    public static DbContextBuilder<TDbContext> AddRedisDb<TDbContext>(
        this DbContextBuilder<TDbContext> dbContext,
        Func<IServiceProvider, string> configurationFactory,
        string? keyPrefix = null)
        where TDbContext : DbContext
    {
        dbContext.Services.AddRedisDb<TDbContext>(configurationFactory, keyPrefix);
        return dbContext;
    }

    public static DbContextBuilder<TDbContext> AddRedisDb<TDbContext>(
        this DbContextBuilder<TDbContext> dbContext,
        string configuration,
        string? keyPrefix = null)
        where TDbContext : DbContext
    {
        dbContext.Services.AddRedisDb<TDbContext>(configuration, keyPrefix);
        return dbContext;
    }

    public static DbContextBuilder<TDbContext> AddRedisDb<TDbContext>(
        this DbContextBuilder<TDbContext> dbContext,
        ConfigurationOptions configuration,
        string? keyPrefix = null)
        where TDbContext : DbContext
    {
        dbContext.Services.AddRedisDb<TDbContext>(configuration, keyPrefix);
        return dbContext;
    }

    public static DbContextBuilder<TDbContext> AddRedisDb<TDbContext>(
        this DbContextBuilder<TDbContext> dbContext,
        Func<Task<IConnectionMultiplexer>> multiplexerFactory,
        string? keyPrefix = null)
        where TDbContext : DbContext
    {
        dbContext.Services.AddRedisDb<TDbContext>(multiplexerFactory, keyPrefix);
        return dbContext;
    }
}
