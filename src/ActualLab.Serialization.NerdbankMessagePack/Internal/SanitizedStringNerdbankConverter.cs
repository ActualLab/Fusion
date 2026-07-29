using ActualLab.Compliance;
using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="SanitizedString{TSanitizer}"/> - writes the raw
/// value exactly as a <see cref="string"/> would be written.
/// </summary>
public sealed class SanitizedStringNerdbankConverter<TSanitizer> : MessagePackConverter<SanitizedString<TSanitizer>>
    where TSanitizer : Sanitizer, new()
{
    public override SanitizedString<TSanitizer> Read(ref MessagePackReader reader, SerializationContext context)
        => new(reader.ReadString());

    public override void Write(
        ref MessagePackWriter writer, in SanitizedString<TSanitizer> value, SerializationContext context)
        // Not value.ToString(): that renders the masked form while sanitization is active
        => writer.Write(value.Value);
}
