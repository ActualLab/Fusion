using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Api.Internal;

public sealed class ApiNullable8MessagePackFormatter<T> : IMessagePackFormatter<ApiNullable8<T>>
    where T : struct
{
    public void Serialize(ref MessagePackWriter writer, ApiNullable8<T> value, MessagePackSerializerOptions options)
    {
        if (!value.HasValue)
            writer.WriteNil();
        else
            options.Resolver.GetFormatterWithVerify<T>().Serialize(ref writer, value.ValueOrDefault, options);
    }

    public ApiNullable8<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        => reader.TryReadNil()
            ? default(ApiNullable8<T>)
            : options.Resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options);
}
