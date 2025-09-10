using ActualLab.Interception;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

public class RpcSwitchInterceptor : RpcInterceptor
{
    public readonly RpcSafeCallRouter SafeCallRouter;
    public readonly object? LocalTarget;
    public readonly object? RemoteTarget;

    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcSwitchInterceptor(
        RpcInterceptorOptions settings,
        IServiceProvider services,
        RpcServiceDef serviceDef,
        object? localTarget,
        object? remoteTarget)
        : base(settings, services, serviceDef)
    {
        SafeCallRouter = Hub.SafeCallRouter;
        LocalTarget = localTarget;
        RemoteTarget = remoteTarget;
    }

    public override Func<Invocation, object?>? SelectHandler(in Invocation invocation)
        => GetHandler(invocation) ?? (LocalTarget as Interceptor)?.SelectHandler(invocation);

    protected override Func<Invocation, object?>? CreateUntypedHandler(Invocation initialInvocation, MethodDef methodDef)
    {
        var rpcMethodDef = (RpcMethodDef)methodDef;
        var proxy = initialInvocation.Proxy;
        var localCallAsyncInvoker = methodDef.SelectAsyncInvokerUntyped(proxy, LocalTarget)
            ?? throw Errors.NoLocalCallInvoker();
        var remoteCallAsyncInvoker = methodDef.SelectAsyncInvokerUntyped(proxy, RemoteTarget)
            ?? throw Errors.NoRemoteCallInvoker();
        return invocation => {
            var peer = SafeCallRouter.Invoke(rpcMethodDef, invocation.Arguments);
            Task resultTask;
            if (peer.Ref.CanBeRerouted) {
                using var scope = RpcOutboundContext.UseOrActivateNew();
#pragma warning disable CA2025
                resultTask = InvokeWithRerouting(
                    rpcMethodDef, scope.Context, peer,
                    localCallAsyncInvoker, remoteCallAsyncInvoker, invocation);
#pragma warning restore CA2025
            }
            else if (peer.ConnectionKind is RpcPeerConnectionKind.Local) {
                if (localCallAsyncInvoker is null)
                    throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it

                resultTask = localCallAsyncInvoker.Invoke(invocation);
            }
            else {
                using var scope = RpcOutboundContext.UseOrActivateNew();
                var context = scope.Context;
                context.Peer = peer; // We already know the peer, so let's skip RpcCallRouter call
                resultTask = remoteCallAsyncInvoker.Invoke(invocation);
            }
            return rpcMethodDef.UniversalAsyncResultWrapper.Invoke(resultTask);
        };
    }

    private async Task<object?> InvokeWithRerouting(
        RpcMethodDef methodDef,
        RpcOutboundContext context,
        RpcPeer? peer,
        Func<Invocation, Task> localCallAsyncInvoker,
        Func<Invocation, Task> remoteCallAsyncInvoker,
        Invocation invocation)
    {
        for (var tryIndex = 0;; tryIndex++) {
            peer ??= SafeCallRouter.Invoke(methodDef, invocation.Arguments);
            try {
                Task resultTask;
                if (peer.ConnectionKind == RpcPeerConnectionKind.Local) {
                    resultTask = localCallAsyncInvoker.Invoke(invocation);
                }
                else {
                    context.Peer = peer;
                    using (tryIndex != 0 ? context.Activate() : default) // .Activate() is needed only after "await"
                        resultTask = remoteCallAsyncInvoker.Invoke(invocation);
                }
                return await methodDef.TaskToUntypedValueTaskConverter.Invoke(resultTask).ConfigureAwait(false);
            }
            catch (RpcRerouteException) {
                var ctIndex = methodDef.CancellationTokenIndex;
                var cancellationToken = ctIndex >= 0
                    ? invocation.Arguments.GetCancellationToken(ctIndex)
                    : default;
                Log.LogWarning("Rerouting: {Invocation}", invocation);
                await Hub.RerouteDelayer.Invoke(cancellationToken).ConfigureAwait(false);
                peer = null;
            }
        }
    }
}
