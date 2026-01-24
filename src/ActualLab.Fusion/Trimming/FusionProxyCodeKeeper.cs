using ActualLab.CommandR.Trimming;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Interception;
using ActualLab.Interception.Trimming;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Fusion.Trimming;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public class FusionProxyCodeKeeper : ProxyCodeKeeper
{
    // CommanderProxyCodeKeeper is also RpcProxyCodeKeeper
    private static readonly CommanderProxyCodeKeeper CommanderProxyCodeKeeper = Get<CommanderProxyCodeKeeper>();

    static FusionProxyCodeKeeper()
        => _ = default(FusionBuilder).Services;

    public override void KeepMethodArgument<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TArg>(string name = "", int index = -1)
    {
        if (AlwaysTrue)
            return;

        CommanderProxyCodeKeeper.KeepMethodArgument<TArg>(name, index);
        KeepUnconstructable(typeof(Completion<>).MakeGenericType(typeof(TArg)));
    }

    public override void KeepMethodResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(string name = "")
    {
        if (AlwaysTrue)
            return;

        CommanderProxyCodeKeeper.KeepMethodResult<TResult, TUnwrapped>(name);

        Keep<ComputeMethodFunction<TUnwrapped>>();
        Keep<ConsolidatingComputeMethodFunction<TUnwrapped>>();
        Keep<RemoteComputeMethodFunction<TUnwrapped>>();
        Keep<ComputeFunctionExt.CompleteProduceValuePromiseFactory<TUnwrapped>>();
        Keep<ComputeFunctionExt.CompleteProduceValuePromiseWithSynchronizerFactory<TUnwrapped>>();

        var outboundContext = CallSilently(() => new RpcOutboundContext());
        var inboundContext = CallSilently(() => new RpcInboundContext(null!, (RpcInboundMessage)null!, default));
        CallSilently(() => new RpcOutboundComputeCall<TUnwrapped>(outboundContext));
        CallSilently(() => new RpcInboundComputeCall<TUnwrapped>(inboundContext));
    }
}
