using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Tests.Extensions;

public static class KeyValueStoreFusionBuilderExt
{
    // InMemoryKeyValueStore

    public static FusionBuilder AddInMemoryKeyValueStore(
        this FusionBuilder fusion,
        Func<IServiceProvider, InMemoryKeyValueStore.Options>? optionsFactory = null)
    {
        var services = fusion.Services;
        services.TryAddSingleton<IDbShardRegistry<Unit>>(c => new DbShardRegistry<Unit>(c, DbShard.Single));
        services.TryAddSingleton<IDbShardResolver<Unit>>(c => new DbShardResolver<Unit>(c));
        services.AddSingleton(optionsFactory, _ => InMemoryKeyValueStore.Options.Default);
        // Local is explicit: IKeyValueStore authorizes nothing, so DefaultServiceMode must never expose it
        fusion.AddService<IKeyValueStore, InMemoryKeyValueStore>(RpcServiceMode.Local);
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
        // Local is explicit: IKeyValueStore authorizes nothing, so DefaultServiceMode must never expose it
        fusion.AddService<IKeyValueStore, DbKeyValueStore<TDbContext, TDbKeyValue>>(RpcServiceMode.Local);

        // DbKeyValueTrimmer - hosted service!
        services.TryAddSingleton<DbKeyValueTrimmer<TDbContext, TDbKeyValue>>();
        services.AddHostedService(c => c.GetRequiredService<DbKeyValueTrimmer<TDbContext, TDbKeyValue>>());
        return fusion;
    }
}
