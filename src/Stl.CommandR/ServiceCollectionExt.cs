using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Internal;

namespace ActualLab.CommandR;

public static class ServiceCollectionExt
{
    [RequiresUnreferencedCode(UnreferencedCode.Commander)]
    public static CommanderBuilder AddCommander(this IServiceCollection services)
        => new(services, null);

    [RequiresUnreferencedCode(UnreferencedCode.Commander)]
    public static IServiceCollection AddCommander(this IServiceCollection services, Action<CommanderBuilder> configure)
        => new CommanderBuilder(services, configure).Services;
}
