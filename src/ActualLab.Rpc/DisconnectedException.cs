using ActualLab.Resilience;

namespace ActualLab.Rpc;

#pragma warning disable SYSLIB0051

[Serializable]
public class DisconnectedException : Exception, ITransientException
{
    public DisconnectedException()
        : this(message: null, innerException: null) { }
    public DisconnectedException(string? message)
        : this(message, innerException: null) { }
    public DisconnectedException(string? message, Exception? innerException)
        : base(message ?? "The server connection is offline.", innerException) { }

    [Obsolete("Obsolete")]
    protected DisconnectedException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
