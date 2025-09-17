using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using ActualLab.Rpc.Trimming;

namespace ActualLab.CommandR.Trimming;

[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
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
        Keep<CommanderExt.TypedCallFactory<TUnwrapped>>();
        Keep<Expression<Func<ICommander, ICommand, bool, CommandContext<string>>>>();
    }
}
