namespace ActualLab.Rpc;

/// <summary>
/// Thrown when an RPC stream cannot be found or has been disconnected.
/// </summary>
[Serializable]
public class RpcStreamNotFoundException : Exception
{
    private const string DefaultMessage = "RpcStream not found or disconnected.";

    public RpcStreamNotFoundException()
        : this(message: null, innerException: null) { }
    public RpcStreamNotFoundException(string? message)
        : this(message, innerException: null) { }
    public RpcStreamNotFoundException(string? message, Exception? innerException)
        : base(message ?? DefaultMessage, innerException) { }

    [Obsolete("Obsolete")]
    protected RpcStreamNotFoundException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
