using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ActualLab.Interception;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable RCS1210, MA0022, VSTHRD103

public abstract class RpcInboundCall : RpcCall
{
    private static readonly ConcurrentDictionary<
        (byte CallTypeId, Type ReturnType),
        Func<RpcInboundContext, RpcInboundCall>> FactoryCache = new();

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
    public static Func<RpcInboundContext, RpcInboundCall> GetFactory(RpcMethodDef methodDef)
        => FactoryCache.GetOrAdd(
            (methodDef.CallTypeId, methodDef.UnwrappedReturnType),
            static key => {
                var type = RpcCallTypeRegistry.Resolve(key.CallTypeId)
                    .InboundCallType
                    .MakeGenericType(key.ReturnType);
                return (Func<RpcInboundContext, RpcInboundCall>)type
                    .GetConstructorDelegate(typeof(RpcInboundContext))!;
            });

    protected RpcInboundCall(RpcInboundContext context)
        : base(context.MethodDef)
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
        var arguments = Arguments is not null
            ? Arguments.ToString()
            : $"ArgumentData: {message.ArgumentData}";
        var relatedId = message.RelatedId;
        var relatedObject = relatedId == 0 ? (object?)null
            : MethodDef.SystemMethodKind.IsAnyStreaming()
                ? Context.Peer.RemoteObjects.Get(relatedId)
                : Context.Peer.OutboundCalls.Get(relatedId);
        var completedStageName = CompletedStageName;

        var result = string.Concat(
            DebugTypeName,
            MethodDef.SystemMethodKind.IsAnyStreaming() ? " ~" : " #",
            relatedId.ToString(CultureInfo.InvariantCulture),
            " ",
            MethodDef.FullName,
            arguments,
            headers.Length > 0 ? $", Headers: {headers.ToDelimitedString()}" : "",
            relatedObject is not null ? $" for [{relatedObject}]" : "",
            completedStageName.IsNullOrEmpty() ? "" : $" @{completedStageName}");
        return result;
    }

    public virtual Task Process(CancellationToken cancellationToken)
    {
        RpcInboundContext.Current = Context;
        var peer = Context.Peer;
        if (NoWait) {
            try {
                Arguments ??= DeserializeArguments();
                if (Arguments is null)
                    return Task.CompletedTask; // No way to resolve argument list type -> the related call is already gone

                // NoWait call arguments aren't validated
            }
            catch (Exception error) {
                throw ProcessArgumentDeserializationError(error);
            }

            if (peer.CallLogger.IsLogged(this))
                peer.CallLogger.LogInbound(this);
            return InvokeServer(); // NoWait calls must complete fast & be cheap, so the cancellationToken isn't passed
        }

        var existingCall = Context.Peer.InboundCalls.GetOrRegister(this);
        if (existingCall != this)
            return existingCall.TryReprocess(0, cancellationToken)
                ?? existingCall.WhenProcessed
                ?? Task.CompletedTask;

        lock (Lock) {
            try {
                if (MethodDef.Tracer is { } tracer)
                    Trace = tracer.StartInboundTrace(this);

                Arguments ??= DeserializeArguments();
                if (Arguments is null)
                    return Task.CompletedTask; // No way to resolve argument list type -> the related call is already gone

                // Before call
                if (peer.CallLogger.IsLogged(this))
                    peer.CallLogger.LogInbound(this);

                // Call
                ResultTask = MethodDef.InboundCallPipelineInvoker.Invoke(this);
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
            if (existingCall != this || ResultTask is null)
                return null;

            return WhenProcessed = completedStage switch {
                >= 1 => Task.CompletedTask,
                _ => ProcessStage1Plus(cancellationToken)
            };
        }
    }

    // Protected methods

    protected internal abstract Task InvokeServer();
    protected abstract Task SendResult();

    protected Exception ProcessArgumentDeserializationError(Exception error)
    {
        error = Errors.CannotDeserializeInboundCallArguments(error);
        if (!MethodDef.SystemMethodKind.IsCallResultMethod())
            return error;

        var oldMethodDef = MethodDef;
        var oldArguments = Arguments;
        try {
            MethodDef = Hub.SystemCallSender.ErrorMethodDef;
            Arguments = ArgumentList.New(error.ToExceptionInfo());
            var peer = Context.Peer;
            if (peer.CallLogger.IsLogged(this))
                peer.CallLogger.LogInbound(this);
            _ = InvokeServer();
            return error;
        }
        finally {
            // Restore the original methodDef and arguments
            MethodDef = oldMethodDef;
            Arguments = oldArguments;
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
        if (arguments is null) {
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

    // Default implementations for SendResult; InvokeServer doesn't need one, coz it's 1-liner

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

public class RpcInboundCall<TResult>(RpcInboundContext context)
    : RpcInboundCall(context)
{
    protected internal override Task InvokeServer()
        // This method is actually never called directly: regular inbound calls use fast pipeline invoker
        // produced by InboundCallPipelineFastInvokerFactory, which "inlines" it right into the pipeline invoker.
        => MethodDef.InboundCallServerInvoker.Invoke(this);

    protected override Task SendResult()
        => DefaultSendResult((Task<TResult>?)ResultTask);
}
