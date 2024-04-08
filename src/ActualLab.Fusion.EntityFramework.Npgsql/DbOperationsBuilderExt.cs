using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Npgsql;

public static class DbOperationsBuilderExt
{
    public static DbOperationsBuilder<TDbContext> AddNpgsqlOperationLogWatchers<TDbContext>(
        this DbOperationsBuilder<TDbContext> dbOperations,
        Func<IServiceProvider, NpgsqlDbLogWatcherOptions<TDbContext>>? optionsFactory = null)
        where TDbContext : DbContext
        => dbOperations.AddOperationLogWatchers(
            typeof(NpgsqlDbLogWatcher<,>),
            _ => NpgsqlDbLogWatcherOptions<TDbContext>.Default,
            optionsFactory);
}
