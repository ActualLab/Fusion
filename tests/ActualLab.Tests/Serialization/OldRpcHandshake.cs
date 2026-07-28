using MessagePack;

namespace ActualLab.Tests.Serialization;

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record OldRpcHandshake(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] Guid RemotePeerId,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] VersionSet? RemoteApiVersionSet,
    [property: DataMember(Order = 2), MemoryPackOrder(2), Key(2)] Guid RemoteHubId
);

// RpcHandshake as it was before the reconnect Secret was appended - i.e. what a peer that hasn't
// been updated yet writes and reads.

[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record OldRpcHandshake5(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] Guid RemotePeerId,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] VersionSet? RemoteApiVersionSet,
    [property: DataMember(Order = 2), MemoryPackOrder(2), Key(2)] Guid RemoteHubId,
    [property: DataMember(Order = 3), MemoryPackOrder(3), Key(3)] int ProtocolVersion,
    [property: DataMember(Order = 4), MemoryPackOrder(4), Key(4)] int Index
);
