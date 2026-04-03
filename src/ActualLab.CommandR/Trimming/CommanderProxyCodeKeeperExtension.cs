using System.Linq.Expressions;
using ActualLab.CommandR.Interception;
using ActualLab.Interception;
using ActualLab.Interception.Trimming;
using ActualLab.Rpc.Trimming;
using ActualLab.Trimming;

namespace ActualLab.CommandR.Trimming;

/// <summary>
/// Retains commander proxy-related code needed for .NET trimming scenarios.
/// </summary>
[UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL2111", Justification = "CodeKeepers are used only to retain the code")]
[UnconditionalSuppressMessage("Trimming", "IL3050", Justification = "CodeKeepers are used only to retain the code")]
public class CommanderProxyCodeKeeperExtension : ProxyCodeKeeper.IExtension
{
    static CommanderProxyCodeKeeperExtension()
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        ProxyCodeKeeper.Extension = new RpcProxyCodeKeeperExtension();
        CodeKeeper.Keep<CommanderBuilder>();

        // Configuration
        CodeKeeper.Keep<CommandHandlerMethodDef>();
        CodeKeeper.Keep<MethodCommandHandler<ICommand>>();
        CodeKeeper.Keep<InterfaceCommandHandler<ICommand>>();

        // Interceptors
        CodeKeeper.Keep<CommandServiceInterceptor>();

        // Stuff that might be forgotten
        ProxyCodeKeeper.KeepAsyncMethod<Unit, ICommand<Unit>, CancellationToken>();
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
    { }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public void KeepMethodResult<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TResult,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>(
        string name)
    {
        if (CodeKeeper.AlwaysTrue)
            return;

        CodeKeeper.Keep<CommandContext<TUnwrapped>>();
        CodeKeeper.Keep<CommandContextExt.TypedCallFactory<TUnwrapped>>();
        CodeKeeper.Keep<Expression<Func<ICommander, ICommand, bool, CommandContext<string>>>>();
    }
}
