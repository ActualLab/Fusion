namespace ActualLab.Resilience;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/> to resolve
/// <see cref="TransiencyResolver"/> instances.
/// </summary>
public static class ServiceProviderExt
{
    public static TransiencyResolver TransiencyResolver(this IServiceProvider services)
        => services.GetRequiredService<TransiencyResolver>();

    public static TransiencyResolver<TContext> TransiencyResolver<TContext>(this IServiceProvider services)
        where TContext : class
        => services.GetRequiredService<TransiencyResolver<TContext>>();
}
