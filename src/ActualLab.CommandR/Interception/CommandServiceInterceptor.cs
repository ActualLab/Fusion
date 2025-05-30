using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Internal;
using ActualLab.Interception;
using ActualLab.OS;
using ActualLab.Rpc.Infrastructure;

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
            var arguments = invocation.Arguments;
            var command = arguments.Get<ICommand>(0);
            var context = CommandContext.Current;
            if (context == null) {
                // The logic below detects inbound RPC calls & reroutes them to local Commander
                var rpcInboundContext = RpcInboundContext.Current;
                if (rpcInboundContext != null) {
                    var call = rpcInboundContext.Call;
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

                        // "isOutermost: true" also guarantees that RpcInboundContext.Current is null
                        // when it enters the same interceptor once again
                        var resultTask = Commander.Call(command, isOutermost: true, cancellationToken);
                        return methodDef.ReturnsTask
                            ? resultTask
                            : methodDef.IsAsyncVoidMethod
                                ? resultTask.ToValueTask()
                                : ((Task<TUnwrapped>)resultTask).ToValueTask();
                    }
                }

                // We're outside the ICommander pipeline
                // and current inbound Rpc call isn't "ours"
                throw Errors.DirectCommandHandlerCallsAreNotAllowed();
            }

            var contextCommand = context.UntypedCommand;
            if (!ReferenceEquals(command, contextCommand) && contextCommand is not ISystemCommand) {
                // We're outside the ICommander pipeline
                throw Errors.DirectCommandHandlerCallsAreNotAllowed();
            }

            // We're already inside the ICommander pipeline created for exactly this command
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
