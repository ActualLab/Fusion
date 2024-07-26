using ActualLab.Resilience;

namespace ActualLab.Rpc;

#pragma warning disable SYSLIB0051

[Serializable]
public class RpcDisconnectedException : Exception, ITransientException
{
    private const string DefaultMessage = "The remote server is disconnected.";

    public static Exception New(RpcPeer peer)
        => New(peer.Ref.IsServer ? "client" : "server");
    public static Exception New(string peerKind = "server")
        => new RpcDisconnectedException($"The remote {peerKind} is disconnected.");

    public RpcDisconnectedException()
        : this(message: null, innerException: null) { }
    public RpcDisconnectedException(string? message)
        : this(message, innerException: null) { }
    public RpcDisconnectedException(string? message, Exception? innerException)
        : base(message ?? DefaultMessage, innerException) { }

    [Obsolete("Obsolete")]
    protected RpcDisconnectedException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
