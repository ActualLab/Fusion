using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Internal;
using ActualLab.Resilience;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcSystemCallSender(IServiceProvider services)
    : RpcServiceBase(services)
{
    private IRpcSystemCalls? _client;
    private RpcServiceDef? _systemCallsServiceDef;
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
    public RpcServiceDef SystemCallsServiceDef => _systemCallsServiceDef
        ??= Hub.ServiceRegistry.Get<IRpcSystemCalls>()!;
    public RpcMethodDef HandshakeMethodDef => _handshakeMethodDef
        ??= SystemCallsServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Handshake)));
    public RpcMethodDef OkMethodDef => _okMethodDef
        ??= SystemCallsServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Ok)));
    public RpcMethodDef ErrorMethodDef => _errorMethodDef
        ??= SystemCallsServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Error)));
    public RpcMethodDef CancelMethodDef => _cancelMethodDef
        ??= SystemCallsServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Cancel)));
    public RpcMethodDef MatchMethodDef => _matchMethodDef
        ??= SystemCallsServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.M)));
    public RpcMethodDef NotFoundMethodDef => _notFoundMethodDef
        ??= SystemCallsServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.NotFound)));
    public RpcMethodDef KeepAliveMethodDef => _keepAliveMethodDef
        ??= SystemCallsServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.KeepAlive)));
    public RpcMethodDef DisconnectMethodDef => _disconnectMethodDef
        ??= SystemCallsServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Disconnect)));
    public RpcMethodDef AckMethodDef => _ackMethodDef
        ??= SystemCallsServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Ack)));
    public RpcMethodDef AckEndMethodDef => _ackEndMethodDef
        ??= SystemCallsServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.AckEnd)));
    public RpcMethodDef ItemMethodDef => _itemMethodDef
        ??= SystemCallsServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.I)));
    public RpcMethodDef BatchMethodDef => _batchMethodDef
        ??= SystemCallsServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.B)));
    public RpcMethodDef EndMethodDef => _endMethodDef
        ??= SystemCallsServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.End)));

    // Handshake

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Handshake(
        RpcPeer peer,
        ChannelWriter<RpcMessage> sender, // Handshake is sent before exposing the Sender, so we pass it directly
        RpcHandshake handshake,
        RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer);
        var call = context.PrepareCall(HandshakeMethodDef, ArgumentList.New(handshake))!;
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
        var inboundHeaders = inboundCall.Context.Message.Headers;
        inboundHeaders.TryGet(RpcHeaderNames.Hash, out var inboundHashHeader);
        try {
            var context = new RpcOutboundContext(peer, inboundCall.Id, headers);
            var call = context.PrepareCall(OkMethodDef, ArgumentList.New(result))!;
            if (inboundHashHeader.IsNone)
                return call.SendNoWait(allowPolymorphism);

            var message = call.CreateMessage(call.Context.RelatedId, allowPolymorphism, "");
            var isMatch = message.Headers.TryGet(RpcHeaderNames.Hash, out var outboundHashHeader)
                && StringComparer.Ordinal.Equals(inboundHashHeader.Value, outboundHashHeader.Value);
            return isMatch
                ? Match(peer, inboundCall.Id, headers)
                : call.SendNoWait(message);
        }
        catch (Exception error) {
            Log.LogError(error, "PrepareCall failed for call #{CallId}", inboundCall.Id);
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
        var call = context.PrepareCall(ErrorMethodDef, ArgumentList.New(error.ToExceptionInfo()))!;
        return call.SendNoWait(false);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Cancel(RpcPeer peer, long callId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, callId, headers);
        var call = context.PrepareCall(CancelMethodDef, ArgumentList.Empty)!;
        return call.SendNoWait(false);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Match(RpcPeer peer, long callId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, callId, headers);
        var call = context.PrepareCall(MatchMethodDef, ArgumentList.Empty)!;
        return call.SendNoWait(false);
    }

    // Objects

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task KeepAlive(RpcPeer peer, long[] localIds, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, headers);
        var call = context.PrepareCall(KeepAliveMethodDef, ArgumentList.New(localIds))!;
        return call.SendNoWait(false);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Disconnect(RpcPeer peer, long[] localIds, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, headers);
        var call = context.PrepareCall(DisconnectMethodDef, ArgumentList.New(localIds))!;
        return call.SendNoWait(false);
    }

    // Streams

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Ack(RpcPeer peer, long localId, long nextIndex, Guid hostId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, localId, headers);
        var call = context.PrepareCall(AckMethodDef, ArgumentList.New(nextIndex, hostId))!;
        return call.SendNoWait(false);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task AckEnd(RpcPeer peer, long localId, Guid hostId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, localId, headers);
        var call = context.PrepareCall(AckEndMethodDef, ArgumentList.New(hostId))!;
        return call.SendNoWait(false);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Item<TItem>(RpcPeer peer, long localId, long index, TItem item, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, localId, headers);
        var call = context.PrepareCall(ItemMethodDef, ArgumentList.New(index, item))!;
        return call.SendNoWait(true);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task Batch<TItem>(RpcPeer peer, long localId, long index, TItem[] items, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, localId, headers);
        var call = context.PrepareCall(BatchMethodDef, ArgumentList.New(index, items))!;
        return call.SendNoWait(true);
    }

    [RequiresUnreferencedCode(UnreferencedCode.Serialization)]
    public Task End(RpcPeer peer, long localId, long index, Exception? error, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, localId, headers);
        var call = context.PrepareCall(EndMethodDef, ArgumentList.New(index, error.ToExceptionInfo()))!;
        return call.SendNoWait(false);
    }
}
