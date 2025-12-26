using ActualLab.Resilience;

namespace ActualLab.Rpc;

#pragma warning disable SYSLIB0051

[Serializable]
public class RpcRerouteException : OperationCanceledException, ITransientException
{
    private const string DefaultMessage = "RPC call must be re-routed.";
    private const string RerouteInboundMessage = "RPC call must be re-routed: inbound call was routed to a non-local peer.";

    public static RpcRerouteException MustRerouteInbound() => new(RerouteInboundMessage);
    public static RpcRerouteException MustReroute() => new(DefaultMessage);
    public static RpcRerouteException MustReroute(string? reason)
        => new(reason is null ? DefaultMessage : $"RPC call must be re-routed: {reason}.");
    public static RpcRerouteException MustReroute(object peer)
        => new($"'{peer}' is already gone. {DefaultMessage}");
    public static RpcRerouteLimitException TooManyReroutes(int limit)
        => new($"Too many reroutes (limit: {limit}).");

    public RpcRerouteException() : base(DefaultMessage) { }
    public RpcRerouteException(string? message) : base(message ?? DefaultMessage) { }
    public RpcRerouteException(string? message, Exception? innerException) : base(message ?? DefaultMessage, innerException) { }
    public RpcRerouteException(string? message, Exception? innerException, CancellationToken token)
        : base(message ?? DefaultMessage, innerException, token) { }
    public RpcRerouteException(string? message, CancellationToken token) : base(message ?? DefaultMessage, token) { }
    public RpcRerouteException(CancellationToken token) : base(DefaultMessage, token) { }
}

[Serializable]
public class RpcRerouteLimitException : OperationCanceledException
{
    public RpcRerouteLimitException(string message) : base(message) { }
    public RpcRerouteLimitException(string message, Exception? innerException) : base(message, innerException) { }
}
