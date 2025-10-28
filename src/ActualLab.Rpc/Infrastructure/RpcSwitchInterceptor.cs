using ActualLab.Interception;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

#pragma warning disable CA2025 // Dispose objects before losing scope

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
            using var scope = RpcOutboundContext.UseOrActivateNew();
            var context = scope.Context;
            RpcCallOptions.Use(context, out bool allowRerouting);
            var peer = context.Peer ?? SafeCallRouter.Invoke(rpcMethodDef, invocation.Arguments);

            Task resultTask;
            if (allowRerouting && peer.Ref.CanBeRerouted) {
                resultTask = InvokeWithRerouting(
                    rpcMethodDef, scope.Context, peer,
                    localCallAsyncInvoker, remoteCallAsyncInvoker, invocation);
            }
            else if (peer.ConnectionKind is RpcPeerConnectionKind.Local) {
                if (localCallAsyncInvoker is null)
                    throw RpcRerouteException.MustRerouteToLocal(); // A higher level interceptor should handle it

                using var _ = RpcOutboundContext.Deactivate(); // No RPC expected -> hide RpcOutboundContext
                resultTask = localCallAsyncInvoker.Invoke(invocation);
            }
            else {
                using var _ = RpcCallOptions.Activate(peer); // Suppress downstream rerouting
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
        while (true) {
            try {
                peer ??= SafeCallRouter.Invoke(methodDef, invocation.Arguments); // We already took care of the override
                peer.ThrowIfRerouted();

                Task resultTask;
                if (peer.ConnectionKind is RpcPeerConnectionKind.Local) {
                    using (RpcOutboundContext.Deactivate()) // No RPC expected -> hide RpcOutboundContext
                        resultTask = localCallAsyncInvoker.Invoke(invocation);
                }
                else {
                    using (RpcCallOptions.Activate(peer)) // Suppress downstream rerouting
                    using (context.Activate()) // Provide call context
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
