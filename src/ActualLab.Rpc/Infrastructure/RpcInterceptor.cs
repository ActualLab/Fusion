using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

#if !NET5_0
[RequiresUnreferencedCode(UnreferencedCode.Rpc)]
#endif
public class RpcInterceptor : RpcInterceptorBase
{
    public new record Options : RpcInterceptorBase.Options
    {
        public static Options Default { get; set; } = new();
    }

    public object? LocalTarget { get; init; }

    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcInterceptor(Options settings, IServiceProvider services, RpcServiceDef serviceDef)
        : base(settings, services, serviceDef)
    { }

    protected override Func<Invocation, object?>? CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>
        (Invocation initialInvocation, MethodDef methodDef)
    {
        var rpcMethodDef = (RpcMethodDef)methodDef;
        var localCallAsyncInvoker = methodDef.SelectAsyncInvoker<TUnwrapped>(initialInvocation.Proxy, LocalTarget);
        return invocation => {
            var context = invocation.Context as RpcOutboundContext ?? new();
            var call = (RpcOutboundCall<TUnwrapped>?)context.PrepareCall(rpcMethodDef, invocation.Arguments);
            var peer = context.Peer!;
            Task<TUnwrapped> resultTask;
            if (peer.Ref.CanBeRerouted) {
                resultTask = InvokeWithRerouting(invocation, context, call, localCallAsyncInvoker);
            }
            else if (call == null) {
                if (localCallAsyncInvoker != null)
                    resultTask = localCallAsyncInvoker.Invoke(invocation);
                else
                    throw RpcRerouteException.LocalCall();
            }
            else if (call.NoWait || context.CacheInfoCapture is { CaptureMode: RpcCacheInfoCaptureMode.KeyOnly }) {
                // NoWait requires call to be sent no matter what is the connection state now;
                // RpcCacheInfoCaptureMode.KeyOnly requires this method to complete synchronously
                // and send nothing.
                _ = call.RegisterAndSend();
                resultTask = call.ResultTask;
            }
            else if (peer.IsConnected()) {
                // No rerouting required & connection is there
                _ = call.RegisterAndSend();
                resultTask = call.ResultTask;
            }
            else {
                resultTask = InvokeOnceConnected(call);
            }

            return rpcMethodDef.WrapAsyncInvokerResult(resultTask);
        };
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    private static async Task<T> InvokeOnceConnected<T>(RpcOutboundCall<T> call)
    {
        await call.Peer.WhenConnected(call.ConnectTimeout, call.Context.CancellationToken).ConfigureAwait(false);
        _ = call.RegisterAndSend();
        return await call.ResultTask.ConfigureAwait(false);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    private async Task<T> InvokeWithRerouting<T>(
        Invocation invocation,
        RpcOutboundContext context,
        RpcOutboundCall<T>? call,
        Func<Invocation, Task<T>>? localCallAsyncInvoker)
    {
        var cancellationToken = context.CancellationToken;
        while (true) {
            if (call == null) {
                return localCallAsyncInvoker != null
                    ? await localCallAsyncInvoker.Invoke(invocation).ConfigureAwait(false)
                    : throw RpcRerouteException.LocalCall();
            }

            try {
                Task<T> resultTask;
                if (call.NoWait || context.CacheInfoCapture is { CaptureMode: RpcCacheInfoCaptureMode.KeyOnly }) {
                    // NoWait requires call to be sent no matter what is the connection state now;
                    // RpcCacheInfoCaptureMode.KeyOnly requires this method to complete synchronously
                    // and send nothing.
                    _ = call.RegisterAndSend();
                    resultTask = call.ResultTask;
                }
                else {
                    if (!context.Peer!.ConnectionState.Value.IsConnected())
                        await context.Peer.WhenConnected(call.ConnectTimeout, cancellationToken).ConfigureAwait(false);
                    _ = call.RegisterAndSend();
                    resultTask = call.ResultTask;
                }
                return await resultTask.ConfigureAwait(false);
            }
            catch (RpcRerouteException) {
                Log.LogWarning("Rerouting: {Invocation}", invocation);
                await Hub.RerouteDelayer.Invoke(cancellationToken).ConfigureAwait(false);
                call = (RpcOutboundCall<T>?)context.PrepareReroutedCall();
            }
        }
    }
}
