using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Internal;
using ActualLab.Interception;

namespace ActualLab.CommandR.Interception;

public sealed class CommandHandlerMethodDef : MethodDef
{
    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume all command handling code is preserved")]
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume all command handling code is preserved")]
    public CommandHandlerMethodDef(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type,
        MethodInfo methodInfo
        ) : base(type, methodInfo)
    {
        var commandHandler = MethodCommandHandler.TryNew(methodInfo.ReflectedType!, methodInfo);
        if (commandHandler is null) {
            IsValid = false;
            return; // Can be only when attr.IsEnabled == false
        }

        if (!methodInfo.IsVirtual || methodInfo.IsFinal)
            throw Errors.WrongInterceptedCommandHandlerMethodSignature(methodInfo);

        var parameters = methodInfo.GetParameters();
        if (parameters.Length != 2)
            throw Errors.WrongInterceptedCommandHandlerMethodSignature(methodInfo);
    }
}
