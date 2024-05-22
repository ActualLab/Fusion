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
#pragma warning disable IL2026
            var context = invocation.Context as RpcOutboundContext ?? new();
            var call = context.PrepareCall(rpcMethodDef, invocation.Arguments);
            var peer = context.Peer;
            Task resultTask;
            if (call == null) {
                // No call -> we invoke it locally
                resultTask = peer.Ref.CanBeGone
                    ? GetResultTaskWithRerouting<T>(context, null, invocation.Arguments)
                    : rpcMethodDef.AsyncInvoker.Invoke(rpcMethodDef.Service.Server, invocation.Arguments);
            }
            else if (call.NoWait || context.CacheInfoCapture?.CaptureMode == RpcCacheInfoCaptureMode.KeyOnly) {
                // NoWait requires call to be sent no matter what is the connection state now;
                // RpcCacheInfoCaptureMode.KeyOnly requires this method to complete synchronously
                // and send nothing.
                _ = call.RegisterAndSend();
                resultTask = call.UntypedResultTask;
            }
            else if (peer.Ref.CanBeGone) {
                resultTask = GetResultTaskWithRerouting(context, (RpcOutboundCall<T>?)call, invocation.Arguments);
            }
            else if (!peer.ConnectionState.Value.IsConnected()) {
                resultTask = GetResultTaskOnceConnected((RpcOutboundCall<T>)call);
            }
            else {
                _ = call.RegisterAndSend();
                resultTask = call.UntypedResultTask;
            }
#pragma warning restore IL2026

            return rpcMethodDef.ReturnsTask
                ? resultTask
                : rpcMethodDef.IsAsyncVoidMethod
                    ? resultTask.ToValueTask()
                    : ((Task<T>)resultTask).ToValueTask();
        };
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    private static async Task<T> GetResultTaskOnceConnected<T>(RpcOutboundCall<T> call)
    {
        await call.Peer.WhenConnected(call.ConnectTimeout, call.Context.CancellationToken).ConfigureAwait(false);
        _ = call.RegisterAndSend();
        return await call.ResultTask.ConfigureAwait(false);
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    private static async Task<T> GetResultTaskWithRerouting<T>(
        RpcOutboundContext context, RpcOutboundCall<T>? call, ArgumentList arguments)
    {
        var cancellationToken = context.CancellationToken;
        while (true) {
            try {
                Task<T> resultTask;
                if (call == null) {
                    var methodDef = context.MethodDef;
                    resultTask = (Task<T>)methodDef!.AsyncInvoker.Invoke(methodDef.Service.Server, arguments);
                }
                else {
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
