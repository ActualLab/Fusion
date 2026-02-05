namespace ActualLab.Rpc.Server;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register
/// Web API controllers as transient services.
/// </summary>
public static class ServiceCollectionExt
{
    public static IServiceCollection AddControllersAsServices(this IServiceCollection services, IEnumerable<Type> controllerTypes)
    {
        foreach (var type in controllerTypes)
            services.AddTransient(type);
        return services;
    }

    public static IServiceCollection AddControllersAsServices(this IServiceCollection services, IEnumerable<Assembly> assemblies)
    {
        foreach (var assembly in assemblies)
            services.AddControllersAsServices(assembly);
        return services;
    }

    public static IServiceCollection AddControllersAsServices(this IServiceCollection services, Assembly assembly)
    {
        services.AddControllersAsServices(assembly.GetControllerTypes());
        return services;
    }
}
