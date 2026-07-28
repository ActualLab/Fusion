using System.Globalization;
using System.Text;
using ActualLab.Rpc.Internal;
using MessagePack;

namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Serializable handshake data exchanged between RPC peers during connection establishment.
/// </summary>
/// <remarks>
/// Wire-format note: any change to the [Key(N)]/[MessagePackOrder] layout below MUST be mirrored
/// in <c>RpcHandshakeNerdbankConverter</c> (ActualLab.Serialization.NerdbankMessagePack) and in the
/// TS RPC client's handshake sender (<c>rpc-system-call-sender.ts</c>) and receiver
/// (<c>rpc-peer.ts</c>), so the Nerdbank wire stays byte-compatible with MessagePack-CSharp
/// and both runtimes read the same fields.
/// </remarks>
[DataContract, MemoryPackable(GenerateType.VersionTolerant), MessagePackObject]
[Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptOut)]
public sealed partial record RpcHandshake(
    [property: DataMember(Order = 0), MemoryPackOrder(0), Key(0)] Guid RemotePeerId,
    [property: DataMember(Order = 1), MemoryPackOrder(1), Key(1)] VersionSet? RemoteApiVersionSet,
    [property: DataMember(Order = 2), MemoryPackOrder(2), Key(2)] Guid RemoteHubId,
    [property: DataMember(Order = 3), MemoryPackOrder(3), Key(3)] int ProtocolVersion,
    [property: DataMember(Order = 4), MemoryPackOrder(4), Key(4)] int Index,
    // Server -> client only: the per-peer reconnect secret (see RpcReconnectProof).
    // A client always sends null here, and the server ignores whatever a client sends.
    [property: DataMember(Order = 5), MemoryPackOrder(5), Key(5)] string? Secret = null
) {
    public const int MinimumProtocolVersion = 2;
    public const int CurrentProtocolVersion = 2;
    public const int MaxApiVersionSetCount = 16;
    public const int MaxApiVersionSetLength = 512;

    public static void RequireAcceptableApiVersionSet(VersionSet versions)
    {
        // Count is checked first, so Value (which is formatted on demand) is cheap by the time it's read.
        if (versions.Count > MaxApiVersionSetCount)
            throw Errors.ApiVersionSetTooLarge("scope count", versions.Count, MaxApiVersionSetCount);
        if (versions.Value.Length > MaxApiVersionSetLength)
            throw Errors.ApiVersionSetTooLarge("length", versions.Value.Length, MaxApiVersionSetLength);
    }

    // Comparing RemotePeerId alone is safe - and is deliberately NOT hardened further.
    // Every handshake that reaches this method arrived over a connection that already passed
    // RpcReconnectProof.TryVerify at the endpoint (RpcWebSocketServer/RpcHttpServer.Invoke),
    // i.e. proved possession of the server peer's secret. Requiring more here would only
    // degrade every legitimate reconnect to a full Reset(), losing the in-flight inbound calls
    // and SharedObjects streams that Unchanged exists to preserve.
    public RpcPeerChangeKind GetPeerChangeKind(RpcHandshake? lastHandshake)
    {
        if (lastHandshake is null)
            return RpcPeerChangeKind.ChangedToVeryFirst;

        return RemotePeerId == lastHandshake.RemotePeerId
            ? RpcPeerChangeKind.Unchanged
            : RpcPeerChangeKind.Changed;
    }

    private bool PrintMembers(StringBuilder builder)
    {
        // Secret is redacted: RpcCallLogger renders the handshake arguments, so raising
        // RpcPeer.DefaultCallLogLevel would otherwise print it to the log.
        builder.Append(nameof(RemotePeerId)).Append(" = ").Append(RemotePeerId.ToString());
        builder.Append(", ").Append(nameof(RemoteApiVersionSet)).Append(" = ")
            .Append(RemoteApiVersionSet?.ToString());
        builder.Append(", ").Append(nameof(RemoteHubId)).Append(" = ").Append(RemoteHubId.ToString());
        builder.Append(", ").Append(nameof(ProtocolVersion)).Append(" = ")
            .Append(ProtocolVersion.ToString(CultureInfo.InvariantCulture));
        builder.Append(", ").Append(nameof(Index)).Append(" = ")
            .Append(Index.ToString(CultureInfo.InvariantCulture));
        builder.Append(", ").Append(nameof(Secret)).Append(" = ").Append(Secret is null ? "null" : "<redacted>");
        return true;
    }
}
