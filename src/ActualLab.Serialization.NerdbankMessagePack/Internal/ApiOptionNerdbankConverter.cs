using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="ApiOption{T}"/>.
/// </summary>
public sealed class ApiOptionNerdbankConverter<T> : MessagePackConverter<ApiOption<T>>
{
    public override ApiOption<T> Read(ref MessagePackReader reader, SerializationContext context)
    {
        var count = reader.ReadArrayHeader();
        if (count == 0)
            return default;

        var itemConverter = context.GetConverter<T>(context.TypeShapeProvider);
        return itemConverter.Read(ref reader, context)!;
    }

    public override void Write(ref MessagePackWriter writer, in ApiOption<T> value, SerializationContext context)
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
