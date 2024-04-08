using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.Fusion.EntityFramework.Operations;

namespace ActualLab.Fusion.EntityFramework;

public readonly struct DbOperationsBuilder<TDbContext>
    where TDbContext : DbContext
{
    public DbContextBuilder<TDbContext> DbContext { get; }
    public IServiceCollection Services => DbContext.Services;

    internal DbOperationsBuilder(
        DbContextBuilder<TDbContext> dbContext,
        Action<DbOperationsBuilder<TDbContext>>? configure)
    {
        DbContext = dbContext;
        var services = Services;
        if (services.HasService<DbOperationScopeProvider<TDbContext>>()) {
            configure?.Invoke(this);
            return;
        }

        // DbOperationScope & its CommandR handler
        services.AddSingleton<DbOperationScopeProvider<TDbContext>>();
        services.AddCommander().AddHandlers<DbOperationScopeProvider<TDbContext>>();
        services.TryAddSingleton<DbOperationScope<TDbContext>.Options>();
        // DbOperationScope<TDbContext> is created w/ services.Activate

        // DbOperationLogProcessor & trimmer - hosted services!
        DbContext.TryAddEntityResolver<long, DbOperation>();
        services.TryAddSingleton<DbOperationLogProcessor<TDbContext>.Options>();
        services.TryAddSingleton<DbOperationLogProcessor<TDbContext>>();
        services.TryAddSingleton<DbOperationLogTrimmer<TDbContext>.Options>();
        services.TryAddSingleton<DbOperationLogTrimmer<TDbContext>>();
        services.AddHostedService(c => c.GetRequiredService<DbOperationLogProcessor<TDbContext>>());
        services.AddHostedService(c => c.GetRequiredService<DbOperationLogTrimmer<TDbContext>>());

        // DbOperationLogProcessor & trimmer - hosted services!
        services.TryAddSingleton<OperationEventProcessor>();
        DbContext.TryAddEntityResolver<long, DbOperationEvent>();
        services.TryAddSingleton<DbOperationEventLogProcessor<TDbContext>.Options>();
        services.TryAddSingleton<DbOperationEventLogProcessor<TDbContext>>();
        services.TryAddSingleton<DbOperationEventLogTrimmer<TDbContext>.Options>();
        services.TryAddSingleton<DbOperationEventLogTrimmer<TDbContext>>();
        services.AddHostedService(c => c.GetRequiredService<DbOperationEventLogProcessor<TDbContext>>());
        services.AddHostedService(c => c.GetRequiredService<DbOperationEventLogTrimmer<TDbContext>>());

        configure?.Invoke(this);
    }

    // Core settings

    public DbOperationsBuilder<TDbContext> ConfigureOperationScope(
        Func<IServiceProvider, DbOperationScope<TDbContext>.Options> optionsFactory)
    {
        Services.AddSingleton(optionsFactory);
        return this;
    }

    public DbOperationsBuilder<TDbContext> ConfigureOperationLogProcessor(
        Func<IServiceProvider, DbOperationLogProcessor<TDbContext>.Options> optionsFactory)
    {
        Services.AddSingleton(optionsFactory);
        return this;
    }

    public DbOperationsBuilder<TDbContext> ConfigureOperationLogTrimmer(
        Func<IServiceProvider, DbOperationLogTrimmer<TDbContext>.Options> optionsFactory)
    {
        Services.AddSingleton(optionsFactory);
        return this;
    }

    // DbIsolationLevelSelectors

    public DbOperationsBuilder<TDbContext> AddIsolationLevelSelector(
        Func<IServiceProvider, DbIsolationLevelSelector<TDbContext>> dbIsolationLevelSelector)
    {
        Services.AddSingleton(dbIsolationLevelSelector);
        return this;
    }

    public DbOperationsBuilder<TDbContext> AddIsolationLevelSelector(
        Func<IServiceProvider, CommandContext, IsolationLevel> dbIsolationLevelSelector)
    {
        Services.AddSingleton(c => new DbIsolationLevelSelector<TDbContext>(
            context => dbIsolationLevelSelector.Invoke(c, context)));
        return this;
    }

    public DbOperationsBuilder<TDbContext> TryAddIsolationLevelSelector(
        Func<IServiceProvider, DbIsolationLevelSelector<TDbContext>> dbIsolationLevelSelector)
    {
        Services.TryAddSingleton(dbIsolationLevelSelector);
        return this;
    }

    public DbOperationsBuilder<TDbContext> TryAddIsolationLevelSelector(
        Func<IServiceProvider, CommandContext, IsolationLevel> dbIsolationLevelSelector)
    {
        Services.TryAddSingleton(c => new DbIsolationLevelSelector<TDbContext>(
            context => dbIsolationLevelSelector.Invoke(c, context)));
        return this;
    }

    // File-based operation log change tracking

    public DbOperationsBuilder<TDbContext> AddFileBasedOperationLogChangeTracking(
        Func<IServiceProvider, FileBasedDbOperationLogChangeTrackingOptions<TDbContext>>? optionsFactory = null)
    {
        var services = Services;
        services.AddSingleton(optionsFactory, _ => FileBasedDbOperationLogChangeTrackingOptions<TDbContext>.Default);
        if (services.HasService<FileBasedDbOperationLogChangeTracker<TDbContext>>())
            return this;

        services.AddSingleton(c => new FileBasedDbOperationLogChangeTracker<TDbContext>(
            c.GetRequiredService<FileBasedDbOperationLogChangeTrackingOptions<TDbContext>>(), c));
        services.AddAlias<
            IDbOperationLogChangeTracker<TDbContext>,
            FileBasedDbOperationLogChangeTracker<TDbContext>>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<
                IOperationCompletionListener,
                FileBasedDbOperationLogChangeNotifier<TDbContext>>());
        return this;
    }
}
