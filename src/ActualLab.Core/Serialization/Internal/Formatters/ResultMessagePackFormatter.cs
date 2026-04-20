using MessagePack;
using MessagePack.Formatters;

namespace ActualLab.Serialization.Internal;

public sealed class ResultMessagePackFormatter<T> : IMessagePackFormatter<Result<T>>
{
    // NB: T? on an unconstrained generic erases to T at IL level, so SG-generated code
    // for a [Key(n)] T? property also calls GetFormatterWithVerify<T>() (verified via
    // typeof(T?) probe — returns the same type as typeof(T) when T is unconstrained).
    // We match that shape exactly to stay wire-compatible with SG output.
    public void Serialize(ref MessagePackWriter writer, Result<T> value, MessagePackSerializerOptions options)
    {
        writer.WriteArrayHeader(2);
        options.Resolver.GetFormatterWithVerify<T>().Serialize(ref writer, value.ValueOrDefault!, options);
        options.Resolver.GetFormatterWithVerify<ExceptionInfo?>().Serialize(ref writer, value.ExceptionInfo, options);
    }

    public Result<T> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
    {
        var count = reader.ReadArrayHeader();
        if (count != 2)
            throw new MessagePackSerializationException($"Expected 2 items for Result<>, but got {count}.");

        var value = options.Resolver.GetFormatterWithVerify<T>().Deserialize(ref reader, options);
        var exceptionInfo = options.Resolver.GetFormatterWithVerify<ExceptionInfo?>().Deserialize(ref reader, options);
        return new Result<T>(value, exceptionInfo);
    }
}
