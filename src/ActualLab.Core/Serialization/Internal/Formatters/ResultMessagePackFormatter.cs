using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Serialization.Internal;

public sealed class ResultMessagePackFormatter<T> : IMessagePackFormatter<Result<T>>
{
    public void Serialize(ref MessagePackWriter writer, Result<T> value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(2);
        options.Resolver.GetFormatterWithVerify<T?>().Serialize(ref writer, value.ValueOrDefault, options);
        options.Resolver.GetFormatterWithVerify<ExceptionInfo?>().Serialize(ref writer, value.ExceptionInfo, options);
    }

    public Result<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var count = reader.ReadArrayHeader();
        if (count != 2)
            throw new MessagePackSerializationException($"Expected 2 items for Result<>, but got {count}.");

        var value = options.Resolver.GetFormatterWithVerify<T?>().Deserialize(ref reader, options);
        var exceptionInfo = options.Resolver.GetFormatterWithVerify<ExceptionInfo?>().Deserialize(ref reader, options);
        return new Result<T>(value!, exceptionInfo);
    }
}
