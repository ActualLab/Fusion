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

    public override void KeepMethodArgument<TArg>(string name = "", int index = -1)
    {
        KeepSerializable<TArg>();
        base.KeepMethodArgument<TArg>(name, index);
    }

    public override void KeepMethodResult<TResult, TUnwrapped>(string name = "")
    {
        KeepSerializable<TUnwrapped>();
        base.KeepMethodResult<TResult, TUnwrapped>(name);
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
