namespace ActualLab.Serialization.Internal;

/// <summary>
/// Internal error factory methods for the Serialization namespace.
/// </summary>
public static class Errors
{
    public static Exception NoSerializer()
        => new NotSupportedException("No serializer provided.");

    public static Exception UnsupportedSerializedType(Type? type)
        => new SerializationException($"Unsupported type: '{type?.ToString() ?? "null"}'.");
    public static Exception SerializedTypeMismatch(Type supportedType, Type requestedType)
        => new NotSupportedException(
            $"The serializer implements '{supportedType}' serialization, but '{requestedType}' was requested to (de)serialize.");
    public static Exception WrongTypeDecoratorFormat()
        => new SerializationException("Wrong type decorator format.");

    public static Exception RemoteException(ExceptionInfo exceptionInfo)
        => new RemoteException(exceptionInfo, exceptionInfo.Message.NullIfEmpty() ?? "Unknown error.");
}
