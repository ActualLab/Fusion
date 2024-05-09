using ActualLab.CommandR;
using ActualLab.Flows.Infrastructure;
using ActualLab.IO;

namespace ActualLab.Flows;

public static class FlowBackendExt
{
    // Get

    public static async Task<Flow?> Get(this IFlowBackend flowBackend,
        FlowId flowId,
        CancellationToken cancellationToken = default)
    {
        var (data, version) = await flowBackend.GetData(flowId, cancellationToken).ConfigureAwait(false);
        if (data == null || data.Length == 0)
            return null;

        var serializer = flowBackend.GetServices().GetRequiredService<FlowSerializer>();
        var flow = serializer.Deserialize(data);
        flow.Initialize(flowId, version);
        return flow;
    }

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
