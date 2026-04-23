using ActualLab.Collections;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="VersionSet"/>. Wire shape matches the legacy
/// MessagePack-CSharp <c>[MessagePackObject] + [Key(0)] string Value</c> formatter: a 1-element
/// array containing the comma-separated value string. Required for cross-runtime wire compatibility
/// (e.g., RpcHandshake.RemoteApiVersionSet field).
/// </summary>
public sealed class VersionSetNerdbankConverter : MessagePackConverter<VersionSet?>
{
    public override VersionSet? Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return null;
        var len = reader.ReadArrayHeader();
        if (len < 1)
            throw new MessagePackSerializationException(
                $"Expected 1+ element array for VersionSet, got {len}.");
        var value = reader.ReadString();
        for (var i = 1; i < len; i++)
            reader.Skip(context);
        return new VersionSet(value);
    }

    public override void Write(ref MessagePackWriter writer, in VersionSet? value, SerializationContext context)
    {
        if (value is null) {
            writer.WriteNil();
            return;
        }
        writer.WriteArrayHeader(1);
        writer.Write(value.Value);
    }
}
