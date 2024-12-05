using System.Diagnostics.CodeAnalysis;
using ActualLab.Rpc.Trimming;

namespace ActualLab.CommandR.Trimming;

public class CommanderProxyCodeKeeper : RpcProxyCodeKeeper
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CommandContext<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(MethodCommandHandler<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(InterfaceCommandHandler<>))]
    static CommanderProxyCodeKeeper()
        => _ = default(CommanderBuilder).Services;

    public override void KeepMethodResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(string name = "")
    {
        base.KeepMethodResult<TResult, TUnwrapped>(name);
        if (AlwaysTrue)
            return;

        Keep<CommandContext<TUnwrapped>>();
    }
}
