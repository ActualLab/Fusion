using ActualLab.Fusion;
using Nerdbank.MessagePack;

namespace ActualLab.Fusion.Internal;

/// <summary>
/// A Nerdbank.MessagePack converter that serializes <see cref="Session"/> as its string identifier.
/// </summary>
public sealed class SessionNerdbankConverter : MessagePackConverter<Session?>
{
    public override Session? Read(ref MessagePackReader reader, SerializationContext context)
        => reader.TryReadNil()
            ? null
            : new(reader.ReadString()!);

    public override void Write(ref MessagePackWriter writer, in Session? value, SerializationContext context)
    {
        if (ReferenceEquals(value, null))
            writer.WriteNil();
        else
            writer.Write(value.Id);
    }
}
