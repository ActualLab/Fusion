using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Redis;

/// <summary>
/// Extension methods for <see cref="DbOperationsBuilder{TDbContext}"/> to register the
/// Redis pub/sub-based operation log watcher.
/// </summary>
public static class DbOperationsBuilderExt
{
    public static DbOperationsBuilder<TDbContext> AddRedisOperationLogWatcher<TDbContext>(
        this DbOperationsBuilder<TDbContext> dbOperations,
        Func<IServiceProvider, RedisDbLogWatcherOptions<TDbContext>>? optionsFactory = null)
        where TDbContext : DbContext
        => dbOperations.AddOperationLogWatcher(
            typeof(RedisDbLogWatcher<,>),
            _ => RedisDbLogWatcherOptions<TDbContext>.Default,
            optionsFactory);
}
