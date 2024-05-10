using System.Diagnostics.CodeAnalysis;
using ActualLab.Internal;

namespace ActualLab.Flows;

public static class ServiceCollectionExt
{
    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static FlowBuilder AddFlows(this IServiceCollection services)
        => new(services, null);

    [RequiresUnreferencedCode(UnreferencedCode.Reflection)]
    public static IServiceCollection AddFlows(this IServiceCollection services, Action<FlowBuilder> configure)
        => new FlowBuilder(services, configure).Services;
}
