using System.Diagnostics.CodeAnalysis;
using ActualLab.Rpc.Trimming;

namespace ActualLab.CommandR.Trimming;

public class CommanderProxyCodeKeeper : RpcProxyCodeKeeper
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(CommandContext<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(MethodCommandHandler<>))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(InterfaceCommandHandler<>))]
    static CommanderProxyCodeKeeper()
    { }

    public override void KeepMethodResult<TResult, TUnwrapped>()
    {
        base.KeepMethodResult<TResult, TUnwrapped>();
        if (AlwaysTrue)
            return;

        Keep<CommandContext<TUnwrapped>>();
    }
}
