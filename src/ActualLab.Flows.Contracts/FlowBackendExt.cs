using ActualLab.CommandR;
using ActualLab.Flows.Infrastructure;

namespace ActualLab.Flows;

public static class FlowBackendExt
{
    // Save

    public static Task Save(this IFlowBackend flowBackend,
        Flow flow, long? expectedVersion,
        CancellationToken cancellationToken = default)
        => flowBackend.Save(flow, expectedVersion, true, cancellationToken);

    public static Task Save(this IFlowBackend flowBackend,
        Flow flow, long? expectedVersion, bool isolate,
        CancellationToken cancellationToken = default)
    {
        if (ReferenceEquals(flow, null))
            throw new ArgumentOutOfRangeException(nameof(flow));

        var serializer = flowBackend.GetServices().GetRequiredService<FlowSerializer>();
        var data = serializer.Serialize(flow);
        var command = new FlowBackend_SetData(flow.Id, expectedVersion, data);
        return flowBackend.GetCommander().Call(command, isolate, cancellationToken);
    }

    // Remove

    public static Task Remove(this IFlowBackend flowBackend,
        FlowId flowId, long? expectedVersion,
        CancellationToken cancellationToken = default)
        => flowBackend.Remove(flowId, expectedVersion, true, cancellationToken);

    public static Task Remove(this IFlowBackend flowBackend,
        FlowId flowId, long? expectedVersion, bool isolate,
        CancellationToken cancellationToken = default)
    {
        var command = new FlowBackend_SetData(flowId, expectedVersion, null);
        return flowBackend.GetCommander().Call(command, isolate, cancellationToken);
    }
}
