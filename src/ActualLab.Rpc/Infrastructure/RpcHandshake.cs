namespace ActualLab.Rpc.Infrastructure;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record RpcHandshake(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] Guid RemotePeerId,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] VersionSet? RemoteApiVersionSet,
    [property: DataMember(Order = 2), MemoryPackOrder(2)] Guid RemoteHubId,
    [property: DataMember(Order = 3), MemoryPackOrder(3)] int ProtocolVersion
) {
    public const int CurrentProtocolVersion = 1;
}
