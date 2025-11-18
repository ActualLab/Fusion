using ActualLab.Resilience;

namespace ActualLab.Rpc;

#pragma warning disable SYSLIB0051

[Serializable]
public class RpcRerouteException : OperationCanceledException, ITransientException
{
    private const string DefaultMessage = "Call must be re-routed to another RPC peer.";
    private const string ReroutePreroutedMessage = "Pre-routed call must be re-routed (resolved to a local peer).";
    private const string RerouteToLocalMessage = "Call must be re-routed to local service.";

    public static RpcRerouteException MustRerouteToLocal() => new(RerouteToLocalMessage);
    public static RpcRerouteException MustReroutePrerouted() => new(ReroutePreroutedMessage);
    public static RpcRerouteException MustReroute() => new(DefaultMessage);
    public static RpcRerouteException MustReroute(object peer)
        => new($"'{peer}' is already gone. {DefaultMessage}");

    public RpcRerouteException() : base(DefaultMessage) { }
    public RpcRerouteException(string? message) : base(message ?? DefaultMessage) { }
    public RpcRerouteException(string? message, Exception? innerException) : base(message ?? DefaultMessage, innerException) { }
    public RpcRerouteException(string? message, Exception? innerException, CancellationToken token)
        : base(message ?? DefaultMessage, innerException, token) { }
    public RpcRerouteException(string? message, CancellationToken token) : base(message ?? DefaultMessage, token) { }
    public RpcRerouteException(CancellationToken token) : base(DefaultMessage, token) { }
}
