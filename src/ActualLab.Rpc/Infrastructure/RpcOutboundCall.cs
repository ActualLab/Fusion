using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using ActualLab.OS;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Diagnostics;
using Cysharp.Text;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcOutboundCall(RpcOutboundContext context)
    : RpcCall(context.MethodDef!)
{
    private static readonly ConcurrentDictionary<RpcCallTypeKey, Func<RpcOutboundContext, RpcOutboundCall>> FactoryCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    protected AsyncTaskMethodBuilder<object?> ResultSource;

    public override string DebugTypeName => "->";

    public readonly RpcOutboundContext Context = context;
    public readonly RpcPeer Peer = context.Peer!;
    public readonly RpcCacheInfoCaptureMode CacheInfoCaptureMode = context.CacheInfoCapture?.CaptureMode ?? default;

    public Task<object?> ResultTask {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ResultSource.Task;
    }

    public virtual int CompletedStage {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => ResultSource.Task.IsCompleted
            ? RpcCallStage.ResultReady | RpcCallStage.Unregistered
            : 0;
    }

    public string CompletedStageName => RpcCallStage.GetName(CompletedStage);

    public CpuTimestamp StartedAt;
    public CancellationTokenRegistration CallCancelHandler;

    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2077", Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume RPC-related code is fully preserved")]
    public static RpcOutboundCall? New(RpcOutboundContext context)
    {
        var peer = context.Peer;
        if (peer == null)
            throw Errors.InternalError("context.Peer == null.");

        if (peer.ConnectionKind == RpcPeerConnectionKind.Local)
            return null;

        return FactoryCache.GetOrAdd(new(context.CallTypeId, context.MethodDef!.UnwrappedReturnType),
            static key => {
                var type = RpcCallTypeRegistry.Resolve(key.CallTypeId)
                    .OutboundCallType
                    .MakeGenericType(key.CallResultType);
                return (Func<RpcOutboundContext, RpcOutboundCall>)type
                    .GetConstructorDelegate(typeof(RpcOutboundContext))!;
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
        var completedStageName = CompletedStageName;
        var result = ZString.Concat(
            DebugTypeName,
            isStream ? " ~" : " #",
            relatedId != 0 ? relatedId : Id,
            ' ',
            methodDef?.FullName.Value ?? "n/a",
            arguments?.ToString() ?? "(n/a)",
            headers.Length > 0 ? $", Headers: {headers.ToDelimitedString()}" : "",
            completedStageName.IsNullOrEmpty() ? "" : $" @{completedStageName}");
        return result;
    }

    public Task<object?> Invoke(bool assumeConnected)
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

        async Task<object?> CompleteAsync() {
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

    public void Register()
    {
        Peer.OutboundCalls.Register(this);
        if (CallCancelHandler == default)
            CallCancelHandler = Context.CallCancelToken.Register(static state => {
                var call = (RpcOutboundCall)state!;
                call.Cancel(call.Context.CallCancelToken);
            }, this, useSynchronizationContext: false);
    }

    public void RegisterCacheKeyOnly()
    {
        using var _ = Context.Activate(); // CreateMessage may use it
        var message = CreateMessage(Id, MethodDef.AllowArgumentPolymorphism);
        Context.CacheInfoCapture?.CaptureKey(Context, message);
    }

    public Task SendNoWait(bool allowPolymorphism, ChannelWriter<RpcMessage>? sender = null)
    {
        // NoWait calls don't require RpcOutboundContext.Current to serialize their arguments,
        // so no Context.Activate() call here.
        var message = CreateMessage(Context.RelatedId, allowPolymorphism);
        if (Peer.CallLogger.IsLogged(this))
            Peer.CallLogger.LogOutbound(this, message);
        return Peer.Send(message, sender);
    }

    public Task SendNoWait(RpcMessage message, ChannelWriter<RpcMessage>? sender = null)
    {
        if (Peer.CallLogger.IsLogged(this))
            Peer.CallLogger.LogOutbound(this, message);
        return Peer.Send(message, sender);
    }

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

    public (RpcMessage Message, string Hash) CreateMessageWithHashHeader(long relatedId, bool allowPolymorphism)
    {
        var arguments = Context.Arguments!;
        var argumentData = Peer.ArgumentSerializer.Serialize(arguments, allowPolymorphism, Context.SizeHint);
        var hash = Peer.HashProvider.Invoke(argumentData);
        var headers = Context.Headers.With(new(WellKnownRpcHeaders.Hash, hash));
        var message = new RpcMessage(Context.CallTypeId, relatedId, MethodDef.Ref, argumentData, headers);
        return (message, hash);
    }

    public virtual void SetResult(object? result, RpcInboundContext? context)
    {
        if (result == null || !MethodDef.UnwrappedReturnType.IsInstanceOfType(result))
            result = MethodDef.DefaultResult;
        if (ResultSource.TrySetResult(result)) {
            CompleteAndUnregister(notifyCancelled: false);
            if (context != null)
                Context.CacheInfoCapture?.CaptureValue(context.Message);
        }
    }

    public virtual void SetMatch(RpcInboundContext? context)
    {
        var cacheEntry = Context.CacheInfoCapture?.CacheEntry;
        if (cacheEntry == null) {
            SetError(Internal.Errors.MatchButNoCachedEntry(), null);
            return;
        }

        if (ResultSource.TrySetResult(cacheEntry.DeserializedValue)) {
            CompleteAndUnregister(notifyCancelled: false);
            if (context != null)
                Context.CacheInfoCapture?.CaptureValue(cacheEntry.Value);
        }
    }

    public virtual void SetError(Exception error, RpcInboundContext? context, bool assumeCancelled = false)
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

    public virtual bool Cancel(CancellationToken cancellationToken)
    {
        var isResultSet = ResultSource.TrySetCanceled(cancellationToken);
        if (isResultSet) {
            CompleteAndUnregister(notifyCancelled: true);
            Context.CacheInfoCapture?.CaptureValue(cancellationToken);
        }
        return isResultSet;
    }

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

    // Helpers

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

    // Protected methods

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

    protected AsyncTaskMethodBuilder<object?> NewResultSource<TResult>()
        => NoWait || CacheInfoCaptureMode == RpcCacheInfoCaptureMode.KeyOnly
            ? Cache<TResult>.NoWaitResultSource
            : AsyncTaskMethodBuilderExt.New<object?>();

    // Nested types

    protected static class Cache<TResult>
    {
        public static readonly AsyncTaskMethodBuilder<object?> NoWaitResultSource
            = AsyncTaskMethodBuilderExt.New<object?>().WithResult(default(TResult));
    }
}

public class RpcOutboundCall<TResult> : RpcOutboundCall
{
    public RpcOutboundCall(RpcOutboundContext context) : base(context)
        => ResultSource = NewResultSource<TResult>();
}
