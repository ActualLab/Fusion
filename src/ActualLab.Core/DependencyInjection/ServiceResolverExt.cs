using ActualLab.Internal;

namespace ActualLab.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="ServiceResolver"/>.
/// </summary>
public static class ServiceResolverExt
{
    public static object? TryResolve(this ServiceResolver? resolver, IServiceProvider services)
    {
        if (resolver is null)
            return null;

        if (resolver.Resolver is null)
            return services.GetService(resolver.Type);

        var service = resolver.Resolver.Invoke(services);
        if (ReferenceEquals(service, null))
            return service;

        var actualType = service.GetType();
        return resolver.Type.IsAssignableFrom(actualType)
            ? service
            : throw Errors.MustBeAssignableTo(actualType, resolver.Type);
    }

    public static object Resolve(this ServiceResolver? resolver, IServiceProvider services)
    {
        if (resolver is null)
            throw new ArgumentNullException(nameof(resolver));

        if (resolver.Resolver is null)
            return services.GetRequiredService(resolver.Type);

        var service = resolver.Resolver.Invoke(services);
        if (ReferenceEquals(service, null))
            throw Errors.ImplementationNotFound(resolver.Type);

        var actualType = service.GetType();
        return resolver.Type.IsAssignableFrom(actualType)
            ? service
            : throw Errors.MustBeAssignableTo(actualType, resolver.Type);
    }
}
