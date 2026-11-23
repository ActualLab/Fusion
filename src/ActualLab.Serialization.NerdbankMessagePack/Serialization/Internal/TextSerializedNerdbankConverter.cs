using Nerdbank.MessagePack;
using PolyType;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="TextSerialized{T}"/> subclasses. The legacy
/// MessagePack wire format - dictated by <c>[Key(0)] string Data</c> - is a 1-element array
/// <c>[Data]</c> carrying the serialized text; Nerdbank's default reflection provider would emit
/// <c>{"Data": ...}</c> (map) instead.
/// </summary>
public class TextSerializedNerdbankConverter<T, TSerialized> : MessagePackConverter<TSerialized?>
    where TSerialized : TextSerialized<T>, new()
{
    public override TSerialized? Read(ref MessagePackReader reader, SerializationContext context)
    {
        if (reader.TryReadNil())
            return null;
        var len = reader.ReadArrayHeader();
        if (len != 1)
            throw new MessagePackSerializationException(
                $"Expected 1-element array for {typeof(TSerialized).Name}, got {len}.");
        var data = reader.ReadString() ?? "";
        return new TSerialized { Data = data };
    }

    public override void Write(ref MessagePackWriter writer, in TSerialized? value, SerializationContext context)
    {
        if (value is null) {
            writer.WriteNil();
            return;
        }
        writer.WriteArrayHeader(1);
        writer.Write(value.Data);
    }
}

/// <summary>
/// Applies <see cref="TextSerializedNerdbankConverter{T,TSerialized}"/> to <c>NewtonsoftJsonSerialized&lt;T&gt;</c>,
/// which lives in ActualLab.Serialization.NewtonsoftJson - a package this one doesn't reference.
/// </summary>
public sealed class NewtonsoftJsonSerializedNerdbankConverterFactory : IMessagePackConverterFactory
{
    private const string NewtonsoftJsonSerializedTypeName = "ActualLab.Serialization.NewtonsoftJsonSerialized`1";

    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume serializable types are fully preserved")]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "We assume serializable types are fully preserved")]
#pragma warning disable RCS1242 // IMessagePackConverterFactory declares the parameter as "in"
    public MessagePackConverter? CreateConverter(Type type, ITypeShape? shape, in ConverterContext context)
#pragma warning restore RCS1242
    {
        if (!type.IsGenericType)
            return null;

        var genericType = type.GetGenericTypeDefinition();
        if (!string.Equals(genericType.FullName, NewtonsoftJsonSerializedTypeName, StringComparison.Ordinal))
            return null;

        var valueType = type.GetGenericArguments()[0];
        var converterType = typeof(TextSerializedNerdbankConverter<,>).MakeGenericType(valueType, type);
        return (MessagePackConverter)converterType.CreateInstance();
    }
}
