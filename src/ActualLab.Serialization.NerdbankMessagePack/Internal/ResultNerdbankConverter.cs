using Nerdbank.MessagePack;

namespace ActualLab.Serialization.Internal;

/// <summary>
/// Nerdbank.MessagePack converter for <see cref="Result{T}"/>. Wire shape matches
/// <see cref="ResultMessagePackFormatter{T}"/> (the MessagePack-CSharp custom formatter):
/// a 2-element array <c>[ValueOrDefault, ExceptionInfo?]</c>.
/// </summary>
public sealed class ResultNerdbankConverter<T> : MessagePackConverter<Result<T>>
{
    public override Result<T> Read(ref MessagePackReader reader, SerializationContext context)
    {
        var len = reader.ReadArrayHeader();
        if (len < 2)
            throw new MessagePackSerializationException(
                $"Expected 2+ element array for Result<>, got {len}.");
        var valueConverter = context.GetConverter<T>(context.TypeShapeProvider);
        var exceptionInfoConverter = context.GetConverter<ExceptionInfo?>(context.TypeShapeProvider);
        var value = valueConverter.Read(ref reader, context);
        var exceptionInfo = exceptionInfoConverter.Read(ref reader, context);
        for (var i = 2; i < len; i++)
            reader.Skip(context);
        return new Result<T>(value!, exceptionInfo);
    }

    public override void Write(ref MessagePackWriter writer, in Result<T> value, SerializationContext context)
    {
        writer.WriteArrayHeader(2);
        var valueConverter = context.GetConverter<T>(context.TypeShapeProvider);
        var exceptionInfoConverter = context.GetConverter<ExceptionInfo?>(context.TypeShapeProvider);
        valueConverter.Write(ref writer, value.ValueOrDefault!, context);
        exceptionInfoConverter.Write(ref writer, value.ExceptionInfo, context);
    }
}
