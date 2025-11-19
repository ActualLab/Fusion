using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Internal;
using ActualLab.Interception;
using ActualLab.OS;

namespace ActualLab.CommandR.Interception;

public sealed class CommandServiceInterceptor(CommandServiceInterceptor.Options settings, IServiceProvider services)
    : Interceptor(settings, services)
{
    public new record Options : Interceptor.Options
    {
        public static Options Default { get; set; } = new() {
            // This interceptor is shared, so we adjust its cache concurrency settings
            HandlerCacheConcurrencyLevel = HardwareInfo.GetProcessorCountPo2Factor(2),
            HandlerCacheCapacity = 131,
        };
    }

    public readonly ICommander Commander = services.Commander();

    protected override Func<Invocation, object?>? CreateTypedHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] TUnwrapped>
        (Invocation initialInvocation, MethodDef methodDef)
        => invocation => {
            if (CommandContext.Current is not { } context)
                // We're outside the Commander pipeline
                throw Errors.DirectCommandHandlerCallsAreNotAllowed();

            var contextCommand = context.UntypedCommand;
            var invocationCommand = (ICommand?)invocation.Arguments.Get0Untyped();
            if (!ReferenceEquals(invocationCommand, contextCommand) && contextCommand is not ISystemCommand) {
                // The context command doesn't match the invocation command
                throw Errors.DirectCommandHandlerCallsAreNotAllowed();
            }

            // All checks passed, so it's safe to proceed
            return invocation.InvokeInterceptedUntyped();
        };

    // We don't need to decorate this method with any dynamic access attributes
    [UnconditionalSuppressMessage("Trimming", "IL2072", Justification = "We assume all command handling code is preserved")]
    protected override MethodDef? CreateMethodDef(MethodInfo method,
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type proxyType)
    {
        try {
            var type = proxyType.NonProxyType();
            var methodDef = new CommandHandlerMethodDef(type, method);
            return methodDef.IsValid ? methodDef : null;
        }
        catch {
            // CommandHandlerMethodDef may throw an exception,
            // which means methodDef isn't valid as well.
            return null;
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "We assume all command handling code is preserved")]
    protected override void ValidateTypeInternal(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] Type type)
    {
        if (typeof(ICommandHandler).IsAssignableFrom(type))
            throw Errors.OnlyInterceptedCommandHandlersAllowed(type);

        var bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var methods = type.IsInterface
            ? type.GetAllInterfaceMethods(bindingFlags)
            : type.GetMethods(bindingFlags);
        foreach (var method in methods) {
            if (method.DeclaringType == typeof(object))
                continue;
            var attr = MethodCommandHandler.GetAttribute(method);
            if (attr is null)
                continue;

            var methodDef = new CommandHandlerMethodDef(type, method);
            var attributeName = attr.GetType().GetName()
#if NETSTANDARD2_0
                .Replace(nameof(Attribute), "");
#else
                .Replace(nameof(Attribute), "", StringComparison.Ordinal);
#endif
            if (!methodDef.IsValid) // attr.IsEnabled == false
                ValidationLog?.Log(ValidationLogLevel,
                    "- {Method}: has [{Attribute}(false)]", method.ToShortString(), attributeName);
            else
                ValidationLog?.Log(ValidationLogLevel,
                    "+ {Method}: [{Attribute}(" +
                    "Priority = {Priority}" +
                    ")]", method.ToShortString(), attributeName, attr.Priority);
        }
    }
}
