using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ActualLab.Resilience;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register
/// <see cref="TransiencyResolver{TContext}"/> instances.
/// </summary>
public static class ServiceCollectionExt
{
    public static IServiceCollection AddTransiencyResolver<TContext>(
        this IServiceCollection services, Func<IServiceProvider, TransiencyResolver> resolverFactory)
        where TContext : class
        => services.AddSingleton(c => resolverFactory.Invoke(c).ForContext<TContext>());

    public static IServiceCollection TryAddTransiencyResolver<TContext>(
        this IServiceCollection services, Func<IServiceProvider, TransiencyResolver> resolverFactory)
        where TContext : class
    {
        services.TryAddSingleton(c => resolverFactory.Invoke(c).ForContext<TContext>());
        return services;
    }
}
