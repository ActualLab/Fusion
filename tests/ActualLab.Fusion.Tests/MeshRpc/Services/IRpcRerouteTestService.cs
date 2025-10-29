namespace ActualLab.Fusion.Tests.MeshRpc;

public interface IRpcRerouteTestService : IComputeService
{
    [ComputeMethod]
    public Task<ValueWithHostId> GetValue(int shardKey, string key, CancellationToken cancellationToken = default);

    public Task<ValueWithHostId> GetValueDirect(int shardKey, string key, CancellationToken cancellationToken = default);

    [CommandHandler]
    public Task<ValueWithHostId> SetValue(RpcRerouteTestService_SetValue command, CancellationToken cancellationToken = default);
}

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
public sealed partial record ValueWithHostId(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] string Value,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] string HostId
);

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
// ReSharper disable once InconsistentNaming
public sealed partial record RpcRerouteTestService_SetValue(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] int ShardKey,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] string Key,
    [property: DataMember(Order = 2), MemoryPackOrder(2)] string Value
) : ICommand<ValueWithHostId>, IHasShardKey;
