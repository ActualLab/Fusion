using ActualLab.CommandR;

namespace ActualLab.Flows;

public static class FlowsExt
{
    public static async Task<TFlow> GetOrStart<TFlow>(this IFlows flows,
        string arguments,
        CancellationToken cancellationToken = default)
        where TFlow : Flow
    {
        var services = flows.GetServices();
        var flowRegistry = services.GetRequiredService<FlowRegistry>();
        var name = flowRegistry.Names[typeof(TFlow)];
        var flowId = new FlowId(name, arguments);
        var flow = await flows.GetOrStart(flowId, cancellationToken).ConfigureAwait(false);
        return (TFlow)flow;
    }
}
