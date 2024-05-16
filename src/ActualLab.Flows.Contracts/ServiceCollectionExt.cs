using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Flows;

public static class ServiceCollectionExt
{
    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static FlowsBuilder AddFlows(this IServiceCollection services)
        => new(services, null);

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static IServiceCollection AddFlows(this IServiceCollection services, Action<FlowsBuilder> configure)
        => new FlowsBuilder(services, configure).Services;
}
