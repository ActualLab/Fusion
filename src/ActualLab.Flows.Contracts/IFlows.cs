using ActualLab.CommandR;
using ActualLab.CommandR.Commands;
using ActualLab.CommandR.Configuration;
using ActualLab.CommandR.Operations;
using ActualLab.Flows.Infrastructure;
using ActualLab.Fusion;
using ActualLab.Rpc;

namespace ActualLab.Flows;

public interface IFlows : IComputeService, IBackendService
{
    [ComputeMethod]
    Task<FlowData> GetData(FlowId flowId, CancellationToken cancellationToken = default);
    [ComputeMethod]
    Task<Flow?> Get(FlowId flowId, CancellationToken cancellationToken = default);
    // Not a [ComputeMethod]!
    Task<Flow> GetOrStart(FlowId flowId, CancellationToken cancellationToken = default);

    // Not a [ComputeMethod]!
    Task<long> OnEvent(FlowId flowId, object? evt, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task<long> OnEvent(Flows_EventData command, CancellationToken cancellationToken = default);

    [CommandHandler]
    Task<long> OnStore(Flows_Store command, CancellationToken cancellationToken = default);
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
[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
public partial record Flows_Store(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] FlowId FlowId,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] long? ExpectedVersion = null
) : ICommand<long>, IBackendCommand, INotLogged
{
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public Flow? Flow { get; init; }
    [JsonIgnore, Newtonsoft.Json.JsonIgnore, IgnoreDataMember, MemoryPackIgnore]
    public Action<Operation>? EventBuilder { get; init; }
}
