using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ActualLab.DependencyInjection.Internal;
using ActualLab.Internal;

namespace ActualLab.DependencyInjection;

public static class ServiceCollectionExt
{
    // HasService

    public static bool HasService<TService>(this IServiceCollection services)
        => services.HasService(typeof(TService));
    public static bool HasService(this IServiceCollection services, Type serviceType)
        => services.Any(d => d.ServiceType == serviceType);

    // RemoveAll

    public static IServiceCollection RemoveAll(this IServiceCollection services, Func<ServiceDescriptor, bool> predicate)
    {
        for (var i = services.Count - 1; i >= 0; i--) {
            var service = services[i];
            if (predicate.Invoke(service))
                services.RemoveAt(i);
        }
        return services;
    }

    // Options

    public static IServiceCollection Configure<TOptions>(
        this IServiceCollection services,
        Action<IServiceProvider, string?, TOptions> configureOptions)
        where TOptions : class
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));
        if (configureOptions == null)
            throw new ArgumentNullException(nameof(configureOptions));
        services.AddOptions();
        services.TryAddSingleton<IConfigureOptions<TOptions>>(
            c => new ConfigureAllNamedOptions<TOptions>(c, configureOptions));
        return services;
    }

    // AddSingleton

    public static IServiceCollection AddSingleton<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, TService>? factory,
        Func<IServiceProvider, TService> defaultFactory)
        where TService : class
    {
        if (factory != null)
            services.AddSingleton(factory);
        else
            services.TryAddSingleton(defaultFactory);
        return services;
    }

    // AddScopedOrSingleton

    public static IServiceCollection AddScopedOrSingleton<TService>(
        this IServiceCollection services,
        Func<IServiceProvider, bool, TService> factory)
        where TService : class
    {
        services.AddSingleton(c => {
            var lazy = new LazySlim<TService>(() => factory.Invoke(c, false));
            return new ScopedOrSingleton<LazySlim<TService>>.Singleton(c, lazy);
        });
        services.AddScoped(c => {
            var lazy = new LazySlim<TService>(() => factory.Invoke(c, true));
            return new ScopedOrSingleton<LazySlim<TService>>.Scoped(c, lazy);
        });

        ScopedOrSingleton<LazySlim<TService>>.Singleton? singleton = null;
        services.AddTransient<TService>(c => {
            // Singleton is always available, so we start from it
            singleton ??= c.GetRequiredService<ScopedOrSingleton<LazySlim<TService>>.Singleton>();
            var lazy = ReferenceEquals(singleton.Services, c) // Is c a root service provider?
                ? singleton.Value
                : c.GetRequiredService<ScopedOrSingleton<LazySlim<TService>>.Scoped>().Value;
            return lazy.Value;
        });
        return services;
    }

    // AddAlias

    public static IServiceCollection AddAlias<TAlias, TService>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TAlias : class
        where TService : class, TAlias
    {
        var descriptor = new ServiceDescriptor(typeof(TAlias),
            c => c.GetRequiredService<TService>(),
            lifetime);
        services.Add(descriptor);
        return services;
    }

    public static IServiceCollection TryAddAlias<TAlias, TService>(
        this IServiceCollection services,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
        where TAlias : class
        where TService : class, TAlias
    {
        var descriptor = new ServiceDescriptor(typeof(TAlias),
            c => c.GetRequiredService<TService>(),
            lifetime);
        services.TryAdd(descriptor);
        return services;
    }

    public static IServiceCollection AddAlias(
        this IServiceCollection services,
        Type aliasType,
        Type serviceType,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        var descriptor = new ServiceDescriptor(aliasType, c => c.GetRequiredService(serviceType), lifetime);
        services.Add(descriptor);
        return services;
    }

    public static IServiceCollection TryAddAlias(
        this IServiceCollection services,
        Type aliasType,
        Type serviceType,
        ServiceLifetime lifetime = ServiceLifetime.Singleton)
    {
        var descriptor = new ServiceDescriptor(aliasType, c => c.GetRequiredService(serviceType), lifetime);
        services.TryAdd(descriptor);
        return services;
    }

    // AddSettings

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static IServiceCollection AddSettings<TSettings>(
        this IServiceCollection services,
        bool mustValidate = true)
        where TSettings : class, new()
        => services.AddSingleton<TSettings>(c => {
            var cfg = c.GetRequiredService<IConfiguration>();
            return cfg.GetSettings<TSettings>(mustValidate);
        });

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static IServiceCollection AddSettings<TSettings>(
        this IServiceCollection services,
        string? sectionName,
        bool mustValidate = true)
        where TSettings : class, new()
        => services.AddSingleton<TSettings>(c => {
            var cfg = c.GetRequiredService<IConfiguration>();
            return cfg.GetSettings<TSettings>(sectionName, mustValidate);
        });

    // FindInstance, AddInstance

    public static T? FindInstance<T>(this IServiceCollection services)
        where T : class
        => services.FindInstance(typeof(T)) as T;

    public static object? FindInstance(this IServiceCollection services, Type type)
    {
        foreach (var d in services) {
            if (d.ServiceType != type)
                continue;
#if NET8_0_OR_GREATER
            if (d is not { Lifetime: ServiceLifetime.Singleton, IsKeyedService: false })
                continue;
#else
            if (d is not { Lifetime: ServiceLifetime.Singleton })
                continue;
#endif

            return d.ImplementationInstance;
        }
        return null;
    }

    public static T FindOrAddInstance<T>(
        this IServiceCollection services, Func<T> instanceFactory, bool addInFront = false)
        where T : class
        => services.FindInstance<T>() ?? services.AddInstance(instanceFactory.Invoke(), addInFront);

    public static T AddInstance<T>(
        this IServiceCollection services, T instance, bool addInFront = false)
        where T : class
    {
        var descriptor = new ServiceDescriptor(typeof(T), instance);
        if (addInFront)
            services.Insert(0, descriptor);
        else
            services.Add(descriptor);
        return instance;
    }
}
