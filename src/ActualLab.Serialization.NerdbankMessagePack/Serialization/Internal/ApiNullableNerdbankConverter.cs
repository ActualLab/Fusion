using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="ApiNullable{T}"/>.
/// </summary>
public sealed class ApiNullableNerdbankConverter<T> : MessagePackConverter<ApiNullable<T>>
    where T : struct
{
    public override ApiNullable<T> Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return default;

        var itemConverter = context.GetConverter<T>(context.TypeShapeProvider);
        return itemConverter.Read(ref reader, context);
    }

    public override void Write(ref MessagePackWriter writer, in ApiNullable<T> value, SerializationContext context)
    {
        if (!value.HasValue) {
            writer.WriteNil();
            return;
        }

        var itemConverter = context.GetConverter<T>(context.TypeShapeProvider);
        itemConverter.Write(ref writer, value.ValueOrDefault, context);
    }
}
