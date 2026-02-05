namespace ActualLab.RestEase;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register RestEase services.
/// </summary>
public static class ServiceCollectionExt
{
    public static RestEaseBuilder AddRestEase(this IServiceCollection services)
        => new(services, null);

    public static IServiceCollection AddRestEase(this IServiceCollection services, Action<RestEaseBuilder> configure)
        => new RestEaseBuilder(services, configure).Services;
}
