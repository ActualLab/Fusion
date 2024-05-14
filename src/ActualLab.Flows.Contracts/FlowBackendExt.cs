using ActualLab.CommandR;
using ActualLab.Flows.Infrastructure;

namespace ActualLab.Flows;

public static class FlowBackendExt
{
    // Save

    public static Task Save(this IFlows flows,
        Flow flow, long? expectedVersion,
        CancellationToken cancellationToken = default)
        => flows.Save(flow, expectedVersion, true, cancellationToken);

    public static Task Save(this IFlows flows,
        Flow flow, long? expectedVersion, bool isolate,
        CancellationToken cancellationToken = default)
    {
        if (ReferenceEquals(flow, null))
            throw new ArgumentOutOfRangeException(nameof(flow));

        var serializer = flows.GetServices().GetRequiredService<FlowSerializer>();
        var data = serializer.Serialize(flow);
        var command = new FlowBackend_SetData(flow.Id, expectedVersion, data);
        return flows.GetCommander().Call(command, isolate, cancellationToken);
    }

    // Remove

    public static Task Remove(this IFlows flows,
        FlowId flowId, long? expectedVersion,
        CancellationToken cancellationToken = default)
        => flows.Remove(flowId, expectedVersion, true, cancellationToken);

    public static Task Remove(this IFlows flows,
        FlowId flowId, long? expectedVersion, bool isolate,
        CancellationToken cancellationToken = default)
    {
        var command = new FlowBackend_SetData(flowId, expectedVersion, null);
        return flows.GetCommander().Call(command, isolate, cancellationToken);
    }
}
