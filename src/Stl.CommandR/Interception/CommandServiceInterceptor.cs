using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Internal;
using ActualLab.Interception;
using ActualLab.Interception.Interceptors;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.CommandR.Interception;

#if !NET5_0
[RequiresUnreferencedCode(UnreferencedCode.Commander)]
#endif
public class CommandServiceInterceptor(CommandServiceInterceptor.Options settings, IServiceProvider services)
    : InterceptorBase(settings, services)
{
    public new record Options : InterceptorBase.Options;

    protected readonly ICommander Commander = services.GetRequiredService<ICommander>();

    protected override Func<Invocation, object?> CreateHandler<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>
        (Invocation initialInvocation, MethodDef methodDef)
        => invocation => {
            var arguments = invocation.Arguments;
            var command = arguments.Get<ICommand>(0);
            var context = CommandContext.Current;
            if (context == null) {
                var rpcInboundCallContext = RpcInboundContext.Current;
                if (rpcInboundCallContext != null) {
                    var call = rpcInboundCallContext.Call;
                    var callMethodDef = call.MethodDef;
                    var callServiceDef = callMethodDef.Service;
                    var callArguments = call.Arguments;
                    if (callArguments is { Length: <= 2 }
                        && callServiceDef.HasServer
                        && Equals(callMethodDef.Method.Name, invocation.Method.Name)
                        && callServiceDef.Type.IsInstanceOfType(invocation.Proxy)
                        && ReferenceEquals(callArguments.GetUntyped(0), command)) {
                        var cancellationToken = callArguments.Length == 2
                            ? arguments.GetCancellationToken(1)
                            : default;

                        var resultTask = Commander.Call(command, isOutermost: true, cancellationToken);
                        return methodDef.ReturnsTask
                            ? resultTask
                            : methodDef.IsAsyncVoidMethod
                                ? resultTask.ToValueTask()
                                : ((Task<T>)resultTask).ToValueTask();
                    }
                }
                // We're outside the ICommander pipeline
                // and current inbound Rpc call isn't "ours"
                throw Errors.DirectCommandHandlerCallsAreNotAllowed();
            }
            if (!ReferenceEquals(command, context.UntypedCommand)) {
                // We're outside the ICommander pipeline
                throw Errors.DirectCommandHandlerCallsAreNotAllowed();
            }

            // We're already inside the ICommander pipeline created for exactly this command
            return invocation.InterceptedUntyped();
        };

    // We don't need to decorate this method with any dynamic access attributes
    protected override MethodDef? CreateMethodDef(MethodInfo method, Invocation initialInvocation)
    {
        try {
            var type = initialInvocation.Proxy.GetType().NonProxyType();
#pragma warning disable IL2072
            var methodDef = new CommandHandlerMethodDef(type, method);
#pragma warning restore IL2072
            return methodDef.IsValid ? methodDef : null;
        }
        catch {
            // CommandHandlerMethodDef may throw an exception,
            // which means methodDef isn't valid as well.
            return null;
        }
    }

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
#pragma warning disable IL2026
            var attr = MethodCommandHandler.GetAttribute(method);
#pragma warning restore IL2026
            if (attr == null)
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
                    "- {Method}: has [{Attribute}(false)]", method.ToString(), attributeName);
            else
                ValidationLog?.Log(ValidationLogLevel,
                    "+ {Method}: [{Attribute}(" +
                    "Priority = {Priority}" +
                    ")]", method.ToString(), attributeName, attr.Priority);
        }
    }
}
