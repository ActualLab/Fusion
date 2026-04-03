using ActualLab.CommandR.Trimming;
using ActualLab.Fusion.Client.Interception;
using ActualLab.Fusion.Client.Internal;
using ActualLab.Fusion.Interception;
using ActualLab.Interception;
using ActualLab.Interception.Trimming;
using ActualLab.Trimming;

namespace ActualLab.Fusion.Trimming;

/// <summary>
/// A code keeper extension that prevents the .NET trimmer from removing Fusion-specific proxy types,
/// computed functions, and RPC call types.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public class FusionProxyCodeKeeperExtension : ProxyCodeKeeper.IExtension
{
    static FusionProxyCodeKeeperExtension()
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        ProxyCodeKeeper.Extension = new CommanderProxyCodeKeeperExtension();

        // Interceptors
        CodeKeeper.Keep<ComputeServiceInterceptor>();
        CodeKeeper.Keep<RemoteComputeServiceInterceptor>();

        // Other services
        CodeKeeper.Keep<RpcComputeSystemCalls>();
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

        CodeKeeper.Keep(typeof(Completion<>).MakeGenericType(typeof(TArg)));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void KeepMethodResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        string name)
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        CodeKeeper.Keep<ComputeMethodFunction<TUnwrapped>>();
        CodeKeeper.Keep<ConsolidatingComputeMethodFunction<TUnwrapped>>();
        CodeKeeper.Keep<RemoteComputeMethodFunction<TUnwrapped>>();
        CodeKeeper.Keep<ComputeFunctionExt.CompleteProduceValuePromiseFactory<TUnwrapped>>();
        CodeKeeper.Keep<ComputeFunctionExt.CompleteProduceValuePromiseWithSynchronizerFactory<TUnwrapped>>();
        CodeKeeper.Keep<RpcOutboundComputeCall<TUnwrapped>>();
        CodeKeeper.Keep<RpcInboundComputeCall<TUnwrapped>>();
    }
}
