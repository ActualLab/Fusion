namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable SYSLIB0051

[Serializable]
public class RpcRerouteException : Exception
{
    public RpcRerouteException()
        : this(message: null, innerException: null) { }
    public RpcRerouteException(string? message)
        : this(message, innerException: null) { }
    public RpcRerouteException(Exception? innerException)
        : base(innerException == null
            ? "Call will be re-routed to another peer."
            : $"Call will be re-routed to another peer: {innerException.Message}", innerException) { }
    public RpcRerouteException(string? message, Exception? innerException)
        : base(message ?? "Call will be re-routed to another peer.", innerException) { }

    [Obsolete("Obsolete")]
    protected RpcRerouteException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
