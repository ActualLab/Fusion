namespace ActualLab.Fusion.Extensions;

/// <summary>
/// Defines the high-level connection state kinds for an RPC peer.
/// </summary>
public enum RpcPeerStateKind
{
    Connected = 0,
    JustDisconnected,
    Disconnected,
    JustConnected,
}

/// <summary>
/// A user-friendly representation of an RPC peer's connection state.
/// </summary>
public sealed record RpcPeerState(
    RpcPeerStateKind Kind,
    Exception? LastError = null,
    TimeSpan ReconnectsIn = default)
{
    public bool IsConnected => Kind is RpcPeerStateKind.Connected or RpcPeerStateKind.JustConnected;
    public bool LikelyConnected => Kind != RpcPeerStateKind.Disconnected;

    public string GetDescription(bool useLastError = false)
    {
        switch (Kind) {
        case RpcPeerStateKind.JustConnected:
            return "Just connected.";
        case RpcPeerStateKind.Connected:
            return "Connected.";
        case RpcPeerStateKind.JustDisconnected:
            return "Just disconnected, reconnecting...";
        case RpcPeerStateKind.Disconnected when ReconnectsIn == default:
            return "Reconnecting...";
        }
        if (LastError is null || !useLastError)
            return "Disconnected.";

        var message = LastError.Message.Trim();
        if (!(message.EndsWith('.') || message.EndsWith('!') || message.EndsWith('?')))
            message += ".";
        return "Disconnected: " + message;
    }
}
