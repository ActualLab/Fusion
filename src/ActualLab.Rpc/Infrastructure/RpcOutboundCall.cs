using System.Diagnostics.CodeAnalysis;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Internal;
using Cysharp.Text;
using Errors = ActualLab.Internal.Errors;

namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcOutboundCall(RpcOutboundContext context)
    : RpcCall(context.MethodDef!)
{
    private static readonly ConcurrentDictionary<(byte, Type), Func<RpcOutboundContext, RpcOutboundCall>> FactoryCache = new();

    protected override string DebugTypeName => "->";

    public readonly RpcOutboundContext Context = context;
    public readonly RpcPeer Peer = context.Peer!;
    public readonly RpcCacheInfoCaptureMode CacheInfoCaptureMode = context.CacheInfoCapture?.CaptureMode ?? default;
    public abstract Task UntypedResultTask { get; }
    public CancellationTokenRegistration CancellationHandler;
    public CpuTimestamp StartedAt;

    public bool MustSerializeArguments {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => CacheInfoCaptureMode != RpcCacheInfoCaptureMode.None || Peer.ConnectionKind == RpcPeerConnectionKind.Remote;
    }

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static RpcOutboundCall? New(RpcOutboundContext context)
    {
        var peer = context.Peer;
        if (peer == null)
            throw Errors.InternalError("context.Peer == null.");

        if (peer.ConnectionKind == RpcPeerConnectionKind.Local)
            return null;

        return FactoryCache.GetOrAdd((context.CallTypeId, context.MethodDef!.UnwrappedReturnType), static key => {
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
        var ctIndex = methodDef?.CancellationTokenIndex ?? -1;
        if (ctIndex >= 0)
            arguments = arguments?.Remove(ctIndex);

        var isStream = methodDef?.IsStream == true;
        var relatedId = context.RelatedId;
        return ZString.Concat(
            DebugTypeName,
            isStream ? " ~" : " #",
            relatedId != 0 ? relatedId : Id,
            ' ',
            methodDef?.FullName ?? "n/a",
            arguments?.ToString() ?? "(n/a)",
            headers.Count > 0 ? $", Headers: {headers.ToDelimitedString()}" : "");
    }

    public Task WhenConnected()
        => Peer.WhenConnected(MethodDef.Timeouts.ConnectTimeout, Context.CancellationToken);

    public Task WhenConnected(CancellationToken cancellationToken)
        => Peer.WhenConnected(MethodDef.Timeouts.ConnectTimeout, cancellationToken);

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public Task RegisterAndSend()
    {
        Peer.OutboundCalls.Register(this);
        var sendTask = SendRegistered();

        if (!UntypedResultTask.IsCompleted && CancellationHandler == default)
            CancellationHandler = Context.CancellationToken.Register(static state => {
                var call = (RpcOutboundCall)state!;
                call.Cancel(call.Context.CancellationToken);
            }, this, useSynchronizationContext: false);

        return sendTask;
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public void RegisterCacheKeyOnly()
    {
        using var _ = Context.Activate();
        var message = CreateMessage(Id, MethodDef.AllowArgumentPolymorphism);
        Context.CacheInfoCapture?.CaptureKey(Context, message);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public Task SendNoWait(bool allowPolymorphism, ChannelWriter<RpcMessage>? sender = null)
    {
        // No "using (Context.Activate())" here: we assume NoWait calls
        // don't require RpcOutboundContext.Current to serialize their arguments.
        var message = CreateMessage(Context.RelatedId, allowPolymorphism);
        if (Peer.CallLogger.IsLogged(this))
            Peer.CallLogger.LogOutbound(this, message);
        return Peer.Send(message, sender);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public Task SendRegistered(bool isFirstAttempt = true)
    {
        RpcMessage message;
        var scope = Context.Activate();
        try {
            message = CreateMessage(Id, MethodDef.AllowArgumentPolymorphism);
            Context.CacheInfoCapture?.CaptureKey(Context, message);
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
        return Peer.Send(message);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public virtual RpcMessage CreateMessage(long relatedId, bool allowPolymorphism)
    {
        var arguments = Context.Arguments!;
        if (MustSerializeArguments) {
            var argumentData = Peer.ArgumentSerializer.Serialize(arguments, allowPolymorphism);
            return new RpcMessage(
                Context.CallTypeId, relatedId,
                MethodDef.Service.Name, MethodDef.Name,
                argumentData, Context.Headers);
        }

        // Skip argument serialization
        var ctIndex = MethodDef.CancellationTokenIndex;
        if (ctIndex >= 0) {
            arguments = arguments with { }; // Clone
            arguments.SetCancellationToken(ctIndex, default);
        }
        return new RpcMessage(
            Context.CallTypeId, relatedId,
            MethodDef.Service.Name, MethodDef.Name,
            default, Context.Headers) {
            Arguments = arguments,
        };
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public abstract void SetResult(object? result, RpcInboundContext? context);
    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public abstract void SetError(Exception error, RpcInboundContext? context, bool assumeCancelled = false);
    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public abstract bool Cancel(CancellationToken cancellationToken);

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public virtual Task Reconnect(bool isPeerChanged, CancellationToken cancellationToken)
    {
        if (isPeerChanged)
            StartedAt = CpuTimestamp.Now;
        return SendRegistered(false);
    }

    public void Complete()
    {
        if (Peer.OutboundCalls.Complete(this))
            CancellationHandler.Dispose();
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public void CompleteAndUnregister(bool notifyCancelled)
    {
        if (NoWait)
            throw Errors.InternalError("This method should never be called for NoWait calls.");
        if (!Peer.OutboundCalls.Unregister(this))
            return; // Already unregistered

        Complete();
        if (notifyCancelled)
            NotifyCancelled();
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
    public Task<TResult> Invoke(bool assumeConnected = true)
    {
        if (CacheInfoCaptureMode == RpcCacheInfoCaptureMode.KeyOnly)
            RegisterCacheKeyOnly();
        else if (NoWait) // NoWait always means "send immediately, even if disconnected"
            _ = SendNoWait(MethodDef.AllowArgumentPolymorphism);
        else if (Peer.IsConnected() || assumeConnected)
            _ = RegisterAndSend(); // Fast path
        else
            return InvokeOnceConnectedAsync(); // Slow path

        return ResultTask;

        async Task<TResult> InvokeOnceConnectedAsync()
        {
            await WhenConnected().ConfigureAwait(false);
            _ = RegisterAndSend();
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
                Context.CacheInfoCapture?.CaptureData(context.Message);
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
        Context.CacheInfoCapture?.CaptureData(oce != null, error, cancellationToken);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public override bool Cancel(CancellationToken cancellationToken)
    {
        var isCancelled = ResultSource.TrySetCanceled(cancellationToken);
        if (isCancelled) {
            CompleteAndUnregister(notifyCancelled: true);
            Context.CacheInfoCapture?.CaptureData(cancellationToken);
        }
        return isCancelled;
    }
}
