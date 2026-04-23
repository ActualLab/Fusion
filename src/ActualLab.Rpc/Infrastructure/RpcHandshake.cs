using MessagePack;

namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Serializable handshake data exchanged between RPC peers during connection establishment.
/// </summary>
/// <remarks>
/// Wire-format note: any change to the [Key(N)]/[MessagePackOrder] layout below MUST be mirrored
/// in <c>RpcHandshakeNerdbankConverter</c> (ActualLab.Serialization.NerdbankMessagePack) so the
/// Nerdbank wire stays byte-compatible with MessagePack-CSharp and the TS RPC client.
/// </remarks>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record RpcHandshake(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] Guid RemotePeerId,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] VersionSet? RemoteApiVersionSet,
    [property: DataMember(Order = 2), MemoryPackOrder(2), Key(2)] Guid RemoteHubId,
    [property: DataMember(Order = 3), MemoryPackOrder(3), Key(3)] int ProtocolVersion,
    [property: DataMember(Order = 4), MemoryPackOrder(4), Key(4)] int Index
) {
    public const int CurrentProtocolVersion = 2;

    public RpcPeerChangeKind GetPeerChangeKind(RpcHandshake? lastHandshake)
    {
        if (lastHandshake is null)
            return RpcPeerChangeKind.ChangedToVeryFirst;

        return RemotePeerId == lastHandshake.RemotePeerId
            ? RpcPeerChangeKind.Unchanged
            : RpcPeerChangeKind.Changed;
    }
}
