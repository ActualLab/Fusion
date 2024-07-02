using System.Diagnostics.CodeAnalysis;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Internal;
using Cysharp.Text;

namespace ActualLab.Rpc.Infrastructure;

public abstract class RpcOutboundCall(RpcOutboundContext context)
    : RpcCall(context.MethodDef!)
{
    private static readonly ConcurrentDictionary<(byte, Type), Func<RpcOutboundContext, RpcOutboundCall>> FactoryCache = new();

    protected override string DebugTypeName => "->";

    public readonly RpcOutboundContext Context = context;
    public readonly RpcPeer Peer = context.Peer!;
    public abstract Task UntypedResultTask { get; }
    public TimeSpan ConnectTimeout;
    public TimeSpan Timeout;

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

        Peer.OutboundCalls.Register(this);
        var sendTask = SendRegistered();

        // RegisterCancellationHandler must follow SendRegistered,
        // coz it's possible that ResultTask is already completed
        // at this point (e.g. due to an error), and thus
        // cancellation handler isn't necessary.
        if (!UntypedResultTask.IsCompleted)
            RegisterCancellationHandler();
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
        => SendRegistered(false);

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public void Unregister(bool notifyCancelled = false)
    {
        if (!Peer.OutboundCalls.Unregister(this))
            return; // Already unregistered

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

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    protected void RegisterCancellationHandler()
    {
        var cancellationToken = Context.CancellationToken;
        CancellationTokenSource? timeoutCts = null;
        CancellationTokenSource? linkedCts = null;
        if (Timeout > TimeSpan.Zero) {
            timeoutCts = new CancellationTokenSource(Timeout);
            linkedCts = timeoutCts.Token.LinkWith(cancellationToken);
            cancellationToken = linkedCts.Token;
        }
        var ctr = cancellationToken.Register(static state => {
            var call = (RpcOutboundCall)state!;
            var cancellationToken = call.Context.CancellationToken;
            if (cancellationToken.IsCancellationRequested)
                call.Cancel(cancellationToken);
            else {
                // timeoutCts is timed out
                var error = Errors.CallTimeout(call.Peer);
                call.SetError(error, context: null);
            }
        }, this, useSynchronizationContext: false);
        _ = UntypedResultTask.ContinueWith(_ => {
                ctr.Dispose();
                linkedCts?.Dispose();
                timeoutCts?.Dispose();
            },
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
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
            Unregister();
            if (context != null && Context.MustCaptureCacheData(out var dataSource))
                dataSource.TrySetResult(context.Message.ArgumentData);
        }
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    public override void SetError(Exception error, RpcInboundContext? context, bool assumeCancelled = false)
    {
        var oce = error as OperationCanceledException;
        var cancellationToken = oce?.CancellationToken ?? default;
        var isResultSet = oce != null
            ? ResultSource.TrySetCanceled(cancellationToken)
            : ResultSource.TrySetException(error);
        if (!isResultSet)
            return;

        Unregister(context == null && !assumeCancelled);
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
            Unregister(true);
            if (Context.MustCaptureCacheData(out var dataSource))
                dataSource.TrySetCanceled(cancellationToken);
        }
        return isCancelled;
    }
}
