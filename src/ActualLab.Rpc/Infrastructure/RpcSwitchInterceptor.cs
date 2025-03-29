using ActualLab.Interception;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

public class RpcSwitchInterceptor : RpcInterceptorBase
{
    public readonly RpcSafeCallRouter CallRouter;
    public readonly object? LocalTarget;
    public readonly object? RemoteTarget;

    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcSwitchInterceptor(
        RpcInterceptorOptions settings, IServiceProvider services,
        RpcServiceDef serviceDef,
        object? localTarget,
        object? remoteTarget)
        : base(settings, services, serviceDef)
    {
        CallRouter = Hub.CallRouter;
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
            var peer = CallRouter.Invoke(rpcMethodDef, invocation.Arguments);
            Task resultTask;
            if (peer.Ref.CanBeRerouted)
                resultTask = InvokeWithRerouting(rpcMethodDef, localCallAsyncInvoker, remoteCallAsyncInvoker, invocation, peer);
            else if (peer.ConnectionKind == RpcPeerConnectionKind.Local) {
                if (localCallAsyncInvoker == null)
                    throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it

                resultTask = localCallAsyncInvoker.Invoke(invocation);
            }
            else {
                var context = invocation.Context as RpcOutboundContext ?? RpcOutboundContext.Current ?? new();
                context.Peer = peer; // We already know the peer, so let's skip RpcCallRouter call
                invocation = invocation.With(context);
                resultTask = remoteCallAsyncInvoker.Invoke(invocation);
            }
            return rpcMethodDef.UniversalAsyncResultWrapper.Invoke(resultTask);
        };
    }

    private async Task<object?> InvokeWithRerouting(
        RpcMethodDef methodDef,
        Func<Invocation, Task> localCallAsyncInvoker,
        Func<Invocation, Task> remoteCallAsyncInvoker,
        Invocation invocation,
        RpcPeer? peer)
    {
        var context = invocation.Context as RpcOutboundContext ?? RpcOutboundContext.Current ?? new();
        while (true) {
            peer ??= CallRouter.Invoke(methodDef, invocation.Arguments);
            try {
                Task resultTask;
                if (peer.ConnectionKind == RpcPeerConnectionKind.Local) {
                    resultTask = localCallAsyncInvoker.Invoke(invocation);
                }
                else {
                    context.Peer = peer;
                    invocation = invocation.With(context);
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
