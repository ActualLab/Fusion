using ActualLab.Interception.Trimming;

namespace ActualLab.Rpc.Trimming;

/// <summary>
/// Retains RPC method definition code that would otherwise be trimmed by the .NET IL linker.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public class RpcMethodDefCodeKeeper : MethodDefCodeKeeper
{
    public override void KeepCodeForResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>()
    {
        if (AlwaysTrue)
            return;

        base.KeepCodeForResult<TResult, TUnwrapped>();
        Keep<RpcMethodDef.InboundCallServerInvokerFactory<TUnwrapped>>();
        Keep<RpcMethodDef.InboundCallMiddlewareInvokerFactory<TUnwrapped>>();
    }
}
