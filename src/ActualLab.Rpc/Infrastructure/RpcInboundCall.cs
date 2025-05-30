using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.OS;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable RCS1210, MA0022, VSTHRD103

public abstract class RpcInboundCall : RpcCall
{
    private static readonly ConcurrentDictionary<RpcCallTypeKey, Func<RpcInboundContext, RpcMethodDef, RpcInboundCall>> FactoryCache
        = new(HardwareInfo.ProcessorCountPo2, 131);

    protected readonly CancellationTokenSource? CallCancelSource;
    protected ILogger Log => Context.Peer.Log;

    public override string DebugTypeName => "<-";
    public readonly RpcInboundContext Context;
    public readonly CancellationToken CallCancelToken;
    public ArgumentList? Arguments;
    public Task? ResultTask;
    public RpcHeader[]? ResultHeaders;
    public virtual int CompletedStage => ResultTask is { IsCompleted: true } ? 1 : 0;
    public virtual string CompletedStageName => CompletedStage == 0 ? "" : "ResultReady";
    public Task? WhenProcessed;
    public RpcInboundCallTrace? Trace;

    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2077", Justification = "We assume RPC-related code is fully preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "We assume RPC-related code is fully preserved")]
    public static RpcInboundCall New(byte callTypeId, RpcInboundContext context, RpcMethodDef? methodDef)
    {
        if (methodDef == null) {
            var notFoundMethodDef = context.Peer.Hub.SystemCallSender.NotFoundMethodDef;
            var message = context.Message;
            var (service, method) = message.MethodRef.GetServiceAndMethodName();
            return new RpcInbound404Call<Unit>(context, notFoundMethodDef) {
                // This prevents argument deserialization
                Arguments = ArgumentList.New(service, method)
            };
        }

        return FactoryCache.GetOrAdd(new(callTypeId, methodDef.UnwrappedReturnType),
            static key => {
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
        var completedStageName = CompletedStageName;

        var result = string.Concat(
            DebugTypeName,
            MethodDef.IsStream ? " ~" : " #",
            relatedId.ToString(),
            " ",
            MethodDef.FullName,
            arguments,
            headers.Length > 0 ? $", Headers: {headers.ToDelimitedString()}" : "",
            relatedObject != null ? $" for [{relatedObject}]" : "",
            completedStageName.IsNullOrEmpty() ? "" : $" @{completedStageName}");
        return result;
    }

    public virtual Task Process(CancellationToken cancellationToken)
    {
        var peer = Context.Peer;
        if (NoWait) {
            try {
                Arguments ??= DeserializeArguments();
                if (Arguments == null)
                    return Task.CompletedTask; // No way to resolve argument list type -> the related call is already gone

                // NoWait call arguments aren't validated
            }
            catch (Exception error) {
                throw ProcessArgumentDeserializationError(error);
            }

            if (peer.CallLogger.IsLogged(this))
                peer.CallLogger.LogInbound(this);
            return InvokeTarget(); // NoWait calls must complete fast & be cheap, so the cancellationToken isn't passed
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
                if (peer.CallLogger.IsLogged(this))
                    peer.CallLogger.LogInbound(this);

                // Call
                MethodDef.CallValidator?.Invoke(this);
                ResultTask = inboundMiddlewares != null
                    ? InvokeTarget(inboundMiddlewares)
                    : InvokeTarget();
            }
            catch (Exception error) {
                ResultTask = TaskExt.FromException(error, MethodDef.UnwrappedReturnType);
            }
            return WhenProcessed = ProcessStage1Plus(cancellationToken);
        }
    }

    public virtual Task? TryReprocess(int completedStage, CancellationToken cancellationToken)
    {
        lock (Lock) {
            var existingCall = Context.Peer.InboundCalls.Get(Id);
            if (existingCall != this || ResultTask == null)
                return null;

            return WhenProcessed = completedStage switch {
                >= 1 => Task.CompletedTask,
                _ => ProcessStage1Plus(cancellationToken)
            };
        }
    }

    // Protected methods

    protected virtual Task InvokeTarget()
    {
        var methodDef = MethodDef;
        var server = methodDef.Service.Server;
        return methodDef.TargetAsyncInvoker.Invoke(server, Arguments!);
    }

    protected abstract Task InvokeTarget(RpcInboundMiddlewares middlewares);
    protected abstract Task SendResult();

    protected Exception ProcessArgumentDeserializationError(Exception error)
    {
        error = Errors.CannotDeserializeInboundCallArguments(error);
        if (MethodDef.IsCallResultMethod())
            InvokeOverridenTarget(Hub.SystemCallSender.ErrorMethodDef, ArgumentList.New(error.ToExceptionInfo()));
        return error;

        void InvokeOverridenTarget(RpcMethodDef methodDef, ArgumentList arguments)
        {
            var oldMethodDef = MethodDef;
            var oldArguments = Arguments;
            try {
                MethodDef = methodDef;
                Arguments = arguments;
                var peer = Context.Peer;
                if (peer.CallLogger.IsLogged(this))
                    peer.CallLogger.LogInbound(this);
                _ = InvokeTarget();
            }
            finally {
                MethodDef = oldMethodDef;
                Arguments = oldArguments;
            }
        }
    }

    protected virtual Task ProcessStage1Plus(CancellationToken cancellationToken)
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
                UnregisterFromLock();
            }
            return CallCancelToken.IsCancellationRequested
                ? Task.CompletedTask
                : SendResult();
        }
    }

    protected ArgumentList? DeserializeArguments()
    {
        var peer = Context.Peer;
        var message = Context.Message;
        var argumentSerializer = peer.ArgumentSerializer;
        var arguments = message.Arguments;
        var methodDef = MethodDef;
        if (arguments == null) {
            arguments = methodDef.ArgumentListType.Factory.Invoke();
            var needsArgumentPolymorphism = methodDef.HasPolymorphicArguments;
            if (!needsArgumentPolymorphism)
                argumentSerializer.Deserialize(ref arguments, false, message.ArgumentData);
            else {
                var expectedArguments = arguments;
                if (ServiceDef.Server is IRpcPolymorphicArgumentHandler handler
                    && !handler.IsValidCall(Context, ref expectedArguments, ref needsArgumentPolymorphism))
                    return null; // Means "related call is gone, so just ignore the incoming one"

                argumentSerializer.Deserialize(ref expectedArguments, needsArgumentPolymorphism, message.ArgumentData);
                if (!ReferenceEquals(arguments, expectedArguments))
                    arguments.SetFrom(expectedArguments);
            }
        }

        // Set CancellationToken
        var ctIndex = methodDef.CancellationTokenIndex;
        if (ctIndex >= 0)
            arguments.SetCancellationToken(ctIndex, CallCancelToken);

        return arguments;
    }

    public void Cancel()
        => CallCancelSource.CancelAndDisposeSilently();

    protected bool Unregister()
    {
        lock (Lock)
            return UnregisterFromLock();
    }

    protected bool UnregisterFromLock()
    {
        if (!Context.Peer.InboundCalls.Unregister(this))
            return false; // Already completed or NoWait

        CallCancelSource.DisposeSilently();
        return true;
    }

    // Default implementations of InvokeTarget and SendResult

    protected async Task<TResult> DefaultInvokeTarget<TResult>(RpcInboundMiddlewares middlewares)
    {
        await middlewares.OnBeforeCall(this).ConfigureAwait(false);
        Task<TResult> resultTask = null!;
        try {
            resultTask = (Task<TResult>)InvokeTarget();
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

    protected Task DefaultSendResult<TResult>(Task<TResult>? resultTask)
    {
        var peer = Context.Peer;
        Result<TResult> result;
        if (resultTask is not { IsCompleted: true })
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
        return systemCallSender.Complete(peer, this, result, MethodDef.HasPolymorphicResult, ResultHeaders);

        static Result<TResult> InvocationIsStillInProgressErrorResult()
            => new(default!, ActualLab.Internal.Errors.InternalError(
                "Something is off: remote method isn't completed yet, but the result is requested to be sent."));
    }
}

public class RpcInboundCall<TResult>(RpcInboundContext context, RpcMethodDef methodDef)
    : RpcInboundCall(context, methodDef)
{
    protected override Task InvokeTarget(RpcInboundMiddlewares middlewares)
        => DefaultInvokeTarget<TResult>(middlewares);

    protected override Task SendResult()
        => DefaultSendResult((Task<TResult>?)ResultTask);
}
