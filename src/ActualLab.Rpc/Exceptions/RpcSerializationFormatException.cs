namespace ActualLab.Rpc;

#pragma warning disable SYSLIB0051

/// <summary>
/// Thrown when a client requests a serialization format that the server does not support.
/// This is a terminal error — the client should not attempt to reconnect with the same format.
/// </summary>
[Serializable]
public class RpcSerializationFormatException : Exception
{
    public RpcSerializationFormatException()
        : this(null)
    { }

    public RpcSerializationFormatException(string? message)
        : base(message ?? "Unsupported RPC serialization format.")
    { }

    public RpcSerializationFormatException(string message, Exception innerException)
        : base(message, innerException)
    { }

    [Obsolete("Obsolete")]
    protected RpcSerializationFormatException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    { }
}
