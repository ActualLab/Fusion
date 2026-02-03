namespace ActualLab.Rpc;

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
