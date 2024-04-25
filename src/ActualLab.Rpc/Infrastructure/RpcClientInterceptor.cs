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
            if (call == null) {
                // No call == no peer -> we invoke it locally
                var server = rpcMethodDef.Service.Server;
                return rpcMethodDef.Invoker.Invoke(server, invocation.Arguments);
            }

            var peer = call.Peer;
            Task resultTask;
            if (call.NoWait || context.CacheInfoCapture?.CaptureMode == RpcCacheInfoCaptureMode.KeyOnly) {
                // NoWait requires call to be sent no matter what is the connection state now;
                // RpcCacheInfoCaptureMode.KeyOnly requires this method to complete synchronously
                // and + send nothing.
                _ = call.RegisterAndSend();
                resultTask = call.UntypedResultTask;
            }
            else if (peer.Ref.CanBecomeObsolete)
                resultTask = GetResultTaskWithRerouting((RpcOutboundCall<T>)call);
            else if (!peer.ConnectionState.Value.IsConnected())
                resultTask = GetResultTaskOnceConnected((RpcOutboundCall<T>)call);
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
    private static async Task<T> GetResultTaskWithRerouting<T>(RpcOutboundCall<T> call)
    {
        var context = call.Context;
        var cancellationToken = context.CancellationToken;
        while (true) {
            await call.Peer.WhenConnected(call.ConnectTimeout, cancellationToken).ConfigureAwait(false);
            _ = call.RegisterAndSend();
            try {
                return await call.ResultTask.ConfigureAwait(false);
            }
            catch (RpcRerouteException) {
                if (context.PrepareReroutedCall() is not RpcOutboundCall<T> reroutedCall)
                    break;

                call = reroutedCall;
            }
        }

        // If we're here, rerouting ended with null call -> we invoke it locally
        var server = call.MethodDef.Service.Server;
        var resultTask = (Task<T>)call.MethodDef.Invoker.Invoke(server, context.Arguments!);
        return await resultTask.ConfigureAwait(false);
    }
}
