using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web.Http;
using System.Web.Http.Dependencies;
using Microsoft.Extensions.DependencyInjection;

namespace Stl.Fusion.Server
{
    public static class ServiceProviderExt
    {
        public static IServiceCollection AddControllersAsServices(this IServiceCollection services,
            IEnumerable<Type> controllerTypes)
        {
            foreach (var type in controllerTypes)
                services.AddTransient(type);
            return services;
        }

        public static IServiceCollection AddControllersAsServices(IServiceCollection services,
            IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies)
                AddControllersAsServices(services, assembly);
            return services;
        }

        public static IServiceCollection AddControllersAsServices(this IServiceCollection services, Assembly assembly)
        {
            services.AddControllersAsServices(assembly.GetControllerTypes());
            return services;
        }

        public static IDependencyResolver BuildDependencyResolver(this IServiceCollection services,
            Action<IServiceCollection> configure)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            configure(services);
            return services.BuildDependencyResolver();
        }

        public static IDependencyResolver BuildDependencyResolver(this IServiceCollection services)
        {
            var serviceProvider = services.BuildServiceProvider();
            return new DefaultDependencyResolver(serviceProvider);
        }

        public static HttpConfiguration AddDependencyResolver(this HttpConfiguration config, Action<IServiceCollection> configure)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (configure == null) throw new ArgumentNullException(nameof(configure));
            config.DependencyResolver = new ServiceCollection().BuildDependencyResolver(configure);
            return config;
        }
    }
}
