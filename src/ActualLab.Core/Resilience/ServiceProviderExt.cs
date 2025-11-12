namespace ActualLab.Resilience;

public static class ServiceProviderExt
{
    extension(IServiceProvider services)
    {
        public TransiencyResolver TransiencyResolver()
            => services.GetRequiredService<TransiencyResolver>();

        public TransiencyResolver<TContext> TransiencyResolver<TContext>()
            where TContext : class
            => services.GetRequiredService<TransiencyResolver<TContext>>();
    }
}
