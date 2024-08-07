using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Internal;
using Cysharp.Text;

namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable RCS1210, MA0022, VSTHRD103

public abstract class RpcInboundCall : RpcCall
{
    private static readonly ConcurrentDictionary<(byte, Type), Func<RpcInboundContext, RpcMethodDef, RpcInboundCall>> FactoryCache = new();

    protected readonly CancellationTokenSource? CallCancelSource;
    protected ILogger Log => Context.Peer.Log;

    public override string DebugTypeName => "<-";
    public readonly RpcInboundContext Context;
    public readonly CancellationToken CallCancelToken;
    public ArgumentList? Arguments;
    public abstract Task? UntypedResultTask { get; }
    public RpcHeader[]? ResultHeaders;
    public virtual int CompletedStage => UntypedResultTask is { IsCompleted: true } ? 1 : 0;
    public virtual string CompletedStageName => CompletedStage == 0 ? "" : "ResultReady";
    public Task? WhenProcessed;
    public RpcInboundCallTrace? Trace;

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
    public static RpcInboundCall New(byte callTypeId, RpcInboundContext context, RpcMethodDef? methodDef)
    {
        if (methodDef == null) {
            var notFoundMethodDef = context.Peer.Hub.SystemCallSender.NotFoundMethodDef;
            var message = context.Message;
            return new RpcInbound404Call<Unit>(context, notFoundMethodDef) {
                // This prevents argument deserialization
                Arguments = ArgumentList.New(message.Service, message.Method)
            };
        }

        return FactoryCache.GetOrAdd((callTypeId, methodDef.UnwrappedReturnType), static key => {
            var (callTypeId, tResult) = key;
            var type = RpcCallTypeRegistry.Resolve(callTypeId)
                .InboundCallType
                .MakeGenericType(tResult);
            return (Func<RpcInboundContext, RpcMethodDef, RpcInboundCall>)type
                .GetConstructorDelegate(typeof(RpcInboundContext), typeof(RpcMethodDef))!;
        }).Invoke(context, methodDef);
    }

    protected RpcInboundCall(RpcInboundContext context, RpcMethodDef methodDef)
        : base(methodDef)
    {
        Context = context;
        Id = NoWait ? 0 : context.Message.RelatedId;
        var peerChangedToken = Context.PeerChangedToken;
        if (NoWait)
            CallCancelToken = peerChangedToken;
        else {
            CallCancelSource = peerChangedToken.CreateLinkedTokenSource();
            CallCancelToken = CallCancelSource.Token;
        }
    }

    public override string ToString()
    {
        var message = Context.Message;
        var headers = message.Headers.OrEmpty();
        var arguments = Arguments != null
            ? Arguments.ToString()
            : $"ArgumentData: {message.ArgumentData}";
        var relatedId = message.RelatedId;
        var relatedObject = relatedId == 0 ? (object?)null
            : MethodDef.IsStream
                ? Context.Peer.RemoteObjects.Get(relatedId)
                : Context.Peer.OutboundCalls.Get(relatedId);

        var result = ZString.Concat(
            DebugTypeName,
            MethodDef.IsStream ? " ~" : " #",
            relatedId,
            ' ',
            MethodDef.FullName,
            arguments,
            headers.Length > 0 ? $", Headers: {headers.ToDelimitedString()}" : "",
            relatedObject != null ? $" for {relatedObject}" : "");
        var completedStageName = CompletedStageName;
        if (!completedStageName.IsNullOrEmpty())
            result += $": {completedStageName}";
        return result;
    }

    public abstract Task Process(CancellationToken cancellationToken);

    public abstract Task? TryReprocess(int completedStage, CancellationToken cancellationToken);

    public void Cancel()
        => CallCancelSource.CancelAndDisposeSilently();

    // Protected methods

    protected bool Unregister()
    {
        lock (Lock) {
            if (!Context.Peer.InboundCalls.Unregister(this))
                return false; // Already completed or NoWait

            CallCancelSource.DisposeSilently();
        }
        return true;
    }
}

