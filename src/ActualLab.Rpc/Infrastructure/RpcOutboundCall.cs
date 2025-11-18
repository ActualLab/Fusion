using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ActualLab.OS;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Diagnostics;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcOutboundCall(RpcOutboundContext context)
    : RpcCall(context.MethodDef!)
{
    private static readonly ConcurrentDictionary<
        (byte CallTypeId, Type ReturnType),
        Func<RpcOutboundContext, RpcOutboundCall>> FactoryCache = new();

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
    public CancellationTokenRegistration CancellationHandler;

    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2077", Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume RPC-related code is fully preserved")]
    public static Func<RpcOutboundContext, RpcOutboundCall> GetFactory(RpcMethodDef methodDef)
        => FactoryCache.GetOrAdd(
            (methodDef.CallTypeId, methodDef.UnwrappedReturnType),
            static key => {
                var type = RpcCallTypeRegistry.Resolve(key.CallTypeId)
                    .OutboundCallType
                    .MakeGenericType(key.ReturnType);
                return (Func<RpcOutboundContext, RpcOutboundCall>)type
                    .GetConstructorDelegate(typeof(RpcOutboundContext))!;
            });

    public override string ToString()
    {
        var context = Context;
        var headers = context.Headers.OrEmpty();
        var arguments = context.Arguments;
        var methodDef = context.MethodDef;

        var isAnyStreaming = methodDef != null && methodDef.SystemMethodKind.IsAnyStreaming();
        var relatedId = context.RelatedId;
        var completedStageName = CompletedStageName;
        var result = string.Concat(
            DebugTypeName,
            isAnyStreaming ? " ~" : " #",
            (relatedId != 0 ? relatedId : Id).ToString(CultureInfo.InvariantCulture),
            " ",
            methodDef?.FullName ?? "n/a",
            arguments?.ToString() ?? "(n/a)",
            headers.Length > 0 ? $", Headers: {headers.ToDelimitedString()}" : "",
            completedStageName.IsNullOrEmpty() ? "" : $" @{completedStageName}");
        return result;
    }

    public Task<object?> Invoke()
    {
        if (CacheInfoCaptureMode == RpcCacheInfoCaptureMode.KeyOnly) {
            RegisterCacheKeyOnly();
            return ResultTask;
        }

        if (NoWait) {
            // NoWait always means "send immediately, even if disconnected"
            _ = SendNoWait(MethodDef.HasPolymorphicArguments);
            return ResultTask;
        }

        Register();
        if (!Peer.IsConnected(out _, out var sender))
            return CompleteAsync(); // Slow path

        _ = SendRegistered(isFirstAttempt: true, sender); // Fast path
        return ResultTask;

        async Task<object?> CompleteAsync() {
            try {
                // WhenConnected throws RpcRerouteException in case Peer.Ref.IsRerouted is true
                var (_, sender1) = await Peer
                    .WhenConnected(MethodDef.OutboundCallTimeouts.ConnectTimeout, Context.CancellationToken)
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
        if (CancellationHandler == default)
            CancellationHandler = Context.CancellationToken.Register(static state => {
                var call = (RpcOutboundCall)state!;
                call.Cancel(call.Context.CancellationToken);
            }, this, useSynchronizationContext: false);
    }

    public void RegisterCacheKeyOnly()
    {
        var message = CreateMessage(Id, MethodDef.HasPolymorphicArguments);
        Context.CacheInfoCapture?.CaptureKey(Context, message);
    }

    public Task SendNoWait(bool needsPolymorphism, ChannelWriter<RpcMessage>? sender = null)
    {
        // NoWait calls don't require RpcOutboundContext.Current to serialize their arguments,
        // so no Context.Activate() call here.
        var message = CreateMessage(Context.RelatedId, needsPolymorphism);
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
        try {
            var cacheInfoCapture = context.CacheInfoCapture;
            var hash = cacheInfoCapture?.CacheEntry?.Value.Hash;
            var activity = context.Trace?.Activity;
            message = CreateMessage(Id, MethodDef.HasPolymorphicArguments, hash, activity);
            cacheInfoCapture?.CaptureKey(context, message);
        }
        catch (Exception error) {
            SetError(error, context: null, assumeCancelled: isFirstAttempt);
            return Task.CompletedTask;
        }
        if (Peer.CallLogger.IsLogged(this))
            Peer.CallLogger.LogOutbound(this, message);
        return Peer.Send(message, sender);
    }

    public RpcMessage CreateMessage(long relatedId, bool needsPolymorphism, string? hash = null, Activity? activity = null)
    {
        var oldOutboundContext = RpcOutboundContext.Current;
        RpcOutboundContext.Current = Context;
        try {
            var arguments = Context.Arguments!;
            var argumentData = Peer.ArgumentSerializer.Serialize(arguments, needsPolymorphism, Context.SizeHint);
            var headers = Context.Headers;
            if (hash is not null)
                headers = headers.With(new(WellKnownRpcHeaders.Hash, hash));
            if (activity is not null)
                headers = RpcActivityInjector.Inject(headers, activity.Context);
            return new RpcMessage(MethodDef.CallTypeId, relatedId, MethodDef.Ref, argumentData, headers);
        }
        finally {
            RpcOutboundContext.Current = oldOutboundContext;
        }
    }

    public (RpcMessage Message, string Hash) CreateMessageWithHashHeader(long relatedId, bool needsPolymorphism)
    {
        var oldOutboundContext = RpcOutboundContext.Current;
        RpcOutboundContext.Current = Context;
        try {
            var arguments = Context.Arguments!;
            var argumentData = Peer.ArgumentSerializer.Serialize(arguments, needsPolymorphism, Context.SizeHint);
            var hash = Peer.Hasher.Invoke(argumentData);
            var headers = Context.Headers.With(new(WellKnownRpcHeaders.Hash, hash));
            var message = new RpcMessage(MethodDef.CallTypeId, relatedId, MethodDef.Ref, argumentData, headers);
            return (message, hash);
        }
        finally {
            RpcOutboundContext.Current = oldOutboundContext;
        }
    }

    public virtual void SetResult(object? result, RpcInboundContext? context)
    {
        // We always use Lock to update ResultSource and call CacheInfoCapture.CaptureXxx
        lock (Lock) {
#if DEBUG
            var rpcMethodDef = MethodDef;
            if (!rpcMethodDef.IsInstanceOfUnwrappedReturnType(result)) {
                var error = Internal.Errors.InvalidResultType(rpcMethodDef.UnwrappedReturnType, result?.GetType());
                SetError(error, context);
                Peer.Log.LogError(error, "Got incorrect call result type: {Call}", this);
                return;
            }
#endif
            if (ResultSource.TrySetResult(result)) {
                CompleteAndUnregister(notifyCancelled: false);
                if (context is not null)
                    Context.CacheInfoCapture?.CaptureValueFromLock(context.Message);
            }
        }
    }

    public virtual void SetMatch(RpcInboundContext? context)
    {
        // We always use Lock to update ResultSource and call CacheInfoCapture.CaptureXxx
        lock (Lock) {
            var cacheInfoCapture = Context.CacheInfoCapture;
            var cacheEntry = cacheInfoCapture?.CacheEntry;
            if (cacheEntry is null) {
                SetError(Internal.Errors.MatchButNoCachedEntry(), null);
                return;
            }

            var result = cacheEntry.DeserializedValue;
#if DEBUG
            if (!MethodDef.IsInstanceOfUnwrappedReturnType(result)) {
                var error = Internal.Errors.InvalidResultType(MethodDef.UnwrappedReturnType, result?.GetType());
                SetError(error, context);
                Peer.Log.LogError(error,
                    "Got 'Match', but cache entry's serialized value has incorrect type type: {Call}", this);
                return;
            }
#endif
            if (ResultSource.TrySetResult(result)) {
                CompleteAndUnregister(notifyCancelled: false);
                if (context is not null)
                    cacheInfoCapture?.CaptureValueFromLock(cacheEntry.Value);
            }
        }
    }

    public virtual void SetError(Exception error, RpcInboundContext? context, bool assumeCancelled = false)
    {
        var oce = error as OperationCanceledException;
        if (error is RpcRerouteException)
            oce = null; // RpcRerouteException is OperationCanceledException, but must be exposed as-is here
        var cancellationToken = oce?.CancellationToken ?? default;

        lock (Lock) {
            var isResultSet = oce is not null
                ? ResultSource.TrySetCanceled(cancellationToken)
                : ResultSource.TrySetException(error);
            if (isResultSet) {
                CompleteAndUnregister(notifyCancelled: context is null && !assumeCancelled);
                Context.CacheInfoCapture?.CaptureErrorFromLock(oce is not null, error, cancellationToken);
            }
        }
    }

    public virtual bool Cancel(CancellationToken cancellationToken)
    {
        // We always use Lock to update ResultSource and call CacheInfoCapture.CaptureXxx
        lock (Lock) {
            var isResultSet = ResultSource.TrySetCanceled(cancellationToken);
            if (isResultSet) {
                CompleteAndUnregister(notifyCancelled: true);
                Context.CacheInfoCapture?.CaptureCancellationFromLock(cancellationToken);
            }
            return isResultSet;
        }
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

        CancellationHandler.Dispose();
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
    {
        var peer = Peer;
        if (!peer.Ref.RouteState.IsRerouted()) {
            // IsPeerChanged() is called from TryReroute(), which checks this condition,
            // but since this is a public method, we need to check it here as well.
            return false;
        }

        return peer != MethodDef.RouteOutboundCall(Context.Arguments!);
    }

    public void SetMustRerouteError()
    {
        var error = RpcRerouteException.MustReroute();
        // This SetError call not only sets the error but also invalidates
        // computed method calls awaiting the invalidation.
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

    protected AsyncTaskMethodBuilder<object?> CreateResultSource<TResult>()
        => NoWait || CacheInfoCaptureMode == RpcCacheInfoCaptureMode.KeyOnly
            ? Cache<TResult>.NoWaitResultSource
            : AsyncTaskMethodBuilderExt.New<object?>(); // MUST run continuations asynchronously!

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
        => ResultSource = CreateResultSource<TResult>();
}
