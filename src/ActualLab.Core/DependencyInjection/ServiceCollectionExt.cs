using System.ComponentModel.DataAnnotations;
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
}
