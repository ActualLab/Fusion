using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Trimming;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Interception;
using ActualLab.Fusion.Internal;
using ActualLab.Interception.Trimming;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Trimming;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public class FusionProxyCodeKeeper : ProxyCodeKeeper
{
    // CommanderProxyCodeKeeper is also RpcProxyCodeKeeper
    private readonly CommanderProxyCodeKeeper _commanderProxyCodeKeeper = Get<CommanderProxyCodeKeeper>();

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ComputeMethodFunction<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ConsolidatingComputeMethodFunction<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcInboundComputeCall<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcOutboundComputeCall<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RemoteComputeMethodFunction<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FuncComputedState<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ComputedSource<>))]
    static FusionProxyCodeKeeper()
        => _ = default(FusionBuilder).Services;

    public override void KeepMethodArgument<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TArg>(string name = "", int index = -1)
        => _commanderProxyCodeKeeper.KeepMethodArgument<TArg>(name, index);

    public override void KeepMethodResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(string name = "")
    {
        _commanderProxyCodeKeeper.KeepMethodResult<TResult, TUnwrapped>(name);
        if (AlwaysTrue)
            return;

        Keep<ComputeMethodFunction<TUnwrapped>>();
        Keep<ConsolidatingComputeMethodFunction<TUnwrapped>>();
        Keep<RemoteComputeMethodFunction<TUnwrapped>>();
        Keep<ComputeFunctionExt.CompleteProduceValuePromiseFactory<TUnwrapped>>();
        Keep<ComputeFunctionExt.CompleteProduceValuePromiseWithSynchronizerFactory<TUnwrapped>>();

        var outboundContext = CallSilently(() => new RpcOutboundContext());
        var inboundContext = CallSilently(() => new RpcInboundContext(null!, null!, default));
        CallSilently(() => new RpcOutboundComputeCall<TUnwrapped>(outboundContext));
        CallSilently(() => new RpcInboundComputeCall<TUnwrapped>(inboundContext, null!));
    }
}
