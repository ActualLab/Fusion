using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Redis;

public static class DbOperationsBuilderExt
{
    public static DbOperationsBuilder<TDbContext> AddRedisOperationLogWatchers<TDbContext>(
        this DbOperationsBuilder<TDbContext> dbOperations,
        Func<IServiceProvider, RedisDbLogWatcherOptions<TDbContext>>? optionsFactory = null)
        where TDbContext : DbContext
        => dbOperations.AddOperationLogWatchers(
            typeof(RedisDbLogWatcher<,>),
            _ => RedisDbLogWatcherOptions<TDbContext>.Default,
            optionsFactory);
}
