using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

#if !NET5_0
[RequiresUnreferencedCode(UnreferencedCode.Rpc)]
#endif
public sealed class RpcClientInterceptor(
    RpcClientInterceptor.Options settings,
    IServiceProvider services,
    RpcServiceDef serviceDef
    ) : RpcInterceptorBase(settings, services, serviceDef)
{
    public new record Options : RpcInterceptorBase.Options
    {
        public static Options Default { get; set; } = new();
    }

    protected override Func<Invocation, object?> CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
        (Invocation initialInvocation, MethodDef methodDef)
    {
        var rpcMethodDef = (RpcMethodDef)methodDef;
        return invocation => {
            var context = invocation.Context as RpcOutboundContext ?? new();
            var call = (RpcOutboundCall<T>?)context.PrepareCall(rpcMethodDef, invocation.Arguments);
            var peer = context.Peer;
            Task<T> resultTask;
            if (peer.Ref.CanBeGone)
                // Requires rerouting
                resultTask = InvokeWithRerouting(context, call, invocation);
            else if (call == null)
                // peer.ConnectionKind == RpcPeerConnectionKind.LocalCall
                resultTask = (Task<T>)rpcMethodDef.AsyncInvoker.Invoke(rpcMethodDef.Service.Server, invocation.Arguments);
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
            else
                resultTask = InvokeOnceConnected(call);

            return rpcMethodDef.UnwrapAsyncInvokerResult(resultTask);
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
    private static async Task<T> InvokeWithRerouting<T>(
        RpcOutboundContext context, RpcOutboundCall<T>? call, Invocation invocation)
    {
        var cancellationToken = context.CancellationToken;
        while (true) {
            try {
                Task<T> resultTask;
                if (call == null) {
                    var methodDef = context.MethodDef;
                    resultTask = (Task<T>)methodDef!.AsyncInvoker.Invoke(methodDef.Service.Server, invocation.Arguments);
                }
                else if (call.NoWait || context.CacheInfoCapture is { CaptureMode: RpcCacheInfoCaptureMode.KeyOnly }) {
                    // NoWait requires call to be sent no matter what is the connection state now;
                    // RpcCacheInfoCaptureMode.KeyOnly requires this method to complete synchronously
                    // and send nothing.
                    _ = call.RegisterAndSend();
                    resultTask = call.ResultTask;
                }
                else {
                    if (!context.Peer.ConnectionState.Value.IsConnected())
                        await context.Peer.WhenConnected(call.ConnectTimeout, cancellationToken).ConfigureAwait(false);
                    _ = call.RegisterAndSend();
                    resultTask = call.ResultTask;
                }
                return await resultTask.ConfigureAwait(false);
            }
            catch (RpcRerouteException) {
                call = (RpcOutboundCall<T>?)context.PrepareReroutedCall();
            }
        }
    }
}
