using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public class RpcCallLogger(RpcPeer peer, RpcCallLoggerFilter filter, ILogger? log, LogLevel logLevel)
{
    public RpcPeer Peer { get; } = peer;
    public RpcCallLoggerFilter Filter { get; } = filter;
    public ILogger? Log { get; } = log.IfEnabled(logLevel);
    public LogLevel LogLevel { get; } = logLevel;

    public bool IsLogged(RpcCall call)
        => Log != null && Filter.Invoke(Peer, call);

    public virtual void LogInbound(RpcInboundCall call)
        => Log?.Log(LogLevel, "'{PeerRef}': <- {Call}", Peer.Ref, call);

    public virtual void LogOutbound(RpcOutboundCall call, RpcMessage message)
    {
        if (!call.ServiceDef.IsSystem)
            Log?.Log(LogLevel, "'{PeerRef}': -> {Call}", Peer.Ref, call);
        else
            Log?.Log(LogLevel, "'{PeerRef}': -> {Call} to #{RelatedId}", Peer.Ref, call, message.RelatedId);
    }
}
