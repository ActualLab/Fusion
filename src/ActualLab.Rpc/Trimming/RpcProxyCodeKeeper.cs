using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception.Trimming;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Trimming;

public class RpcProxyCodeKeeper : ProxyCodeKeeper
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcInboundCall<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcInbound404Call<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcOutboundCall<>))]
    static RpcProxyCodeKeeper()
        => _ = default(RpcBuilder).Services;

    public override void KeepMethodArgument<T>()
    {
        KeepSerializable<T>();
        base.KeepMethodArgument<T>();
    }

    public override void KeepMethodResult<TResult, TUnwrapped>()
    {
        KeepSerializable<TUnwrapped>();
        base.KeepMethodResult<TResult, TUnwrapped>();
        if (AlwaysTrue)
            return;

        // RpcInbound/OutboundXxx
        var outboundContext = CallSilently(() => new RpcOutboundContext());
        var inboundContext = CallSilently(() => new RpcInboundContext(null!, null!, default));
        CallSilently(() => new RpcOutboundCall<TUnwrapped>(outboundContext));
        CallSilently(() => new RpcInboundCall<TUnwrapped>(inboundContext, null!));
        CallSilently(() => new RpcInbound404Call<TUnwrapped>(inboundContext, null!));
    }
}
