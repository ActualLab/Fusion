namespace ActualLab.Resilience;

public static class ServiceProviderExt
{
    public static TransiencyResolver TransiencyResolver(this IServiceProvider services)
        => services.GetRequiredService<TransiencyResolver>();

    public static TransiencyResolver<TContext> TransiencyResolver<TContext>(this IServiceProvider services)
        where TContext : class
        => services.GetRequiredService<TransiencyResolver<TContext>>();
}
