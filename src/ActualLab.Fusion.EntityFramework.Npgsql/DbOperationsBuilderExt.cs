using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Npgsql;

/// <summary>
/// Extension methods for <see cref="DbOperationsBuilder{TDbContext}"/> to register the
/// PostgreSQL LISTEN/NOTIFY-based operation log watcher.
/// </summary>
public static class DbOperationsBuilderExt
{
    public static DbOperationsBuilder<TDbContext> AddNpgsqlOperationLogWatcher<TDbContext>(
        this DbOperationsBuilder<TDbContext> dbOperations,
        Func<IServiceProvider, NpgsqlDbLogWatcherOptions<TDbContext>>? optionsFactory = null)
        where TDbContext : DbContext
        => dbOperations.AddOperationLogWatcher(
            typeof(NpgsqlDbLogWatcher<,>),
            _ => NpgsqlDbLogWatcherOptions<TDbContext>.Default,
            optionsFactory);
}
