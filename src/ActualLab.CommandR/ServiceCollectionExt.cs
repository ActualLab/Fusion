using ActualLab.CommandR.Operations;

namespace ActualLab.CommandR;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to register the commander.
/// </summary>
public static class ServiceCollectionExt
{
    public static CommanderBuilder AddCommander(this IServiceCollection services)
        => new(services, null);

    public static IServiceCollection AddCommander(this IServiceCollection services, Action<CommanderBuilder> configure)
        => new CommanderBuilder(services, configure).Services;

    public static ServiceTypeResolver AddServiceTypeResolver(this IServiceCollection services)
        => services.FindOrAddInstance(() => new ServiceTypeResolver(), addInFront: true);
}
