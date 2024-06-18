using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc.Caching;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

#if !NET5_0
[RequiresUnreferencedCode(UnreferencedCode.Rpc)]
#endif
public class RpcClientInterceptor : RpcInterceptor
{
    public new record Options : RpcInterceptor.Options
    {
        public static Options Default { get; set; } = new();
    }

    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcClientInterceptor(Options settings, IServiceProvider services, RpcServiceDef serviceDef)
        : base(settings, services, serviceDef)
    { }

    protected override Func<Invocation, object?>? CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>
        (Invocation initialInvocation, MethodDef methodDef)
    {
        var rpcMethodDef = (RpcMethodDef)methodDef;
        return invocation => {
            var context = invocation.Context as RpcOutboundContext ?? new();
            var call = (RpcOutboundCall<TUnwrapped>?)context.PrepareCall(rpcMethodDef, invocation.Arguments);
            var peer = context.Peer!;
            Task<TUnwrapped> resultTask;
            if (peer.Ref.CanBeGone) {
                resultTask = InvokeWithRerouting(context, call);
            }
            else if (call == null) {
                throw RpcRerouteException.LocalCall(); // To be handled by RpcRoutingInterceptor
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
        RpcOutboundContext context,
        RpcOutboundCall<T>? call)
    {
        var cancellationToken = context.CancellationToken;
        while (true) {
            if (call == null)
                throw RpcRerouteException.LocalCall(); // To be handled by RpcRoutingInterceptor
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
                await Hub.RerouteDelayer.Invoke(cancellationToken).ConfigureAwait(false);
                call = (RpcOutboundCall<T>?)context.PrepareReroutedCall();
            }
        }
    }
}
