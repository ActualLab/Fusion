using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Diagnostics;

public class RpcCallLogger(RpcPeer peer, ILogger? log, LogLevel logLevel)
{
    public RpcPeer Peer { get; } = peer;
    public Func<RpcPeer, RpcCall, bool> Filter { get; init; } = static (_, _) => true;
    public ILogger? Log { get; } = log.IfEnabled(logLevel);
    public LogLevel LogLevel { get; } = logLevel;

    public bool IsLogged(RpcCall call)
        => Log is not null && Filter.Invoke(Peer, call);

    public virtual void LogInbound(RpcInboundCall call)
        => Log?.Log(LogLevel, "'{PeerRef}': {Call}", Peer.Ref, call);

    public virtual void LogOutbound(RpcOutboundCall call, RpcOutboundMessage message)
    {
        var connectionState = Peer.ConnectionState;
        if (connectionState.IsFinal)
            return;

        var callState = connectionState.Value.IsConnected() ? "" : " - queued";
        Log?.Log(LogLevel, "'{PeerRef}': {Call}{State}", Peer.Ref, call, callState);
    }
}
