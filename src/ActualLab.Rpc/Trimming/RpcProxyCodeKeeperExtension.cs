using ActualLab.Interception;
using ActualLab.Interception.Trimming;
using ActualLab.Rpc.Clients;
using ActualLab.Rpc.Diagnostics;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Middlewares;
using ActualLab.Rpc.Serialization;
using ActualLab.Trimming;

namespace ActualLab.Rpc.Trimming;

/// <summary>
/// Retains RPC proxy and serialization code that would otherwise be trimmed by the .NET IL linker.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public class RpcProxyCodeKeeperExtension : ProxyCodeKeeper.IExtension
{
    static RpcProxyCodeKeeperExtension()
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        // Serializable types
        CodeKeeper.KeepSerializable<TypeRef>();

        // Interceptors
        CodeKeeper.Keep<RpcInterceptor>();

        // Configuration
        CodeKeeper.Keep<RpcBuilder>();
        CodeKeeper.Keep<RpcRegistryOptions>();
        CodeKeeper.Keep<RpcPeerOptions>();
        CodeKeeper.Keep<RpcInboundCallOptions>();
        CodeKeeper.Keep<RpcOutboundCallOptions>();
        CodeKeeper.Keep<RpcWebSocketClientOptions>();
        CodeKeeper.Keep<RpcDiagnosticsOptions>();
        CodeKeeper.Keep<RpcMethodDef>();
        CodeKeeper.Keep<RpcServiceDef>();
        CodeKeeper.Keep<RpcServiceRegistry>();
        CodeKeeper.Keep<RpcConfiguration>();
        CodeKeeper.Keep<RpcSerializationFormat>();
        CodeKeeper.Keep<RpcSerializationFormatResolver>();
        CodeKeeper.Keep<RpcByteArgumentSerializerV4>();
        CodeKeeper.Keep<RpcByteMessageSerializerV4>();
        CodeKeeper.Keep<RpcDefaultCallTracer>();

        // Per-hub
        CodeKeeper.Keep<RpcHub>();
        CodeKeeper.Keep<RpcSystemCalls>();

        // Per-peer
        CodeKeeper.Keep<RpcClientPeer>();
        CodeKeeper.Keep<RpcServerPeer>();
        CodeKeeper.Keep<RpcRemoteObjectTracker>();
        CodeKeeper.Keep<RpcSharedObjectTracker>();
        CodeKeeper.Keep<RpcSharedStream>();

        // Per-call
        CodeKeeper.Keep<RpcInboundContext>();
        CodeKeeper.Keep<RpcOutboundContext>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void KeepProxy<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TBase,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TProxy>()
        where TBase : IRequiresAsyncProxy
        where TProxy : IProxy
    { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void KeepMethodArgument<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TArg>(
        string name, int index)
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        CodeKeeper.KeepSerializable<TArg>();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void KeepMethodResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        string name)
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        CodeKeeper.KeepSerializable<TUnwrapped>();
        CodeKeeper.Keep<RpcOutboundCall<TUnwrapped>>();
        CodeKeeper.Keep<RpcInboundCall<TUnwrapped>>();
        CodeKeeper.Keep<RpcInboundNotFoundCall<TUnwrapped>>();
        CodeKeeper.Keep<RpcMiddlewareContext<TUnwrapped>>();

        // RpcMethodDef: invoker factories
        CodeKeeper.Keep<RpcMethodDef.InboundCallServerInvokerFactory<TUnwrapped>>();
        CodeKeeper.Keep<RpcMethodDef.InboundCallMiddlewareInvokerFactory<TUnwrapped>>();
    }
}
