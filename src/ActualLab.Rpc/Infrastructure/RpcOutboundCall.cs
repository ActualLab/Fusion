using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ActualLab.OS;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Internal;
using Cysharp.Text;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcOutboundCall(RpcOutboundContext context)
    : RpcCall(context.MethodDef!)
{
    private static readonly ConcurrentDictionary<RpcCallTypeKey, Func<RpcOutboundContext, RpcOutboundCall>> FactoryCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    public override string DebugTypeName => "->";

    public readonly RpcOutboundContext Context = context;
    public readonly RpcPeer Peer = context.Peer!;
    public readonly RpcCacheInfoCaptureMode CacheInfoCaptureMode = context.CacheInfoCapture?.CaptureMode ?? default;
    public abstract Task UntypedResultTask { get; }
    public virtual int CompletedStage
        => UntypedResultTask.IsCompleted
            ? RpcCallStage.ResultReady | RpcCallStage.Unregistered
            : 0;
    public string CompletedStageName => RpcCallStage.GetName(CompletedStage);

    public CpuTimestamp StartedAt;
    public CancellationTokenRegistration CallCancelHandler;

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static RpcOutboundCall? New(RpcOutboundContext context)
    {
        var peer = context.Peer;
        if (peer == null)
            throw Errors.InternalError("context.Peer == null.");

        if (peer.ConnectionKind == RpcPeerConnectionKind.Local)
            return null;

        return FactoryCache.GetOrAdd(new(context.CallTypeId, context.MethodDef!.UnwrappedReturnType), static key => {
            var (callTypeId, tResult) = key;
            var type = RpcCallTypeRegistry.Resolve(callTypeId)
                .OutboundCallType
                .MakeGenericType(tResult);
            return (Func<RpcOutboundContext, RpcOutboundCall>)type.GetConstructorDelegate(typeof(RpcOutboundContext))!;
        }).Invoke(context);
    }

    public override string ToString()
    {
        var context = Context;
        var headers = context.Headers.OrEmpty();
        var arguments = context.Arguments;
        var methodDef = context.MethodDef;

        var isStream = methodDef?.IsStream == true;
        var relatedId = context.RelatedId;
        var result = ZString.Concat(
            DebugTypeName,
            isStream ? " ~" : " #",
            relatedId != 0 ? relatedId : Id,
            ' ',
            methodDef?.FullName.Value ?? "n/a",
            arguments?.ToString() ?? "(n/a)",
            headers.Length > 0 ? $", Headers: {headers.ToDelimitedString()}" : "");
        var completedStageName = CompletedStageName;
        if (!completedStageName.IsNullOrEmpty())
            result += $": {completedStageName}";
        return result;
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public void Register()
    {
        Peer.OutboundCalls.Register(this);
        if (CallCancelHandler == default)
            CallCancelHandler = Context.CallCancelToken.Register(static state => {
                var call = (RpcOutboundCall)state!;
                call.Cancel(call.Context.CallCancelToken);
            }, this, useSynchronizationContext: false);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public void RegisterCacheKeyOnly()
    {
        using var _ = Context.Activate(); // CreateMessage may use it
        var message = CreateMessage(Id, MethodDef.AllowArgumentPolymorphism);
        Context.CacheInfoCapture?.CaptureKey(Context, message);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public Task SendNoWait(bool allowPolymorphism, ChannelWriter<RpcMessage>? sender = null)
    {
        // NoWait calls don't require RpcOutboundContext.Current to serialize their arguments,
        // so no Context.Activate() call here.
        var message = CreateMessage(Context.RelatedId, allowPolymorphism);
        if (Peer.CallLogger.IsLogged(this))
            Peer.CallLogger.LogOutbound(this, message);
        return Peer.Send(message, sender);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public Task SendNoWait(RpcMessage message, ChannelWriter<RpcMessage>? sender = null)
    {
        if (Peer.CallLogger.IsLogged(this))
            Peer.CallLogger.LogOutbound(this, message);
        return Peer.Send(message, sender);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public Task SendRegistered(bool isFirstAttempt, ChannelWriter<RpcMessage>? sender = null)
    {
        RpcMessage message;
        var context = Context;
        var scope = context.Activate(); // CreateMessage may use it
        try {
            var cacheInfoCapture = context.CacheInfoCapture;
            var hash = cacheInfoCapture?.CacheEntry?.Value.Hash;
            var activity = context.Trace?.Activity;
            message = CreateMessage(Id, MethodDef.AllowArgumentPolymorphism, hash, activity);
            cacheInfoCapture?.CaptureKey(context, message);
        }
        catch (Exception error) {
            SetError(error, context: null, assumeCancelled: isFirstAttempt);
            return Task.CompletedTask;
        }
        finally {
            scope.Dispose();
        }
        if (Peer.CallLogger.IsLogged(this))
            Peer.CallLogger.LogOutbound(this, message);
        return Peer.Send(message, sender);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public RpcMessage CreateMessage(long relatedId, bool allowPolymorphism, string? hash = null, Activity? activity = null)
    {
        var arguments = Context.Arguments!;
        var argumentData = Peer.ArgumentSerializer.Serialize(arguments, allowPolymorphism, Context.SizeHint);
        var headers = Context.Headers;
        if (hash != null)
            headers = headers.With(new(WellKnownRpcHeaders.Hash, hash));
        if (activity != null)
            headers = RpcActivityInjector.Inject(headers, activity.Context);
        return new RpcMessage(Context.CallTypeId, relatedId, MethodDef.Ref, argumentData, headers);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public (RpcMessage Message, string Hash) CreateMessageWithHashHeader(long relatedId, bool allowPolymorphism)
    {
        var arguments = Context.Arguments!;
        var argumentData = Peer.ArgumentSerializer.Serialize(arguments, allowPolymorphism, Context.SizeHint);
        var hash = Peer.HashProvider.Invoke(argumentData);
        var headers = Context.Headers.With(new(WellKnownRpcHeaders.Hash, hash));
        var message = new RpcMessage(Context.CallTypeId, relatedId, MethodDef.Ref, argumentData, headers);
        return (message, hash);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public abstract void SetResult(object? result, RpcInboundContext? context);
    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public abstract void SetMatch(RpcInboundContext context);
    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public abstract void SetError(Exception error, RpcInboundContext? context, bool assumeCancelled = false);
    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public abstract bool Cancel(CancellationToken cancellationToken);

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public virtual int? GetReconnectStage(bool isPeerChanged)
    {
        lock (Lock) {
            var completedStage = CompletedStage;
            if ((completedStage & RpcCallStage.Unregistered) != 0 || ServiceDef.Type == typeof(IRpcSystemCalls))
                return null;

            StartedAt = CpuTimestamp.Now;
            return completedStage;
        }
    }

    public void CompleteKeepRegistered()
    {
        if (!Peer.OutboundCalls.CompleteKeepRegistered(this))
            return;

        CallCancelHandler.Dispose();
        Context.Trace?.Complete(this);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public void CompleteAndUnregister(bool notifyCancelled)
    {
        if (NoWait)
            throw Errors.InternalError("This method should never be called for NoWait calls.");

        if (Peer.OutboundCalls.Unregister(this)) {
            CompleteKeepRegistered();
            if (notifyCancelled)
                NotifyCancelled();
        }
    }

    // Protected methods

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    protected void NotifyCancelled()
    {
        if (Context.CacheInfoCapture is { CaptureMode: RpcCacheInfoCaptureMode.KeyOnly })
            return; // The call was never sent, so no need for cancellation notification

        try {
            var systemCallSender = Peer.Hub.InternalServices.SystemCallSender;
            _ = systemCallSender.Cancel(Peer, Id);
        }
        catch {
            // It's totally fine to ignore any error here:
            // peer.Hub might be already disposed at this point,
            // so SystemCallSender might not be available.
            // In any case, peer on the other side is going to
            // be gone as well after that, so every call there
            // will be cancelled anyway.
        }
    }

    public bool IsPeerChanged()
        => Peer != MethodDef.Hub.CallRouter.Invoke(MethodDef, Context.Arguments!);

    public void SetRerouteError()
    {
        var error = RpcRerouteException.MustReroute();
        // This SetError call not only sets the error, but also
        // invalidates computed method calls awaiting the invalidation.
        // See RpcOutboundComputeCall.SetError / when it calls SetInvalidatedUnsafe.
        SetError(error, context: null, assumeCancelled: true);
    }
}

public class RpcOutboundCall<TResult> : RpcOutboundCall
{
    private static readonly TaskCompletionSource<TResult> CompletedDefaultResultSource
        = new TaskCompletionSource<TResult>().WithResult(default!);

    protected readonly TaskCompletionSource<TResult> ResultSource;

    public override Task UntypedResultTask => ResultSource.Task;
    public Task<TResult> ResultTask => ResultSource.Task;

    public RpcOutboundCall(RpcOutboundContext context) : base(context)
    {
        ResultSource = NoWait || CacheInfoCaptureMode == RpcCacheInfoCaptureMode.KeyOnly
            ? CompletedDefaultResultSource
            : new TaskCompletionSource<TResult>();
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public Task<TResult> Invoke(bool assumeConnected)
    {
        if (CacheInfoCaptureMode == RpcCacheInfoCaptureMode.KeyOnly) {
            RegisterCacheKeyOnly();
            return ResultTask;
        }

        if (NoWait) {
            // NoWait always means "send immediately, even if disconnected"
            _ = SendNoWait(MethodDef.AllowArgumentPolymorphism);
            return ResultTask;
        }

        Register();
        var sender = (ChannelWriter<RpcMessage>?)null;
        if (assumeConnected || Peer.IsConnected(out _, out sender)) {
            _ = SendRegistered(true, sender); // Fast path
            return ResultTask;
        }
        return CompleteAsync(); // Slow path

        async Task<TResult> CompleteAsync() {
            try {
                var (_, sender1) = await Peer
                    .WhenConnected(MethodDef.Timeouts.ConnectTimeout, Context.CallCancelToken)
                    .ConfigureAwait(false);
                _ = SendRegistered(true, sender1);
            }
            catch (Exception error) {
                SetError(error, null);
            }
            return await ResultTask.ConfigureAwait(false);
        }
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public override void SetResult(object? result, RpcInboundContext? context)
    {
        var typedResult = default(TResult)!;
        try {
            if (result != null)
                typedResult = (TResult)result;
        }
        catch (InvalidCastException) {
            // Intended
        }
        if (ResultSource.TrySetResult(typedResult)) {
            CompleteAndUnregister(notifyCancelled: false);
            if (context != null)
                Context.CacheInfoCapture?.CaptureValue(context.Message);
        }
    }

    public override void SetMatch(RpcInboundContext? context)
    {
        var cacheEntry = Context.CacheInfoCapture?.CacheEntry as RpcCacheEntry<TResult>;
        if (cacheEntry == null) {
            SetError(Rpc.Internal.Errors.MatchButNoCachedEntry(), null);
            return;
        }

        if (ResultSource.TrySetResult(cacheEntry.Result)) {
            CompleteAndUnregister(notifyCancelled: false);
            if (context != null)
                Context.CacheInfoCapture?.CaptureValue(cacheEntry.Value);
        }
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public override void SetError(Exception error, RpcInboundContext? context, bool assumeCancelled = false)
    {
        var oce = error as OperationCanceledException;
        if (error is RpcRerouteException)
            oce = null; // RpcRerouteException is OperationCanceledException, but must be exposed as-is here
        var cancellationToken = oce?.CancellationToken ?? default;
        var isResultSet = oce != null
            ? ResultSource.TrySetCanceled(cancellationToken)
            : ResultSource.TrySetException(error);
        if (!isResultSet)
            return;

        CompleteAndUnregister(notifyCancelled: context == null && !assumeCancelled);
        Context.CacheInfoCapture?.CaptureValue(oce != null, error, cancellationToken);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public override bool Cancel(CancellationToken cancellationToken)
    {
        var isResultSet = ResultSource.TrySetCanceled(cancellationToken);
        if (isResultSet) {
            CompleteAndUnregister(notifyCancelled: true);
            Context.CacheInfoCapture?.CaptureValue(cancellationToken);
        }
        return isResultSet;
    }
}
