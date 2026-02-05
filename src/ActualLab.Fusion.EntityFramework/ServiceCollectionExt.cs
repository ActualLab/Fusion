using System;
using Microsoft.EntityFrameworkCore;
using ActualLab.Fusion.EntityFramework.Internal;

namespace ActualLab.Fusion.EntityFramework;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register
/// <see cref="DbContextBuilder{TDbContext}"/> services and transient DbContext factories.
/// </summary>
public static class ServiceCollectionExt
{
    public static DbContextBuilder<TDbContext> AddDbContextServices<TDbContext>(this IServiceCollection services)
        where TDbContext : DbContext
        => new(services, null);

    public static IServiceCollection AddDbContextServices<TDbContext>(
        this IServiceCollection services,
        Action<DbContextBuilder<TDbContext>> configure)
        where TDbContext : DbContext
        => new DbContextBuilder<TDbContext>(services, configure).Services;

    // AddTransientDbContextFactory

    public static IServiceCollection AddTransientDbContextFactory<TDbContext>(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder>? optionsAction)
        where TDbContext : DbContext
        => services.AddTransientDbContextFactory<TDbContext>((_, db) => optionsAction?.Invoke(db));

    public static IServiceCollection AddTransientDbContextFactory<TDbContext>(
        this IServiceCollection services,
        Action<IServiceProvider, DbContextOptionsBuilder>? optionsAction)
        where TDbContext : DbContext
    {
        services.AddDbContext<TDbContext>(optionsAction, ServiceLifetime.Singleton, ServiceLifetime.Singleton);
        services.RemoveAll(x => x.ServiceType == typeof(TDbContext));
        services.AddSingleton<IDbContextFactory<TDbContext>>(
            c => new FuncDbContextFactory<TDbContext>(() => c.CreateInstance<TDbContext>()));
        return services;
    }

    public static IServiceCollection ReplaceDbEntityResolvers(
        this IServiceCollection services,
        Type entityResolverGenericType)
    {
        foreach (var descriptor in services) {
            var serviceType = descriptor.ServiceType;
            if (!serviceType.IsGenericType || serviceType.GetGenericTypeDefinition() != typeof(IDbEntityResolver<,>))
                continue;

            var newImplementationType = entityResolverGenericType.MakeGenericType(serviceType.GetGenericArguments());
            descriptor.SetImplementationFactory(c => c.CreateInstance(newImplementationType));
        }
        return services;
    }
}
