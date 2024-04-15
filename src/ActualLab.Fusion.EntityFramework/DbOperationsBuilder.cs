using System.Data;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Fusion.EntityFramework.Operations;
using ActualLab.Fusion.EntityFramework.Operations.LogProcessing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

        // DbOperationCompletionListener
        services.TryAddSingleton(_ => DbOperationCompletionListener<TDbContext>.Options.Default);
        AddOperationCompletionListener<DbOperationCompletionListener<TDbContext>>();

        // DbEventProcessor
        services.TryAddSingleton<DbEventProcessor<TDbContext>>();

        // DbOperationLogReader & trimmer - hosted services!
        DbContext.TryAddEntityResolver<long, DbOperation>();
        services.TryAddSingleton(_ => DbOperationLogReader<TDbContext>.Options.Default);
        services.TryAddSingleton<DbOperationLogReader<TDbContext>>();
        services.TryAddSingleton(_ => DbOperationLogTrimmer<TDbContext>.Options.Default);
        services.TryAddSingleton<DbOperationLogTrimmer<TDbContext>>();
        services.AddHostedService(c => c.GetRequiredService<DbOperationLogReader<TDbContext>>());
        services.AddHostedService(c => c.GetRequiredService<DbOperationLogTrimmer<TDbContext>>());

        // DbEventLogReader & trimmer - hosted services!
        DbContext.TryAddEntityResolver<long, DbEvent>();
        services.TryAddSingleton(_ => DbEventLogReader<TDbContext>.Options.Default);
        services.TryAddSingleton<DbEventLogReader<TDbContext>>();
        services.TryAddSingleton(_ => DbEventLogTrimmer<TDbContext>.Options.Default);
        services.TryAddSingleton<DbEventLogTrimmer<TDbContext>>();
        services.AddHostedService(c => c.GetRequiredService<DbEventLogReader<TDbContext>>());
        services.AddHostedService(c => c.GetRequiredService<DbEventLogTrimmer<TDbContext>>());

        // Fake operation log watchers - they just log warnings stating log watchers aren't setup,
        // everything will still work with them, but operations & events will be processed
        // with 5-second delays or so.
        var fakeOperationLogWatcherType = typeof(FakeDbLogWatcher<,>);
        DbContext.TryAddLogWatcher<DbOperation>(fakeOperationLogWatcherType);
        // DbEvent log watcher is local: there is no need to notify watchers on other hosts,
        // coz the current host can instantly process new events right once it completes the transaction,
        // and if it can't do this, one of hosts will anyway do that on the next check.
        DbContext.TryAddLogWatcher<DbEvent>(typeof(LocalDbLogWatcher<,>));

        configure?.Invoke(this);
    }

    // Core settings

    public DbOperationsBuilder<TDbContext> ConfigureOperationScope(
        Func<IServiceProvider, DbOperationScope<TDbContext>.Options> optionsFactory)
    {
        Services.AddSingleton(optionsFactory);
        return this;
    }

    public DbOperationsBuilder<TDbContext> ConfigureOperationLogReader(
        Func<IServiceProvider, DbOperationLogReader<TDbContext>.Options> optionsFactory)
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

    // Isolation level selectors

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

    // Operation completion listeners

    public DbOperationsBuilder<TDbContext> AddOperationCompletionListener<TListener>(
        Func<IServiceProvider, TListener>? factory = null)
        where TListener : class, IOperationCompletionListener
        => AddOperationCompletionListener(typeof(TListener), factory);

    public DbOperationsBuilder<TDbContext> AddOperationCompletionListener(
        Type listenerType,
        Func<IServiceProvider, object>? factory = null)
    {
        if (!typeof(IOperationCompletionListener).IsAssignableFrom(listenerType))
            throw ActualLab.Internal.Errors.MustBeAssignableTo<IOperationCompletionListener>(listenerType, nameof(listenerType));

        var descriptor = factory != null
            ? ServiceDescriptor.Singleton(typeof(IOperationCompletionListener), factory)
            : ServiceDescriptor.Singleton(typeof(IOperationCompletionListener), listenerType);
        Services.TryAddEnumerable(descriptor);
        return this;
    }

    // Operation log watchers

    public DbOperationsBuilder<TDbContext> AddOperationLogWatchers<TOptions>(
        Type implementationGenericType,
        Func<IServiceProvider, TOptions> defaultOptionsFactory,
        Func<IServiceProvider, TOptions>? optionsFactory = null)
        where TOptions : class
    {
        var services = Services;
        services.AddSingleton(optionsFactory, defaultOptionsFactory);
        DbContext.TryAddLogWatcher<DbOperation>(implementationGenericType);
        DbContext.TryAddLogWatcher<DbEvent>(implementationGenericType);
        return this;
    }

    // FileSystem operation log watchers

    public DbOperationsBuilder<TDbContext> AddFileSystemOperationLogWatchers(
        Func<IServiceProvider, FileSystemDbLogWatcherOptions<TDbContext>>? optionsFactory = null)
        => AddOperationLogWatchers(
            typeof(FileSystemDbLogWatcher<,>),
            _ => FileSystemDbLogWatcherOptions<TDbContext>.Default,
            optionsFactory);
}
