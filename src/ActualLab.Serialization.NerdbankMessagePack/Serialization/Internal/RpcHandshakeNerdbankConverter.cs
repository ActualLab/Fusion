using ActualLab.Rpc.Infrastructure;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="RpcHandshake"/>. Wire shape matches the legacy
/// MessagePack-CSharp <c>[MessagePackObject] + [Key(N)]</c> formatter: a 6-element array of
/// <c>[RemotePeerId, RemoteApiVersionSet, RemoteHubId, ProtocolVersion, Index, Secret]</c>.
/// This is the wire format the TS RPC client emits when calling <c>$sys.Handshake</c>, so it's
/// required for cross-runtime handshake to succeed. A 5-element array (no <c>Secret</c>) is
/// still accepted, since that's what an older peer - including today's TS client - sends.
/// </summary>
public sealed class RpcHandshakeNerdbankConverter : MessagePackConverter<RpcHandshake?>
{
    public override RpcHandshake? Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return null;
        var len = reader.ReadArrayHeader();
        if (len < 5)
            throw new MessagePackSerializationException(
                $"Expected 5+ element array for RpcHandshake, got {len}.");
        var guidConverter = context.GetConverter<Guid>(context.TypeShapeProvider);
        var versionSetConverter = context.GetConverter<VersionSet?>(context.TypeShapeProvider);
        var remotePeerId = guidConverter.Read(ref reader, context);
        var remoteApiVersionSet = versionSetConverter.Read(ref reader, context);
        var remoteHubId = guidConverter.Read(ref reader, context);
        var protocolVersion = reader.ReadInt32();
        var index = reader.ReadInt32();
        var secret = len >= 6 ? reader.ReadString() : null;
        for (var i = 6; i < len; i++)
            reader.Skip(context);
        return new RpcHandshake(remotePeerId, remoteApiVersionSet, remoteHubId, protocolVersion, index, secret);
    }

    public override void Write(ref MessagePackWriter writer, in RpcHandshake? value, SerializationContext context)
    {
        if (value is null) {
            writer.WriteNil();
            return;
        }
        writer.WriteArrayHeader(6);
        var guidConverter = context.GetConverter<Guid>(context.TypeShapeProvider);
        var versionSetConverter = context.GetConverter<VersionSet?>(context.TypeShapeProvider);
        guidConverter.Write(ref writer, value.RemotePeerId, context);
        versionSetConverter.Write(ref writer, value.RemoteApiVersionSet, context);
        guidConverter.Write(ref writer, value.RemoteHubId, context);
        writer.Write(value.ProtocolVersion);
        writer.Write(value.Index);
        writer.Write(value.Secret); // Writes nil when null
    }
}
