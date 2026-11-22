namespace ActualLab.Rpc;

#pragma warning disable SYSLIB0051

/// <summary>
/// Thrown when a client requests a wire format that the server does not support.
/// This is a terminal error — the client should not attempt to reconnect with the same format.
/// </summary>
[Serializable]
public class RpcWireFormatException : Exception
{
    public RpcWireFormatException()
        : this(null)
    { }

    public RpcWireFormatException(string? message)
        : base(message ?? "Unsupported RPC wire format.")
    { }

    public RpcWireFormatException(string message, Exception innerException)
        : base(message, innerException)
    { }

    [Obsolete("Obsolete")]
    protected RpcWireFormatException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    { }
}
