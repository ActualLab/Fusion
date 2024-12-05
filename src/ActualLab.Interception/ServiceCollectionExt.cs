using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception.Interceptors;

namespace ActualLab.Interception;

public static class ServiceCollectionExt
{
    // TypeViewFactory

    public static IServiceCollection AddTypeViewFactory(this IServiceCollection services)
    {
        services.AddSingleton(_ => TypeViewInterceptor.Options.Default);
        services.AddSingleton<TypeViewInterceptor>();
        services.AddSingleton<ITypeViewFactory, TypeViewFactory>();
        return services;
    }

    // Typed factories

    public static IServiceCollection UseTypedFactories(this IServiceCollection services)
    {
        services.AddSingleton(_ => TypedFactoryInterceptor.Options.Default);
        services.AddScoped<TypedFactoryInterceptor>();
        return services;
    }

    public static IServiceCollection AddTypedFactory<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TFactory>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TFactory : class, IRequiresAsyncProxy
#pragma warning disable IL2111
        => services.AddTypedFactory<TFactory, TypedFactoryInterceptor>(lifetime);
#pragma warning restore IL2111

    public static IServiceCollection AddTypedFactory<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TFactory,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TInterceptor>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TFactory : class, IRequiresAsyncProxy
        where TInterceptor : Interceptor
#pragma warning disable IL2111
        => services.AddTypedFactory(typeof(TFactory), typeof(TInterceptor), lifetime);
#pragma warning restore IL2111

    public static IServiceCollection AddTypedFactory(
        this IServiceCollection services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type factoryType,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
#pragma warning disable IL2111
        => services.AddTypedFactory(factoryType, typeof(TypedFactoryInterceptor), lifetime);
#pragma warning restore IL2111

    public static IServiceCollection AddTypedFactory(
        this IServiceCollection services,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type factoryType,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type interceptorType,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        // All implementations converge here.
        services.Add(new ServiceDescriptor(factoryType, c => {
            var factoryProxyType = Proxies.GetProxyType(factoryType);
            var interceptor = (Interceptor)c.GetServiceOrCreateInstance(interceptorType);
#pragma warning disable IL2072
            var proxy = (IProxy)c.GetServiceOrCreateInstance(factoryProxyType);
#pragma warning restore IL2072
            interceptor.BindTo(proxy);
            return proxy;
        }, lifetime));
        return services;
    }
}
