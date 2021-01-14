using System;
using Microsoft.Extensions.DependencyInjection;

namespace Stl.CommandR
{
    public static class ServiceCollectionEx
    {
        public static CommanderBuilder AddCommander(this IServiceCollection services)
            => new(services);

        public static IServiceCollection AddCommander(this IServiceCollection services, Action<CommanderBuilder> configureCommander)
        {
            var commandR = services.AddCommander();
            configureCommander.Invoke(commandR);
            return services;
        }
    }
}
