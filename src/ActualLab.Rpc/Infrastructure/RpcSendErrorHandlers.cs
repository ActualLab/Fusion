namespace ActualLab.Rpc.Infrastructure;

public delegate void RpcSendErrorHandler(Exception error, RpcOutboundMessage message, RpcTransport transport);

public static class RpcSendErrorHandlers
{
    public static readonly RpcSendErrorHandler PropagateToCall
        = (error, message, transport) => {
            if (IsAutoHandledError(error))
                return;

            var peer = transport.Peer;
            var call = message.Context.Call;
            call?.SetError(error, context: null);
            peer.Log.LogError(error, "Send failed");
        };

    public static readonly RpcSendErrorHandler PropagateToInboundCall
        = (error, message, transport) => {
            if (IsAutoHandledError(error))
                return;

            var peer = transport.Peer;
            var inboundCall = peer.InboundCalls[message.RelatedId];
            _ = peer.Hub.SystemCallSender.Error(peer, inboundCall, error);
            peer.Log.LogError(error, "Failed to send Ok response for call #{CallId}", inboundCall.Id);
        };

    public static bool IsAutoHandledError(Exception error)
        => error is OperationCanceledException or ChannelClosedException;
}
