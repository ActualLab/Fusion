using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ActualLab.Fusion.EntityFramework;
using ActualLab.Fusion.Extensions.Services;
using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion.Extensions;

public static class FusionBuilderExt
{
    // SandboxedKeyValueStore

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static FusionBuilder AddSandboxedKeyValueStore(
        this FusionBuilder fusion,
        Func<IServiceProvider, SandboxedKeyValueStore.Options>? optionsFactory = null)
    {
        var services = fusion.Services;
        services.AddSingleton(optionsFactory, _ => SandboxedKeyValueStore.Options.Default);
        fusion.AddService<ISandboxedKeyValueStore, SandboxedKeyValueStore>();
        return fusion;
    }

    // InMemoryKeyValueStore

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static FusionBuilder AddInMemoryKeyValueStore(
        this FusionBuilder fusion,
        Func<IServiceProvider, InMemoryKeyValueStore.Options>? optionsFactory = null)
    {
        var services = fusion.Services;
        services.AddSingleton(optionsFactory, _ => InMemoryKeyValueStore.Options.Default);
        fusion.AddService<IKeyValueStore, InMemoryKeyValueStore>();
        services.AddHostedService(c => (InMemoryKeyValueStore)c.GetRequiredService<IKeyValueStore>());
        return fusion;
    }

    // DbKeyValueStore

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static FusionBuilder AddDbKeyValueStore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbContext>(
        this FusionBuilder fusion,
        Func<IServiceProvider, DbKeyValueTrimmer<TDbContext, DbKeyValue>.Options>? keyValueTrimmerOptionsFactory = null)
        where TDbContext : DbContext
        => fusion.AddDbKeyValueStore<TDbContext, DbKeyValue>(keyValueTrimmerOptionsFactory);

    [RequiresUnreferencedCode(UnreferencedCode.Fusion)]
    public static FusionBuilder AddDbKeyValueStore<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TDbContext,
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
