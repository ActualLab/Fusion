using ActualLab.CommandR.Commands;

namespace ActualLab.Flows;

public interface IFlowEvent : IApiCommand<Unit>, IBackendCommand
{
    FlowId FlowId { get; init; }
}
