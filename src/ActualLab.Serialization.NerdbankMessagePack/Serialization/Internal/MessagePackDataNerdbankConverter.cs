using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="MessagePackData"/>.
/// </summary>
public sealed class MessagePackDataNerdbankConverter : MessagePackConverter<MessagePackData>
{
    public override MessagePackData Read(ref MessagePackReader reader, SerializationContext context)
    {
        var seq = reader.ReadBytes();
        if (!seq.HasValue)
            return new(null!);

        return new(System.Buffers.BuffersExtensions.ToArray(seq.Value));
    }

    public override void Write(ref MessagePackWriter writer, in MessagePackData value, SerializationContext context)
        => writer.Write(value.Data);
}
