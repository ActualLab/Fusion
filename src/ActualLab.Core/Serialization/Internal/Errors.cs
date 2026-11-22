namespace ActualLab.Serialization.Internal;

/// <summary>
/// Internal error factory methods for the Serialization namespace.
/// </summary>
public static class Errors
{
    public static Exception NoSerializer()
        => new NotSupportedException("No serializer provided.");

    public static Exception SerializerKindIsNotRegistered(SerializerKind serializerKind)
        => new NotSupportedException(
            $"Serializer kind '{serializerKind}' is not registered - call "
            + $"SerializerKind.{serializerKind}.Register(...) "
            + "or reference the ActualLab.Serialization.* package providing it.");

    public static Exception UnsupportedSerializedType(Type? type)
        => new SerializationException($"Unsupported type: '{type?.ToString() ?? "null"}'.");
    public static Exception NoCurrentSerializer(Type expectedType, IByteSerializer? current)
        => new InvalidOperationException(current is null
            ? $"No '{expectedType.GetName()}' serializer runs on this thread: values like UniSerialized<T> can be (de)serialized only by Read or Write of an ActualLab serializer."
            : $"No '{expectedType.GetName()}' serializer runs on this thread, '{current.GetType().GetName()}' does: values like UniSerialized<T> can be (de)serialized only by Read or Write of an ActualLab serializer of the same format.");
    public static Exception WrongTypeDecoratorFormat()
        => new SerializationException("Wrong type decorator format.");

    public static Exception RemoteException(ExceptionInfo exceptionInfo)
        => new RemoteException(exceptionInfo, exceptionInfo.Message.NullIfEmpty() ?? "Unknown error.");
}
