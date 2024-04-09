using System.Data;
using ActualLab.Fusion.EntityFramework.LogProcessing;
using ActualLab.Fusion.EntityFramework.Operations;
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

        // DbOperationLogProcessor & trimmer - hosted services!
        DbContext.TryAddEntityResolver<long, DbOperation>();
        services.TryAddSingleton(_ => DbOperationLogProcessor<TDbContext>.Options.Default);
        services.TryAddSingleton<DbOperationLogProcessor<TDbContext>>();
        services.TryAddSingleton(_ => DbOperationLogTrimmer<TDbContext>.Options.Default);
        services.TryAddSingleton<DbOperationLogTrimmer<TDbContext>>();
        services.AddHostedService(c => c.GetRequiredService<DbOperationLogProcessor<TDbContext>>());
        services.AddHostedService(c => c.GetRequiredService<DbOperationLogTrimmer<TDbContext>>());

        // DbOperationLogProcessor & trimmer - hosted services!
        services.TryAddSingleton<OperationEventProcessor>();
        DbContext.TryAddEntityResolver<long, DbOperationEvent>();
        services.TryAddSingleton(_ => DbOperationEventLogProcessor<TDbContext>.Options.Default);
        services.TryAddSingleton<DbOperationEventLogProcessor<TDbContext>>();
        services.TryAddSingleton(_ => DbOperationEventLogTrimmer<TDbContext>.Options.Default);
        services.TryAddSingleton<DbOperationEventLogTrimmer<TDbContext>>();
        services.AddHostedService(c => c.GetRequiredService<DbOperationEventLogProcessor<TDbContext>>());
        services.AddHostedService(c => c.GetRequiredService<DbOperationEventLogTrimmer<TDbContext>>());

        // Fake operation log processor
        var fakeOperationLogWatcherType = typeof(FakeDbLogWatcher<,>);
        DbContext.TryAddLogWatcher<DbOperation>(fakeOperationLogWatcherType);
        DbContext.TryAddLogWatcher<DbOperationEvent>(fakeOperationLogWatcherType);

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
        DbContext.TryAddLogWatcher<DbOperationEvent>(implementationGenericType);
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
