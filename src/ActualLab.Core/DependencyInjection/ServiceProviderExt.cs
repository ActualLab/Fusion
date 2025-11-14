using System.Diagnostics.CodeAnalysis;

namespace ActualLab.DependencyInjection;

public static class ServiceProviderExt
{
    public static readonly IServiceProvider Empty = new EmptyServiceProvider();

    extension(IServiceProvider services)
    {
        public bool IsDisposedOrDisposing()
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

        public ILoggerFactory LoggerFactory()
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

        public ILogger<T> LogFor<T>()
            => new Logger<T>(services.LoggerFactory()); // See ILoggerFactory.CreateLogger<T>()

        public ILogger LogFor(Type type)
            => services.LoggerFactory().CreateLogger(type.NonProxyType());

        public ILogger LogFor(string category)
            => services.LoggerFactory().CreateLogger(category);

        // Get HostedServiceSet

        public HostedServiceSet HostedServices()
            => new(services);

        // CreateInstance

        public T CreateInstance<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
            params object[] arguments)
            => (T) services.CreateInstance(typeof(T), arguments);

        public object CreateInstance(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type instanceType,
            params object[] arguments)
            => ActivatorUtilities.CreateInstance(services, instanceType, arguments);

        // GetServiceOrCreateInstance

        public T GetServiceOrCreateInstance<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>(
            params object[] arguments)
            => (T)services.GetServiceOrCreateInstance(typeof(T));

        public object GetServiceOrCreateInstance(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type,
            params object[] arguments)
            => services.GetService(type) ?? services.CreateInstance(type);
    }

    // Nested types

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
            => serviceType == typeof(IServiceProvider) ? this : null;
    }
}
