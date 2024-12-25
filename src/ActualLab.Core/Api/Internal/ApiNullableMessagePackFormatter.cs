using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Api.Internal;

public sealed class ApiNullableMessagePackFormatter<T> : IMessagePackFormatter<ApiNullable<T>>
    where T : struct
{
    public void Serialize(ref MessagePackWriter writer, ApiNullable<T> value, MessagePackSerializerOptions options)
    {
        if (!value.HasValue)
            writer.WriteNil();
        else
            options.Resolver.GetFormatterWithVerify<T>().Serialize(ref writer, value.ValueOrDefault, options);
    }

    public ApiNullable<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => reader.TryReadNil()
            ? default(ApiNullable<T>)
            : options.Resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options);
}
