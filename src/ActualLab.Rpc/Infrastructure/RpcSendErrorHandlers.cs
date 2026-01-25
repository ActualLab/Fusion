namespace ActualLab.Rpc.Infrastructure;

public delegate bool RpcSendErrorHandler(
    Exception error, RpcPeer peer, RpcOutboundMessage message, RpcTransport transport);

public static class RpcSendErrorHandlers
{
    public static readonly RpcSendErrorHandler PropagateToCall
        = (error, peer, message, transport) => {
            if (IsAutoHandledError(error))
                return true;

            peer.Log.LogError(error, "Send failed");
            var call = message.Context.Call;
            call?.SetError(error, context: null);
            return true;
        };

    public static readonly RpcSendErrorHandler PropagateToInboundCall
        = (error, peer, message, transport) => {
            if (IsAutoHandledError(error))
                return true;

            var inboundCall = peer.InboundCalls[message.RelatedId];
            peer.Log.LogError(error, "Failed to send Ok response for call #{CallId}", inboundCall.Id);
            _ = peer.Hub.SystemCallSender.Error(peer, inboundCall, error);
            return true;
        };

    public static readonly RpcSendErrorHandler Silence
        = (error, peer, message, transport) => true;

    public static readonly RpcSendErrorHandler LogAndSilence
        = (error, peer, message, transport) => {
            if (IsAutoHandledError(error))
                return true;

            peer.Log.LogError(error, "Send failed");
            return true;
        };

    public static bool IsAutoHandledError(Exception error)
        => error is OperationCanceledException or ChannelClosedException;
}
