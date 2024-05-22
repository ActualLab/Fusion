namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable SYSLIB0051

[Serializable]
public class RpcRerouteException : OperationCanceledException
{
    private const string DefaultMessage = "Call will be re-routed to another peer.";

    public RpcRerouteException() : base(DefaultMessage) { }
    public RpcRerouteException(string? message) : base(message ?? DefaultMessage) { }
    public RpcRerouteException(string? message, Exception? innerException) : base(message ?? DefaultMessage, innerException) { }
    public RpcRerouteException(string? message, Exception? innerException, CancellationToken token)
        : base(message ?? DefaultMessage, innerException, token) { }
    public RpcRerouteException(string? message, CancellationToken token) : base(message ?? DefaultMessage, token) { }
    public RpcRerouteException(CancellationToken token) : base(DefaultMessage, token) { }
}
