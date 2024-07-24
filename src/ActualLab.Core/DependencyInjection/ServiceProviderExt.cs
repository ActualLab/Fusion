using System.Diagnostics.CodeAnalysis;

namespace ActualLab.DependencyInjection;

public static class ServiceProviderExt
{
    public static readonly IServiceProvider Empty = new EmptyServiceProvider();

    public static bool IsDisposedOrDisposing(this IServiceProvider services)
    {
        try {
            services.GetService<IServiceProvider>();
            return false;
        }
        catch (ObjectDisposedException) {
            return true;
        }
    }

    // Logging extensions

    public static ILoggerFactory LoggerFactory(this IServiceProvider services)
    {
        try {
            return services.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance;
        }
        catch (ObjectDisposedException) {
            // ILoggerFactory could be requested during IServiceProvider disposal,
            // and we don't want to get any exceptions during this process.
            return NullLoggerFactory.Instance;
        }
    }

    public static ILogger<T> LogFor<T>(this IServiceProvider services)
        => new Logger<T>(services.LoggerFactory());
    public static ILogger LogFor(this IServiceProvider services, Type type)
        => services.LoggerFactory().CreateLogger(type.NonProxyType());
    public static ILogger LogFor(this IServiceProvider services, string category)
        => services.LoggerFactory().CreateLogger(category);

    // Get HostedServiceSet

    public static HostedServiceSet HostedServices(this IServiceProvider services)
        => new(services);

    // GetOrActivate

    public static T GetOrActivate<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
        this IServiceProvider services, params object[] arguments)
        => (T)services.GetOrActivate(typeof(T));

    public static object GetOrActivate(
        this IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type type,
        params object[] arguments)
        => services.GetService(type) ?? services.Activate(type);

    // Activate

    public static T Activate<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
        this IServiceProvider services, params object[] arguments)
        => (T) services.Activate(typeof(T), arguments);

    public static object Activate(
        this IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type instanceType,
        params object[] arguments)
        => ActivatorUtilities.CreateInstance(services, instanceType, arguments);

    // GetRequiredMixedModeService

    public static T GetRequiredMixedModeService<T>(this IServiceProvider services)
        where T : class
    {
        var singleton = services.GetRequiredService<MixedModeService<T>.Singleton>();
        if (ReferenceEquals(singleton.Services, services))
            return singleton.Service;

        var scoped = services.GetRequiredService<MixedModeService<T>.Scoped>();
        return scoped.Service;
    }

    // Nested types

    private class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IServiceProvider) ? this : null;
    }
}
