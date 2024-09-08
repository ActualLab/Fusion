using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Trimming;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Interception.Trimming;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Trimming;

public class FusionProxyCodeKeeper : ProxyCodeKeeper
{
    // CommanderProxyCodeKeeper is also RpcProxyCodeKeeper
    private readonly CommanderProxyCodeKeeper _commanderProxyCodeKeeper = Get<CommanderProxyCodeKeeper>();

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ComputeMethodFunction<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcInboundComputeCall<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcOutboundComputeCall<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RemoteComputeMethodFunction<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FuncComputedState<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ComputedSource<>))]
    static FusionProxyCodeKeeper()
        => _ = default(FusionBuilder).Services;

    public override void KeepMethodArgument<T>()
        => _commanderProxyCodeKeeper.KeepMethodArgument<T>();

    public override void KeepMethodResult<TResult, TUnwrapped>()
    {
        _commanderProxyCodeKeeper.KeepMethodResult<TResult, TUnwrapped>();
        if (AlwaysTrue)
            return;

        Keep<ComputeMethodFunction<TUnwrapped>>();
        Keep<RemoteComputeMethodFunction<TUnwrapped>>();

        var outboundContext = CallSilently(() => new RpcOutboundContext());
        var inboundContext = CallSilently(() => new RpcInboundContext(null!, null!, default));
        CallSilently(() => new RpcOutboundComputeCall<TUnwrapped>(outboundContext));
        CallSilently(() => new RpcInboundComputeCall<TUnwrapped>(inboundContext, null!));
    }
}
