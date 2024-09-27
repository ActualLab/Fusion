using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Internal;
using ActualLab.Resilience;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcSystemCallSender(IServiceProvider services)
    : RpcServiceBase(services)
{
    private IRpcSystemCalls? _client;
    private RpcServiceDef? _serviceDef;
    private RpcMethodDef? _handshakeMethodDef;
    private RpcMethodDef? _okMethodDef;
    private RpcMethodDef? _errorMethodDef;
    private RpcMethodDef? _cancelMethodDef;
    private RpcMethodDef? _matchMethodDef;
    private RpcMethodDef? _notFoundMethodDef;
    private RpcMethodDef? _keepAliveMethodDef;
    private RpcMethodDef? _disconnectMethodDef;
    private RpcMethodDef? _ackMethodDef;
    private RpcMethodDef? _ackEndMethodDef;
    private RpcMethodDef? _itemMethodDef;
    private RpcMethodDef? _batchMethodDef;
    private RpcMethodDef? _endMethodDef;

    public IRpcSystemCalls Client => _client
        ??= Services.GetRequiredService<IRpcSystemCalls>();
    public RpcServiceDef ServiceDef => _serviceDef
        ??= Hub.ServiceRegistry.Get<IRpcSystemCalls>()!;
    public RpcMethodDef HandshakeMethodDef => _handshakeMethodDef
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Handshake)));
    public RpcMethodDef OkMethodDef => _okMethodDef
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Ok)));
    public RpcMethodDef ErrorMethodDef => _errorMethodDef
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Error)));
    public RpcMethodDef CancelMethodDef => _cancelMethodDef
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Cancel)));
    public RpcMethodDef MatchMethodDef => _matchMethodDef
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.M)));
    public RpcMethodDef NotFoundMethodDef => _notFoundMethodDef
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.NotFound)));
    public RpcMethodDef KeepAliveMethodDef => _keepAliveMethodDef
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.KeepAlive)));
    public RpcMethodDef DisconnectMethodDef => _disconnectMethodDef
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Disconnect)));
    public RpcMethodDef AckMethodDef => _ackMethodDef
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Ack)));
    public RpcMethodDef AckEndMethodDef => _ackEndMethodDef
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.AckEnd)));
    public RpcMethodDef ItemMethodDef => _itemMethodDef
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.I)));
    public RpcMethodDef BatchMethodDef => _batchMethodDef
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.B)));
    public RpcMethodDef EndMethodDef => _endMethodDef
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.End)));

    // Handshake

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Handshake(
        RpcPeer peer,
        ChannelWriter<RpcMessage> sender, // Handshake is sent before exposing the Sender, so we pass it directly
        RpcHandshake handshake)
    {
        var context = new RpcOutboundContext(peer);
        var call = context.PrepareCallForSendNoWait(HandshakeMethodDef, ArgumentList.New(handshake))!;
        return call.SendNoWait(false, sender);
    }

    // Regular calls

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Complete<TResult>(
        RpcPeer peer, RpcInboundCall inboundCall, Result<TResult> result,
        bool allowPolymorphism,
        RpcHeader[]? headers = null)
        => result.IsValue(out var value)
            ? Ok(peer, inboundCall, value, allowPolymorphism, headers)
            : Error(peer, inboundCall, result.Error!, headers);

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Ok<TResult>(
        RpcPeer peer, RpcInboundCall inboundCall, TResult result,
        bool allowPolymorphism,
        RpcHeader[]? headers = null)
    {
        try {
            var context = new RpcOutboundContext(peer, inboundCall.Id, headers);
            var call = context.PrepareCallForSendNoWait(OkMethodDef, ArgumentList.New(result))!;
            var inboundHash = inboundCall.Context.Message.Headers.TryGet(RpcHeaderNames.Hash);
            if (inboundHash == null)
                return call.SendNoWait(allowPolymorphism);

            var (message, hash) = call.CreateMessageWithHashHeader(call.Context.RelatedId, allowPolymorphism);
            return string.Equals(hash, inboundHash, StringComparison.Ordinal)
                ? Match(peer, inboundCall.Id, headers)
                : call.SendNoWait(message);
        }
        catch (Exception error) {
            Log.LogError(error, "Failed to send Ok response for call #{CallId}", inboundCall.Id);
            return Error(peer, inboundCall, error, headers);
        }
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Error(
        RpcPeer peer, RpcInboundCall inboundCall, Exception error,
        RpcHeader[]? headers = null)
    {
#if false
        if (error is RpcRerouteException) {
            Log.LogError("Error(...) got RpcRerouteException, which should never happen");
            error = new TaskCanceledException();
        }
#endif
        if (peer.StopToken.IsCancellationRequested) {
            // The peer is stopping, we may omit sending call result here
            var stopMode = RpcPeerStopModeExt.ComputeFor(peer);
            if (stopMode == RpcPeerStopMode.KeepInboundCallsIncomplete) {
                // We must keep all inbound calls incomplete - assuming they're getting aborted with
                // either OperationCanceledException or ObjectDisposedException.
                if (error is OperationCanceledException || error.IsServiceProviderDisposedException())
                    return Task.CompletedTask;
            }
        }

        var context = new RpcOutboundContext(peer, inboundCall.Id, headers);
        var call = context.PrepareCallForSendNoWait(ErrorMethodDef, ArgumentList.New(error.ToExceptionInfo()))!;
        return call.SendNoWait(false);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Cancel(RpcPeer peer, long callId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, callId, headers);
        var call = context.PrepareCallForSendNoWait(CancelMethodDef, ArgumentList.Empty)!;
        return call.SendNoWait(false);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Match(RpcPeer peer, long callId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, callId, headers);
        var call = context.PrepareCallForSendNoWait(MatchMethodDef, ArgumentList.Empty)!;
        return call.SendNoWait(false);
    }

    // Objects

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task KeepAlive(RpcPeer peer, long[] localIds, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, headers);
        var call = context.PrepareCallForSendNoWait(KeepAliveMethodDef, ArgumentList.New(localIds))!;
        return call.SendNoWait(false);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Disconnect(RpcPeer peer, long[] localIds, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, headers);
        var call = context.PrepareCallForSendNoWait(DisconnectMethodDef, ArgumentList.New(localIds))!;
        return call.SendNoWait(false);
    }

    // Streams

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Ack(RpcPeer peer, long localId, long nextIndex, Guid hostId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, localId, headers);
        var call = context.PrepareCallForSendNoWait(AckMethodDef, ArgumentList.New(nextIndex, hostId))!;
        return call.SendNoWait(false);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task AckEnd(RpcPeer peer, long localId, Guid hostId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, localId, headers);
        var call = context.PrepareCallForSendNoWait(AckEndMethodDef, ArgumentList.New(hostId))!;
        return call.SendNoWait(false);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Item<TItem>(RpcPeer peer, long localId, long index, TItem item, int sizeHint, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, localId, headers) { SizeHint = sizeHint };
        using var _ = context.Activate();
        var call = context.PrepareCallForSendNoWait(ItemMethodDef, ArgumentList.New(index, item))!;
        return call.SendNoWait(true);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Batch<TItem>(RpcPeer peer, long localId, long index, TItem[] items, int sizeHint, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, localId, headers) { SizeHint = sizeHint };
        using var _ = context.Activate();
        var call = context.PrepareCallForSendNoWait(BatchMethodDef, ArgumentList.New(index, items))!;
        return call.SendNoWait(true);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task End(RpcPeer peer, long localId, long index, Exception? error, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, localId, headers);
        var call = context.PrepareCallForSendNoWait(EndMethodDef, ArgumentList.New(index, error.ToExceptionInfo()))!;
        return call.SendNoWait(false);
    }
}
