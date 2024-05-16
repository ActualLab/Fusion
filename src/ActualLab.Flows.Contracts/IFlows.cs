using ActualLab.CommandR;
using ActualLab.CommandR.Commands;
using ActualLab.CommandR.Configuration;
using ActualLab.CommandR.Operations;
using ActualLab.Fusion;
using ActualLab.Rpc;

namespace ActualLab.Flows;

public interface IFlows : IComputeService, IBackendService
{
    [ComputeMethod]
    Task<(byte[]? Data, long Version)> GetData(FlowId flowId, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<Flow?> Get(FlowId flowId, CancellationToken cancellationToken = default);
    // Not a [ComputeMethod]!
    Task<Flow> GetOrStart(FlowId flowId, CancellationToken cancellationToken = default);
    // Not a [ComputeMethod]!
    Task<long> OnEvent(FlowId flowId, object? @event, CancellationToken cancellationToken = default);

    [CommandHandler]
    Task<long> OnEventData(Flows_EventData command, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task<long> OnSave(Flows_Save command, CancellationToken cancellationToken = default);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
// ReSharper disable once InconsistentNaming
public partial record Flows_EventData(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] Symbol Uuid,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] FlowId FlowId,
    [property: DataMember(Order = 2), MemoryPackOrder(2)] byte[]? EventData
) : IApiCommand<long>, IBackendCommand, IHasUuid, INotLogged;

// ReSharper disable once InconsistentNaming
// This command should always run locally / shouldn't be serializable
public record Flows_Save(
    Flow Flow,
    long? ExpectedVersion = null
) : ICommand<long>, IBackendCommand, INotLogged
{
    public Action<Operation>? EventBuilder { get; init; }
}
