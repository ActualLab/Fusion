namespace ActualLab.DependencyInjection;

/// <summary>
/// Extension methods for <see cref="IServiceProvider"/>.
/// </summary>
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

    public static void ThrowIfDisposedOrDisposing(this IServiceProvider services)
        => services.GetService<IServiceProvider>();

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
        => new Logger<T>(services.LoggerFactory()); // See ILoggerFactory.CreateLogger<T>()
    public static ILogger LogFor(this IServiceProvider services, Type type)
        => services.LoggerFactory().CreateLogger(type.NonProxyType());
    public static ILogger LogFor(this IServiceProvider services, string category)
        => services.LoggerFactory().CreateLogger(category);

    // Get HostedServiceSet

    public static HostedServiceSet HostedServices(this IServiceProvider services)
        => new(services);

    // CreateInstance

    public static T CreateInstance<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
        this IServiceProvider services, params object[] arguments)
        => (T) services.CreateInstance(typeof(T), arguments);

    public static object CreateInstance(
        this IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type instanceType,
        params object[] arguments)
        => ActivatorUtilities.CreateInstance(services, instanceType, arguments);

    // GetServiceOrCreateInstance

    public static T GetServiceOrCreateInstance<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
        this IServiceProvider services, params object[] arguments)
        => (T)services.GetServiceOrCreateInstance(typeof(T));

    public static object GetServiceOrCreateInstance(
        this IServiceProvider services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        Type type,
        params object[] arguments)
        => services.GetService(type) ?? services.CreateInstance(type);

    // Nested types

    /// <summary>
    /// A minimal <see cref="IServiceProvider"/> that only resolves itself.
    /// </summary>
    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IServiceProvider) ? this : null;
    }
}