public class RpcInboundCall<TResult>(RpcInboundContext context, RpcMethodDef methodDef)
    : RpcInboundCall(context, methodDef)
{
    public Task<TResult>? ResultTask { get; private set; } = null!;
    public override Task? UntypedResultTask => ResultTask;

    [RequiresUnreferencedCode(UnreferencedCode.Rpc)]
#pragma warning disable IL2046
    public override Task Process(CancellationToken cancellationToken)
#pragma warning restore IL2046
    {
        if (NoWait) {
            try {
                Arguments ??= DeserializeArguments();
                if (Arguments == null)
                    return Task.CompletedTask; // No way to resolve argument list type -> the related call is already gone

                var peer = Context.Peer;
                if (peer.CallLogger.IsLogged(this))
                    peer.CallLogger.LogInbound(this);
                return InvokeTarget(); // NoWait calls must complete fast & be cheap, so cancellationToken isn't passed
            }
            catch (Exception error) {
                return Task.FromException<TResult>(error);
            }
        }

        var existingCall = Context.Peer.InboundCalls.GetOrRegister(this);
        if (existingCall != this)
            return existingCall.TryReprocess(0, cancellationToken)
                ?? existingCall.WhenProcessed
                ?? Task.CompletedTask;

        var inboundMiddlewares = Hub.InboundMiddlewares.NullIfEmpty();
        lock (Lock) {
            try {
                if (MethodDef.Tracer is { } tracer)
                    Trace = tracer.StartInboundTrace(this);

                Arguments ??= DeserializeArguments();
                if (Arguments == null)
                    return Task.CompletedTask; // No way to resolve argument list type -> the related call is already gone

                // Before call
                var peer = Context.Peer;
                if (peer.CallLogger.IsLogged(this))
                    peer.CallLogger.LogInbound(this);

                // Call
                ResultTask = inboundMiddlewares != null
                    ? InvokeTarget(inboundMiddlewares)
                    : InvokeTarget();
            }
            catch (Exception error) {
                ResultTask = Task.FromException<TResult>(error);
            }
            return WhenProcessed = ProcessStage1(cancellationToken);
        }
    }

    public override Task? TryReprocess(int completedStage, CancellationToken cancellationToken)
    {
        lock (Lock) {
            var existingCall = Context.Peer.InboundCalls.Get(Id);
            if (existingCall != this || ResultTask == null)
                return null;

            return WhenProcessed = completedStage switch {
                >= 1 => Task.CompletedTask,
                _ => ProcessStage1(cancellationToken)
            };
        }
    }

    // Protected methods

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    protected virtual Task ProcessStage1(CancellationToken cancellationToken)
    {
        return ResultTask!.IsCompleted
            ? Complete()
            : CompleteAsync();

        async Task CompleteAsync() {
            await ResultTask!.SilentAwait(false);
            await Complete().ConfigureAwait(false);
        }

        Task Complete() {
            lock (Lock) {
                if (Trace is { } trace) {
                    trace.Complete(this);
                    Trace = null;
                }
                Unregister();
            }
            return CallCancelToken.IsCancellationRequested
                ? Task.CompletedTask
                : SendResult();
        }
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    protected ArgumentList? DeserializeArguments()
    {
        var peer = Context.Peer;
        var message = Context.Message;
        var argumentSerializer = peer.ArgumentSerializer;
        var arguments = message.Arguments;
        if (arguments == null) {
            arguments = MethodDef.ArgumentListFactory.Invoke();
            var allowPolymorphism = MethodDef.AllowArgumentPolymorphism;
            if (!MethodDef.HasObjectTypedArguments)
                argumentSerializer.Deserialize(ref arguments, allowPolymorphism, message.ArgumentData);
            else {
                var dynamicCallHandler = (IRpcDynamicCallHandler)ServiceDef.Server;
                var expectedArguments = arguments;
                if (!dynamicCallHandler.IsValidCall(Context, ref expectedArguments, ref allowPolymorphism))
                    return null;

                argumentSerializer.Deserialize(ref expectedArguments, allowPolymorphism, message.ArgumentData);
                if (!ReferenceEquals(arguments, expectedArguments))
                    arguments.SetFrom(expectedArguments);
            }
        }

        // Set CancellationToken
        var ctIndex = MethodDef.CancellationTokenIndex;
        if (ctIndex >= 0)
            arguments.SetCancellationToken(ctIndex, CallCancelToken);

        return arguments;
    }

    protected async Task<TResult> InvokeTarget(RpcInboundMiddlewares middlewares)
    {
        await middlewares.OnBeforeCall(this).ConfigureAwait(false);
        Task<TResult> resultTask = null!;
        try {
            resultTask = InvokeTarget();
            return await resultTask.ConfigureAwait(false);
        }
        catch (Exception e) {
            resultTask ??= Task.FromException<TResult>(e);
            throw;
        }
        finally {
            await middlewares.OnAfterCall(this, resultTask).ConfigureAwait(false);
        }
    }

    protected virtual Task<TResult> InvokeTarget()
    {
        var methodDef = MethodDef;
        var server = methodDef.Service.Server;
        return (Task<TResult>)methodDef.TargetAsyncInvoker.Invoke(server, Arguments!);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    protected Task SendResult()
    {
        var peer = Context.Peer;
        Result<TResult> result;
        if (ResultTask is not { IsCompleted: true } resultTask)
            result = InvocationIsStillInProgressErrorResult();
        else if (resultTask.Exception is { } error)
            result = new Result<TResult>(default!, error.GetBaseException());
        else if (resultTask.IsCanceled)
            result = new Result<TResult>(default!, new TaskCanceledException()); // Technically it's covered by prev. "else if"
        else
            result = resultTask.Result;
        if (result.Error is { } e and not OperationCanceledException)
            Log.IfEnabled(LogLevel.Error)?.LogError(e, "Remote call completed with an error: {Call}", this);

        var systemCallSender = Hub.SystemCallSender;
        return systemCallSender.Complete(peer, this, result, MethodDef.AllowResultPolymorphism, ResultHeaders);
    }

    // Private methods

    private static Result<TResult> InvocationIsStillInProgressErrorResult() =>
        new(default!, ActualLab.Internal.Errors.InternalError(
            "Something is off: remote method isn't completed yet, but the result is requested to be sent."));
}
