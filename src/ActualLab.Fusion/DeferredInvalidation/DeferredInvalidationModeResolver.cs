using ActualLab.CommandR.Operations;
using ActualLab.Fusion.Internal;

namespace ActualLab.Fusion;

/// <summary>
/// Tells which <see cref="DeferredInvalidationMode"/> applies to the command that's running now:
/// it reads the <see cref="DeferredInvalidationModeAttribute"/> of the handler's method, then of
/// the implementation the container maps its service type to, then of the method's declaring type.
/// Override any of its methods to resolve some or all of that differently.
/// </summary>
/// <remarks>
/// There is no fallback mode. A handler that defers a block declares how far that invalidation
/// has to reach, because nothing else can know: a wrong guess either leaves other hosts stale or
/// pays for a broadcast nobody needed. So an undeclared mode is an error, not a default.
/// </remarks>
public class DeferredInvalidationModeResolver(ServiceTypeResolver serviceTypeResolver)
{
    protected ServiceTypeResolver ServiceTypeResolver { get; } = serviceTypeResolver;

    // The mode of the command that's running now - which is the one whose handler is adding a
    // block, not the one the context was opened for
    public virtual DeferredInvalidationMode Resolve()
        => CommandContext.Current is { } commandContext
            ? Resolve(commandContext.Services, commandContext.UntypedCommand)
            : throw Errors.DeferredInvalidationModeIsUndeclared(null);

    public DeferredInvalidationMode Resolve(IServiceProvider services, ICommand command)
    {
        var finalHandler = services
            .GetRequiredService<CommandHandlerResolver>()
            .GetCommandHandlerChain(command)
            .FinalHandler;
        return finalHandler is IMethodCommandHandler handler
            ? Resolve(handler)
            : throw Errors.DeferredInvalidationModeIsUndeclared(command.GetType());
    }

    public virtual DeferredInvalidationMode Resolve(IMethodCommandHandler handler)
    {
        // A handler declared on an interface (IAuth, etc.) carries no mode of its own, so the
        // implementation the container maps that interface to is where the declaration lives
        var implementationType = ServiceTypeResolver.TryResolveImplementationType(handler.ServiceType);
        var attribute = DeferredInvalidationModeAttribute.Get(handler.Method, implementationType);
        return attribute?.Mode
            ?? throw Errors.DeferredInvalidationModeIsUndeclared(handler.Method.DeclaringType);
    }
}
