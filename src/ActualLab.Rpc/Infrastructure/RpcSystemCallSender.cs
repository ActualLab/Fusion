using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Resilience;

namespace ActualLab.Rpc.Infrastructure;

public sealed class RpcSystemCallSender(IServiceProvider services)
    : RpcServiceBase(services)
{
    [field: AllowNull, MaybeNull]
    public IRpcSystemCalls Client => field
        ??= Services.GetRequiredService<IRpcSystemCalls>();
    [field: AllowNull, MaybeNull]
    public RpcServiceDef ServiceDef => field
        ??= Hub.ServiceRegistry.Get<IRpcSystemCalls>()!;
    [field: AllowNull, MaybeNull]
    public RpcMethodDef HandshakeMethodDef => field
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Handshake)));
    [field: AllowNull, MaybeNull]
    public RpcMethodDef OkMethodDef => field
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Ok)));
    [field: AllowNull, MaybeNull]
    public RpcMethodDef ErrorMethodDef => field
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Error)));
    [field: AllowNull, MaybeNull]
    public RpcMethodDef CancelMethodDef => field
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Cancel)));

    [field: AllowNull, MaybeNull]
    public RpcMethodDef MatchMethodDef => field
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.M)));
    [field: AllowNull, MaybeNull]
    public RpcMethodDef NotFoundMethodDef => field
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.NotFound)));
    [field: AllowNull, MaybeNull]
    public RpcMethodDef KeepAliveMethodDef => field
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.KeepAlive)));
    [field: AllowNull, MaybeNull]
    public RpcMethodDef DisconnectMethodDef => field
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Disconnect)));
    [field: AllowNull, MaybeNull]
    public RpcMethodDef AckMethodDef => field
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.Ack)));
    [field: AllowNull, MaybeNull]
    public RpcMethodDef AckEndMethodDef => field
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.AckEnd)));
    [field: AllowNull, MaybeNull]
    public RpcMethodDef ItemMethodDef => field
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.I)));
    [field: AllowNull, MaybeNull]
    public RpcMethodDef BatchMethodDef => field
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.B)));
    [field: AllowNull, MaybeNull]
    public RpcMethodDef EndMethodDef => field
        ??= ServiceDef.Methods.Single(m => Equals(m.Method.Name, nameof(IRpcSystemCalls.End)));

    // Handshake

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

    public Task Complete<TResult>(
        RpcPeer peer, RpcInboundCall inboundCall, Result<TResult> result,
        bool allowPolymorphism,
        RpcHeader[]? headers = null)
    {
        var (value, error) = result;
        return error == null
            ? Ok(peer, inboundCall, value, allowPolymorphism, headers)
            : Error(peer, inboundCall, result.Error!, headers);
    }

    public Task Ok<TResult>(
        RpcPeer peer, RpcInboundCall inboundCall, TResult result,
        bool allowPolymorphism,
        RpcHeader[]? headers = null)
    {
        try {
            var context = new RpcOutboundContext(peer, inboundCall.Id, headers);
            var call = context.PrepareCallForSendNoWait(OkMethodDef, ArgumentList.New(result))!;
            var inboundHash = inboundCall.Context.Message.Headers.TryGet(WellKnownRpcHeaders.Hash);
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

    public Task Cancel(RpcPeer peer, long callId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, callId, headers);
        var call = context.PrepareCallForSendNoWait(CancelMethodDef, ArgumentList.Empty)!;
        return call.SendNoWait(false);
    }

    public Task Match(RpcPeer peer, long callId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, callId, headers);
        var call = context.PrepareCallForSendNoWait(MatchMethodDef, ArgumentList.Empty)!;
        return call.SendNoWait(false);
    }

    // Objects

    public Task KeepAlive(RpcPeer peer, long[] localIds, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, headers);
        var call = context.PrepareCallForSendNoWait(KeepAliveMethodDef, ArgumentList.New(localIds))!;
        return call.SendNoWait(false);
    }

    public Task Disconnect(RpcPeer peer, long[] localIds, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, headers);
        var call = context.PrepareCallForSendNoWait(DisconnectMethodDef, ArgumentList.New(localIds))!;
        return call.SendNoWait(false);
    }

    // Streams

    public Task Ack(RpcPeer peer, long localId, long nextIndex, Guid hostId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, localId, headers);
        var call = context.PrepareCallForSendNoWait(AckMethodDef, ArgumentList.New(nextIndex, hostId))!;
        return call.SendNoWait(false);
    }

    public Task AckEnd(RpcPeer peer, long localId, Guid hostId, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, localId, headers);
        var call = context.PrepareCallForSendNoWait(AckEndMethodDef, ArgumentList.New(hostId))!;
        return call.SendNoWait(false);
    }

    public Task Item<TItem>(RpcPeer peer, long localId, long index, TItem item, int sizeHint, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, localId, headers) { SizeHint = sizeHint };
        using var _ = context.Activate();
        var call = context.PrepareCallForSendNoWait(ItemMethodDef, ArgumentList.New(index, item))!;
#pragma warning disable MA0100
        return call.SendNoWait(true);
#pragma warning restore MA0100
    }

    public Task Batch<TItem>(RpcPeer peer, long localId, long index, TItem[] items, int sizeHint, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, localId, headers) { SizeHint = sizeHint };
        using var _ = context.Activate();
        var call = context.PrepareCallForSendNoWait(BatchMethodDef, ArgumentList.New(index, items))!;
#pragma warning disable MA0100
        return call.SendNoWait(true);
#pragma warning restore MA0100
    }

    public Task End(RpcPeer peer, long localId, long index, Exception? error, RpcHeader[]? headers = null)
    {
        var context = new RpcOutboundContext(peer, localId, headers);
        var call = context.PrepareCallForSendNoWait(EndMethodDef, ArgumentList.New(index, error.ToExceptionInfo()))!;
        return call.SendNoWait(false);
    }
}
