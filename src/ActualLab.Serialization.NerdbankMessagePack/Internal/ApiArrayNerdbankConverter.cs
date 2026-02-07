using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="ApiArray{T}"/>.
/// </summary>
public sealed class ApiArrayNerdbankConverter<T> : MessagePackConverter<ApiArray<T>>
{
    public override ApiArray<T> Read(ref MessagePackReader reader, SerializationContext context)
    {
        var len = reader.ReadArrayHeader();
        if (len == 0)
            return ApiArray<T>.Empty;

        var itemConverter = context.GetConverter<T>(context.TypeShapeProvider);
        var array = new T[len];
        for (var i = 0; i < len; i++)
            array[i] = itemConverter.Read(ref reader, context)!;
        return new ApiArray<T>(array);
    }

    public override void Write(ref MessagePackWriter writer, in ApiArray<T> value, SerializationContext context)
    {
        if (value.IsEmpty) {
            writer.WriteArrayHeader(0);
            return;
        }

        var itemConverter = context.GetConverter<T>(context.TypeShapeProvider);
        writer.WriteArrayHeader(value.Count);
        foreach (var item in value.Items)
            itemConverter.Write(ref writer, item, context);
    }
}
