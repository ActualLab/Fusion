using ActualLab.Interception;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.Serialization;
using Errors = ActualLab.Rpc.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

public interface IRpcSystemCalls : IRpcSystemService
{
    // Handshake & Reconnected
    public Task<RpcNoWait> Handshake(RpcHandshake handshake);
    public Task<byte[]> Reconnect(
        int handshakeIndex, Dictionary<int, byte[]> completedStagesData, CancellationToken cancellationToken);

    // Regular calls
    public Task<RpcNoWait> Ok(object? result);
    public Task<RpcNoWait> Error(ExceptionInfo error);
    public Task<RpcNoWait> Cancel();
    public Task<RpcNoWait> M(); // Match
    public Task<Unit> NotFound(string serviceName, string methodName);

    // Objects
    public Task<RpcNoWait> KeepAlive(long[] localIds);
    public Task<RpcNoWait> Disconnect(long[] localIds);

    // Streams
    public Task<RpcNoWait> Ack(long nextIndex, Guid hostId = default);
    public Task<RpcNoWait> AckEnd(Guid hostId = default);
    public Task<RpcNoWait> I(long index, object? item);
    public Task<RpcNoWait> B(long index, object? items);
    public Task<RpcNoWait> End(long index, ExceptionInfo error);
}

public sealed class RpcSystemCalls(IServiceProvider services)
    : RpcServiceBase(services), IRpcSystemCalls, IRpcPolymorphicArgumentHandler
{
    public const string Name = "$sys";
    public const string OkMethodName = nameof(Ok);
    public const string ItemMethodName = nameof(I);
    public const string BatchMethodName = nameof(B);

    public Task<RpcNoWait> Handshake(RpcHandshake handshake)
        => RpcNoWait.Tasks.Completed; // Does nothing: this call is processed inside RpcPeer.OnRun

    public Task<byte[]> Reconnect(
        int handshakeIndex,
        Dictionary<int, byte[]> completedStagesData,
        CancellationToken cancellationToken)
    {
        var context = RpcInboundContext.GetCurrent();
        var peer = context.Peer;
        var connectionState = peer.ConnectionState.Value;
        if (connectionState.Handshake is not { } handshake || handshake.Index != handshakeIndex)
            throw Errors.TooLateToReconnect();

        CancellationToken readerToken;
        try {
            readerToken = connectionState.ReaderTokenSource!.Token;
        }
        catch (ObjectDisposedException) {
            throw Errors.TooLateToReconnect();
        }

        var unknownCallIds = new HashSet<long>();
        var inboundCalls = peer.InboundCalls;
        foreach (var (completedStage, data) in completedStagesData) {
            var callIds = IncreasingSeqCompressor.Deserialize(data);
            foreach (var callId in callIds) {
                var call = inboundCalls.Get(callId);
                var reprocessTask = call?.TryReprocess(completedStage, readerToken);
                if (reprocessTask is null)
                    unknownCallIds.Add(callId);
            }
        }
        var result = IncreasingSeqCompressor.Serialize(unknownCallIds.OrderBy(x => x));
        return Task.FromResult(result);
    }

    public Task<RpcNoWait> Ok(object? result)
    {
        var context = RpcInboundContext.GetCurrent();
        if (context.RelatedObject is RpcOutboundCall outboundCall) // IsValidCall sets it
            outboundCall.SetResult(result, context);
        return RpcNoWait.Tasks.Completed;
    }

    public Task<RpcNoWait> Error(ExceptionInfo error)
    {
        var context = RpcInboundContext.GetCurrent();
        var peer = context.Peer;
        var outboundCallId = context.Message.RelatedId;
        var exception = error.ToException()!;
        if (exception is RpcRerouteException) {
            exception = Errors.GotRpcRerouteExceptionFromRemotePeer();
            Log.LogError(exception, "Error(...) got RpcRerouteException from remote peer");
        }
        peer.OutboundCalls.Get(outboundCallId)?.SetError(exception, context);
        return RpcNoWait.Tasks.Completed;
    }

    public Task<RpcNoWait> Cancel()
    {
        var context = RpcInboundContext.GetCurrent();
        var peer = context.Peer;
        var inboundCallId = context.Message.RelatedId;
        var inboundCall = peer.InboundCalls.Get(inboundCallId);
        if (inboundCall is not null) {
            peer.Log.IfEnabled(LogLevel.Debug)
                ?.LogDebug("Remote call cancelled on the client side: {Call}", inboundCall);
            inboundCall.Cancel();
        }
        return RpcNoWait.Tasks.Completed;
    }

    public Task<RpcNoWait> M()
    {
        var context = RpcInboundContext.GetCurrent();
        var peer = context.Peer;
        var outboundCallId = context.Message.RelatedId;
        peer.OutboundCalls.Get(outboundCallId)?.SetMatch(context);
        return RpcNoWait.Tasks.Completed;
    }

    public Task<Unit> NotFound(string serviceName, string methodName)
        => throw Errors.EndpointNotFound(serviceName, methodName);

    public async Task<RpcNoWait> KeepAlive(long[] localIds)
    {
        var context = RpcInboundContext.GetCurrent();
        var peer = context.Peer;
        await peer.SharedObjects.KeepAlive(localIds).ConfigureAwait(false);
        return default;
    }

    public Task<RpcNoWait> Disconnect(long[] localIds)
    {
        var context = RpcInboundContext.GetCurrent();
        var peer = context.Peer;
        peer.RemoteObjects.Disconnect(localIds);
        return RpcNoWait.Tasks.Completed;
    }

    public async Task<RpcNoWait> Ack(long nextIndex, Guid hostId = default)
    {
        var context = RpcInboundContext.GetCurrent();
        var peer = context.Peer;
        var localId = context.Message.RelatedId;
        if (peer.SharedObjects.Get(localId) is RpcSharedStream stream)
            await stream.OnAck(nextIndex, hostId).ConfigureAwait(false);
        else
            await peer.Hub.SystemCallSender.Disconnect(peer, [localId]).ConfigureAwait(false);
        return default;
    }

    public Task<RpcNoWait> AckEnd(Guid hostId = default)
        => Ack(long.MaxValue, hostId);

    public Task<RpcNoWait> I(long index, object? item)
    {
        var context = RpcInboundContext.GetCurrent();
        return context.RelatedObject is RpcStream stream // IsValidCall sets it
            ? RpcNoWait.Tasks.From(stream.OnItem(index, item))
            : RpcNoWait.Tasks.Completed;
    }

    public Task<RpcNoWait> B(long index, object? items)
    {
        var context = RpcInboundContext.GetCurrent();
        return context.RelatedObject is RpcStream stream // IsValidCall sets it
            ? RpcNoWait.Tasks.From(stream.OnBatch(index, items))
            : RpcNoWait.Tasks.Completed;
    }

    public Task<RpcNoWait> End(long index, ExceptionInfo error)
    {
        var context = RpcInboundContext.GetCurrent();
        var peer = context.Peer;
        var localId = context.Message.RelatedId;
        return peer.RemoteObjects.Get(localId) is RpcStream stream
            ? RpcNoWait.Tasks.From(stream.OnEnd(index, error.IsNone ? null : error.ToException()))
            : RpcNoWait.Tasks.Completed;
    }

    // IRpcDynamicCallHandler

    public bool IsValidCall(RpcInboundContext context, ref ArgumentList arguments, ref bool needsArgumentPolymorphism)
    {
        var call = context.Call;
        RpcStream? stream;
        var systemCallKind = call.MethodDef.SystemMethodKind;
        if (!systemCallKind.HasPolymorphicResult()) // Most frequent path
            return false;

        // Only Ok, Item, and Batch calls have polymorphic results
        if (systemCallKind == RpcSystemMethodKind.Ok) { // Next frequent path
            var outboundCall = context.Peer.OutboundCalls.Get(context.Message.RelatedId);
            if (outboundCall is null)
                return false;

            context.RelatedObject = outboundCall;
            var outboundMethodDef = outboundCall.MethodDef;
            arguments = outboundMethodDef.ResultListType.Factory.Invoke();
            needsArgumentPolymorphism = outboundMethodDef.HasPolymorphicResult;
            return true;
        }

        if (systemCallKind == RpcSystemMethodKind.Item) {
            stream = context.Peer.RemoteObjects.Get(context.Message.RelatedId) as RpcStream;
            if (stream is null)
                return false;

            context.RelatedObject = stream;
            arguments = stream.CreateStreamItemArguments();
            needsArgumentPolymorphism = RpcArgumentSerializer.IsPolymorphic(stream.ItemType);
            return true;
        }

        // If we're here, systemCallKind == RpcSystemCallKind.Batch
        stream = context.Peer.RemoteObjects.Get(context.Message.RelatedId) as RpcStream;
        if (stream is null)
            return false;

        context.RelatedObject = stream;
        needsArgumentPolymorphism = RpcArgumentSerializer.IsPolymorphic(stream.ItemType);
        arguments = needsArgumentPolymorphism
            // We need to force polymorphic deserialization of the second argument in RpcArgumentSerializer
            // in case TItem is polymorphic. TItem[] is non-abstract & non-object, so RpcArgumentSerializer
            // won't use polymorphic deserialization for the second argument unless we "reset" its type to object.
            ? ArgumentList.New<long, object>(0L, null!)
            : stream.CreateStreamBatchArguments();
        return true;
    }
}
