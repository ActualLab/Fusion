namespace ActualLab.Rpc;

#pragma warning disable SYSLIB0051

[Serializable]
public class RpcReconnectFailedException : Exception
{
    private const string DefaultMessage = "Impossible to (re)connect: the remote host is unreachable.";
    private const string DefaultMessagePrefix = "Impossible to (re)connect:";

    public static RpcReconnectFailedException ReconnectFailed(RpcPeerRef peerRef, Exception? innerException = null)
        => ReconnectFailed(peerRef.GetRemotePartyName(), innerException);
    public static RpcReconnectFailedException ReconnectFailed(string remoteParty = "remote host", Exception? innerException = null)
        => new($"Impossible to (re)connect: the {remoteParty} is unreachable.", innerException);
    public static RpcReconnectFailedException DisconnectedExplicitly(Exception? innerException = null)
        => new("Disconnected.", innerException);
    public static RpcReconnectFailedException StopRequested(Exception? innerException = null)
        => new("Stop requested.", innerException);
    public static RpcReconnectFailedException ClientIsGone(Exception? innerException = null)
        => new("The client is gone.", innerException);
    public static RpcReconnectFailedException Unspecified()
        => new("RpcPeer is stopped w/o a specific error somehow.");

    public RpcReconnectFailedException()
        : this(message: null, innerException: null) { }
    public RpcReconnectFailedException(string? message)
        : this(message, innerException: null) { }
    public RpcReconnectFailedException(Exception? innerException)
        : base(innerException is null
            ? DefaultMessage
            : $"{DefaultMessagePrefix} {innerException.Message}", innerException) { }
    public RpcReconnectFailedException(string? message, Exception? innerException)
        : base(message ?? DefaultMessage, innerException) { }

    [Obsolete("Obsolete")]
    protected RpcReconnectFailedException(SerializationInfo info, StreamingContext context)
        : base(info, context) { }
}
