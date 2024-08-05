namespace ActualLab.Tests.Serialization;

[DataContract, MemoryPackable(GenerateType.VersionTolerant)]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record OldRpcHandshake(
    [property: DataMember(Order = 0), MemoryPackOrder(0)] Guid RemotePeerId,
    [property: DataMember(Order = 1), MemoryPackOrder(1)] VersionSet? RemoteApiVersionSet,
    [property: DataMember(Order = 2), MemoryPackOrder(2)] Guid RemoteHubId
);
