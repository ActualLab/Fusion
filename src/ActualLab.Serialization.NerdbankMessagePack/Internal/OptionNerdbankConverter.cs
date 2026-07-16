using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="Option{T}"/>.
/// </summary>
public sealed class OptionNerdbankConverter<T> : MessagePackConverter<Option<T>>
{
    public override Option<T> Read(ref MessagePackReader reader, SerializationContext context)
    {
        var count = reader.ReadArrayHeader();
        if (count == 0)
            return default;

        var itemConverter = context.GetConverter<T>(context.TypeShapeProvider);
        var result = itemConverter.Read(ref reader, context)!;
        for (var i = 1; i < count; i++)
            reader.Skip(context);
        return result;
    }

    public override void Write(ref MessagePackWriter writer, in Option<T> value, SerializationContext context)
    {
        if (!value.HasValue) {
            writer.WriteArrayHeader(0);
            return;
        }

        writer.WriteArrayHeader(1);
        var itemConverter = context.GetConverter<T>(context.TypeShapeProvider);
        itemConverter.Write(ref writer, value.ValueOrDefault!, context);
    }
}
