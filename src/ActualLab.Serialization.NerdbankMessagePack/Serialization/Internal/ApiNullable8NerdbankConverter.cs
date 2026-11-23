using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="ApiNullable8{T}"/>.
/// </summary>
public sealed class ApiNullable8NerdbankConverter<T> : MessagePackConverter<ApiNullable8<T>>
    where T : struct
{
    public override ApiNullable8<T> Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return default;

        var itemConverter = context.GetConverter<T>(context.TypeShapeProvider);
        return itemConverter.Read(ref reader, context);
    }

    public override void Write(ref MessagePackWriter writer, in ApiNullable8<T> value, SerializationContext context)
    {
        if (!value.HasValue) {
            writer.WriteNil();
            return;
        }

        var itemConverter = context.GetConverter<T>(context.TypeShapeProvider);
        itemConverter.Write(ref writer, value.ValueOrDefault, context);
    }
}
