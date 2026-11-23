using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Api.Internal;

// Excluded from the generated resolver: it would build this formatter with MakeGenericType, which
// can't be analyzed for trimming because of the struct constraint. [MessagePackFormatter] still finds it.

/// <summary>
/// MessagePack formatter for <see cref="ApiNullable8{T}"/>.
/// </summary>
#if NET8_0_OR_GREATER // MessagePack v3 (source-generated resolvers) is used on .NET 8+ only
[ExcludeFormatterFromSourceGeneratedResolver]
#endif
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
