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
        MethodInfo method
        ) : base(type, method)
    {
        var commandHandler = MethodCommandHandler.TryNew(method.ReflectedType!, method);
        if (commandHandler == null) {
            IsValid = false;
            return; // Can be only when attr.IsEnabled == false
        }

        if (!method.IsVirtual || method.IsFinal)
            throw Errors.WrongInterceptedCommandHandlerMethodSignature(method);

        var parameters = method.GetParameters();
        if (parameters.Length != 2)
            throw Errors.WrongInterceptedCommandHandlerMethodSignature(method);
    }
}
