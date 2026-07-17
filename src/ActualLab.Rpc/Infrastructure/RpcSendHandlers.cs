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

            // The inbound call unregisters itself before its result is sent,
            // so it may be gone by the time a send failure is reported
            var peer = transport.Peer;
            var inboundCall = peer.InboundCalls.Get(message.RelatedId);
            if (inboundCall is not null)
                peer.Hub.SystemCallSender.Error(peer, inboundCall, error);
            peer.Log.LogError(error, "Failed to send Ok response for call #{CallId}", message.RelatedId);
        };

    public static bool IsAutoHandledError(Exception error)
        => error is OperationCanceledException or ChannelClosedException;
}
