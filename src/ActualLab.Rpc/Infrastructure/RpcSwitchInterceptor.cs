using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc.Internal;

namespace ActualLab.Rpc.Infrastructure;

#if !NET5_0
[RequiresUnreferencedCode(UnreferencedCode.Rpc)]
#endif
public class RpcSwitchInterceptor : RpcInterceptorBase
{
    public new record Options : RpcInterceptorBase.Options
    {
        public static Options Default { get; set; } = new();
    }

    public Options Settings { get; }
    public object? LocalTarget { get; init; }
    public object? RemoteTarget { get; init; }
    public RpcSafeCallRouter CallRouter { get; }

    // ReSharper disable once ConvertToPrimaryConstructor
    public RpcSwitchInterceptor(Options settings, IServiceProvider services, RpcServiceDef serviceDef)
        : base(settings, services, serviceDef)
    {
        Settings = settings;
        CallRouter = Hub.CallRouter;
    }

    public override Func<Invocation, object?>? SelectHandler(in Invocation invocation)
        => GetHandler(invocation) ?? (LocalTarget as Interceptor)?.SelectHandler(invocation);

    protected override Func<Invocation, object?>? CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>
        (Invocation initialInvocation, MethodDef methodDef)
    {
        var rpcMethodDef = (RpcMethodDef)methodDef;
        var proxy = initialInvocation.Proxy;
        var localCallAsyncInvoker = methodDef.SelectAsyncInvoker<TUnwrapped>(proxy, LocalTarget)
            ?? throw Errors.NoLocalCallInvoker();
        var remoteCallAsyncInvoker = methodDef.SelectAsyncInvoker<TUnwrapped>(proxy, RemoteTarget)
            ?? throw Errors.NoRemoteCallInvoker();
        return invocation => {
            var peer = CallRouter.Invoke(rpcMethodDef, invocation.Arguments);
            Task<TUnwrapped> resultTask;
            if (peer.Ref.CanBeRerouted) {
                resultTask = InvokeWithRerouting(rpcMethodDef, localCallAsyncInvoker, remoteCallAsyncInvoker, invocation, peer);
            }
            else if (peer.ConnectionKind == RpcPeerConnectionKind.LocalCall) {
                resultTask = localCallAsyncInvoker.Invoke(invocation);
            }
            else {
                var context = invocation.Context as RpcOutboundContext ?? new();
                context.Peer = peer; // We already know the peer - this allows to skip its detection
                invocation = invocation.With(context);
                resultTask = remoteCallAsyncInvoker.Invoke(invocation);
            }
            return rpcMethodDef.WrapAsyncInvokerResult(resultTask);
        };
    }

    [RequiresUnreferencedCode(ActualLab.Internal.UnreferencedCode.Serialization)]
    private async Task<T> InvokeWithRerouting<T>(
        RpcMethodDef methodDef,
        Func<Invocation, Task<T>> localCallAsyncInvoker,
        Func<Invocation, Task<T>> remoteCallAsyncInvoker,
        Invocation invocation,
        RpcPeer? peer)
    {
        var context = invocation.Context as RpcOutboundContext ?? new();
        while (true) {
            peer ??= CallRouter.Invoke(methodDef, invocation.Arguments);
            try {
                Task<T> resultTask;
                if (peer.ConnectionKind == RpcPeerConnectionKind.LocalCall) {
                    resultTask = localCallAsyncInvoker.Invoke(invocation);
                }
                else {
                    context.Peer = peer;
                    invocation = invocation.With(context);
                    resultTask = remoteCallAsyncInvoker.Invoke(invocation);
                }
                return await resultTask.ConfigureAwait(false);
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
