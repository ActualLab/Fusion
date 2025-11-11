using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception.Trimming;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.Rpc.Trimming;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public class RpcProxyCodeKeeper : ProxyCodeKeeper
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcInboundCall<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcInbound404Call<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(RpcOutboundCall<>))]
    static RpcProxyCodeKeeper()
        => _ = default(RpcBuilder).Services;

    public override void KeepMethodArgument<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TArg>(string name = "", int index = -1)
    {
        KeepSerializable<TArg>();
        base.KeepMethodArgument<TArg>(name, index);
    }

    public override void KeepMethodResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(string name = "")
    {
        if (AlwaysTrue)
            return;

        base.KeepMethodResult<TResult, TUnwrapped>(name);

        KeepSerializable<TUnwrapped>();
        Keep<RpcMethodDefCodeKeeper>().KeepCodeForResult<TResult, TUnwrapped>();

        // RpcInbound/OutboundXxx
        var outboundContext = CallSilently(() => new RpcOutboundContext());
        var inboundContext = CallSilently(() => new RpcInboundContext(null!, null!, default));
        CallSilently(() => new RpcOutboundCall<TUnwrapped>(outboundContext));
        CallSilently(() => new RpcInboundCall<TUnwrapped>(inboundContext, null!));
        CallSilently(() => new RpcInbound404Call<TUnwrapped>(inboundContext, null!));
    }
}
