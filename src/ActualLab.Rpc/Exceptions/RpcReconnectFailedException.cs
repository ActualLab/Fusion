namespace ActualLab.Rpc;

#pragma warning disable SYSLIB0051

[Serializable]
public class RpcReconnectFailedException : Exception
{
    private const string DefaultMessage = "Impossible to (re)connect: remote host is unreachable.";
    private const string DefaultMessagePrefix = "Impossible to (re)connect:";

    public static RpcReconnectFailedException StopRequested(Exception? innerException = null)
        => new("Stop requested.", innerException);
    public static RpcReconnectFailedException ClientIsGone(Exception? innerException = null)
        => new("The client is gone.", innerException);

    public RpcReconnectFailedException()
        : this(message: null, innerException: null) { }
    public RpcReconnectFailedException(string? message)
        : this(message, innerException: null) { }
    public RpcReconnectFailedException(Exception? innerException)
        : base(innerException == null
            ? DefaultMessage
            : $"{DefaultMessagePrefix} {innerException.Message}", innerException) { }
    public RpcReconnectFailedException(string? message, Exception? innerException)
        : base(message ?? DefaultMessage, innerException) { }

    [Obsolete("Obsolete")]
    protected RpcReconnectFailedException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
