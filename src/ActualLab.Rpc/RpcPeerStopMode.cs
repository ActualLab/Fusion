namespace ActualLab.Rpc;

public enum RpcPeerStopMode
{
    Auto = 0, // DropInboundCalls for any server peer, CancelInboundCalls for any client peer
    KeepInboundCallsIncomplete = 1, // Send nothing for inbound calls
    CancelInboundCalls = 2, // Send cancellation for any inbound call
}

public static class RpcPeerStopModeExt
{
    public static RpcPeerStopMode ComputeFor(RpcPeer peer)
    {
        var stopMode = peer.StopMode;
        if (stopMode == RpcPeerStopMode.Auto)
            stopMode = peer.ComputeAutoStopMode();
        return stopMode;
    }
}
