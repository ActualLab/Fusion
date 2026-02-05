namespace ActualLab.Rpc.Infrastructure;

// Handler is invoked by the transport after message serialization:
// - With error = null on success
// - With the caught exception on failure

/// <summary>
/// Delegate invoked by <see cref="RpcTransport"/> after message serialization to handle send completion or failure.
/// </summary>
public delegate void RpcTransportSendHandler(RpcTransport transport, RpcOutboundMessage message, Exception? error);

/// <summary>
/// Provides built-in <see cref="RpcTransportSendHandler"/> implementations for common send completion scenarios.
/// </summary>
public static class RpcSendHandlers
{
    // Used when the handler is null
    public static readonly RpcTransportSendHandler Default
        = (transport, _, error) => {
            if (error is null || IsAutoHandledError(error))
                return;

            transport.Peer.Log.LogError(error, "Send failed");
        };

    public static readonly RpcTransportSendHandler PropagateToCall
        = (transport, message, error) => {
            if (error is null || IsAutoHandledError(error))
                return;

            var peer = transport.Peer;
            var call = message.Context.Call;
            call?.SetError(error, context: null);
            peer.Log.LogError(error, "Send failed");
        };

    public static readonly RpcTransportSendHandler PropagateToInboundCall
        = (transport, message, error) => {
            if (error is null || IsAutoHandledError(error))
                return;

            var peer = transport.Peer;
            var inboundCall = peer.InboundCalls[message.RelatedId];
            peer.Hub.SystemCallSender.Error(peer, inboundCall, error);
            peer.Log.LogError(error, "Failed to send Ok response for call #{CallId}", inboundCall.Id);
        };

    public static bool IsAutoHandledError(Exception error)
        => error is OperationCanceledException or ChannelClosedException;
}
