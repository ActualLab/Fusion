using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="ByteString"/>.
/// </summary>
public sealed class ByteStringNerdbankConverter : MessagePackConverter<ByteString>
{
    public override ByteString Read(ref MessagePackReader reader, SerializationContext context)
    {
        var seq = reader.ReadBytes();
        if (!seq.HasValue)
            return new([]);

        return new(System.Buffers.BuffersExtensions.ToArray(seq.Value));
    }

    public override void Write(ref MessagePackWriter writer, in ByteString value, SerializationContext context)
        => writer.Write(value.Bytes.Span);
}
