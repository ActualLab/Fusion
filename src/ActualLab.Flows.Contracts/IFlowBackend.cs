using ActualLab.CommandR.Commands;
using ActualLab.CommandR.Configuration;
using ActualLab.Fusion;
using ActualLab.Rpc;

namespace ActualLab.Flows;

public interface IFlowBackend : IComputeService, IBackendService
{
    [ComputeMethod]
    Task<(byte[]? Data, long Version)> GetData(FlowId flowId, CancellationToken cancellationToken = default);

    [CommandHandler]
    Task<long> SetData(FlowBackend_SetData command, CancellationToken cancellationToken = default);
    [CommandHandler]
    Task<long> Resume(FlowBackend_Resume command, CancellationToken cancellationToken = default);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
// ReSharper disable once InconsistentNaming
public partial record FlowBackend_SetData(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] FlowId Id,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] long? ExpectedVersion,
    [property: DataMember(Order = 2), MemoryPackOrder(2)] byte[]? Data
) : IBackendCommand, INotLogged;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[method: JsonConstructor, Newtonsoft.Json.JsonConstructor, MemoryPackConstructor]
// ReSharper disable once InconsistentNaming
public partial record FlowBackend_Resume(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] FlowId Id,
    [property: DataMember(Order = 1), MemoryPackOrder(2)] byte[]? EventData
) : IBackendCommand, INotLogged;
