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
    public abstract Task UntypedResultTask { get; }
    public CancellationTokenRegistration CancellationHandler;
    public CpuTimestamp StartedAt;

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static RpcOutboundCall? New(RpcOutboundContext context)
    {
        var peer = context.Peer;
        if (peer == null)
            throw ActualLab.Internal.Errors.InternalError("context.Peer == null.");
        if (peer.ConnectionKind == RpcPeerConnectionKind.LocalCall)
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

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public Task RegisterAndSend()
    {
        if (NoWait)
            return SendNoWait(MethodDef.AllowArgumentPolymorphism);
        if (Id != 0)
            throw Errors.InternalError("This method should never be called repeatedly for the same call.");

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
        try {
            using (Context.Activate())
                message = CreateMessage(Id, MethodDef.AllowArgumentPolymorphism);
            if (Context.MustCaptureCacheKey(message, out var keyOnly) && keyOnly) {
                // "Key-only" capture -> skipping the actual execution
                SetResult(null, null);
                return Task.CompletedTask;
            }
        }
        catch (Exception error) {
            SetError(error, context: null, assumeCancelled: isFirstAttempt);
            return Task.CompletedTask;
        }
        if (Peer.CallLogger.IsLogged(this))
            Peer.CallLogger.LogOutbound(this, message);
        return Peer.Send(message);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public virtual RpcMessage CreateMessage(long relatedId, bool allowPolymorphism)
    {
        var arguments = Context.Arguments!;
        if (Peer.ConnectionKind != RpcPeerConnectionKind.Remote) {
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

        var argumentData = Peer.ArgumentSerializer.Serialize(arguments, allowPolymorphism);
        return new RpcMessage(
            Context.CallTypeId, relatedId,
            MethodDef.Service.Name, MethodDef.Name,
            argumentData, Context.Headers);
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
    protected readonly TaskCompletionSource<TResult> ResultSource;

    public override Task UntypedResultTask => ResultSource.Task;
    public Task<TResult> ResultTask => ResultSource.Task;

    public RpcOutboundCall(RpcOutboundContext context)
        : base(context)
    {
        ResultSource = NoWait
            ? (TaskCompletionSource<TResult>)(object)RpcNoWait.TaskSources.Completed
            : new TaskCompletionSource<TResult>();
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
            if (context != null && Context.MustCaptureCacheData(out var dataSource))
                dataSource.TrySetResult(context.Message.ArgumentData);
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
        if (Context.MustCaptureCacheData(out var dataSource)) {
            if (oce != null)
                dataSource.TrySetCanceled(cancellationToken);
            else
                dataSource.TrySetException(error);
        }
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public override bool Cancel(CancellationToken cancellationToken)
    {
        var isCancelled = ResultSource.TrySetCanceled(cancellationToken);
        if (isCancelled) {
            CompleteAndUnregister(notifyCancelled: true);
            if (Context.MustCaptureCacheData(out var dataSource))
                dataSource.TrySetCanceled(cancellationToken);
        }
        return isCancelled;
    }
}
