using Microsoft.EntityFrameworkCore;

namespace ActualLab.Fusion.EntityFramework.Npgsql;

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
