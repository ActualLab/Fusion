using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Extensions.Services;

namespace ActualLab.Fusion.Extensions;

public static class FusionBuilderExt
{
    // SandboxedKeyValueStore

    public static FusionBuilder AddSandboxedKeyValueStore<TContext>(
        this FusionBuilder fusion,
        Func<IServiceProvider, SandboxedKeyValueStore<TContext>.Options>? optionsFactory = null)
    {
        var services = fusion.Services;
        services.AddSingleton(optionsFactory, _ => SandboxedKeyValueStore<TContext>.Options.Default);
        fusion.AddService<ISandboxedKeyValueStore, SandboxedKeyValueStore<TContext>>();
        return fusion;
    }

    // InMemoryKeyValueStore

    public static FusionBuilder AddInMemoryKeyValueStore(
        this FusionBuilder fusion,
        Func<IServiceProvider, InMemoryKeyValueStore.Options>? optionsFactory = null)
    {
        var services = fusion.Services;
        // Even though InMemoryKeyValueStore doesn't need TDbContext,
        // SandboxedKeyValueStore uses DbShard-based APIs, so we add fake IDbShardRegistry<Unit>
        // to let it use Unit as TDbContext.
        services.TryAddSingleton<IDbShardRegistry<Unit>>(c => new DbShardRegistry<Unit>(c, DbShard.None));
        services.TryAddSingleton<IDbShardResolver<Unit>>(c => new DbShardResolver<Unit>(c));
        services.AddSingleton(optionsFactory, _ => InMemoryKeyValueStore.Options.Default);
        fusion.AddService<IKeyValueStore, InMemoryKeyValueStore>();
        services.AddHostedService(c => (InMemoryKeyValueStore)c.GetRequiredService<IKeyValueStore>());
        return fusion;
    }

    // DbKeyValueStore

    public static FusionBuilder AddDbKeyValueStore<TDbContext>(
        this FusionBuilder fusion,
        Func<IServiceProvider, DbKeyValueTrimmer<TDbContext, DbKeyValue>.Options>? keyValueTrimmerOptionsFactory = null)
        where TDbContext : DbContext
        => fusion.AddDbKeyValueStore<TDbContext, DbKeyValue>(keyValueTrimmerOptionsFactory);

    public static FusionBuilder AddDbKeyValueStore<TDbContext,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbKeyValue>(
        this FusionBuilder fusion,
        Func<IServiceProvider, DbKeyValueTrimmer<TDbContext, TDbKeyValue>.Options>? keyValueTrimmerOptionsFactory = null)
        where TDbContext : DbContext
        where TDbKeyValue : DbKeyValue, new()
    {
        var services = fusion.Services;
        services.AddSingleton(keyValueTrimmerOptionsFactory, _ => DbKeyValueTrimmer<TDbContext, TDbKeyValue>.Options.Default);
        if (services.HasService<DbKeyValueTrimmer<TDbContext, TDbKeyValue>>())
            return fusion;

        var dbContext = services.AddDbContextServices<TDbContext>();
        dbContext.AddOperations();
        dbContext.TryAddEntityResolver<string, TDbKeyValue>();
        fusion.AddService<IKeyValueStore, DbKeyValueStore<TDbContext, TDbKeyValue>>();

        // DbKeyValueTrimmer - hosted service!
        services.TryAddSingleton<DbKeyValueTrimmer<TDbContext, TDbKeyValue>>();
        services.AddHostedService(c => c.GetRequiredService<DbKeyValueTrimmer<TDbContext, TDbKeyValue>>());
        return fusion;
    }
}
