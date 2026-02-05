using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using ActualLab.Rpc.Internal;
using ActualLab.Rpc.Middlewares;

namespace ActualLab.CommandR.Rpc;

/// <summary>
/// An RPC middleware that routes inbound RPC calls for commands through the
/// <see cref="ICommander"/> pipeline instead of direct method invocation.
/// </summary>
public sealed record RpcInboundCommandHandler : IRpcMiddleware
{
    public static Func<RpcMethodDef, bool> DefaultFilter { get; set; } = _ => true;

    public double Priority { get; init; } = RpcInboundMiddlewarePriority.Final;
    public Func<RpcMethodDef, bool> Filter { get; init; } = DefaultFilter;

    public Func<RpcInboundCall, Task<T>> Create<T>(RpcMiddlewareContext<T> context, Func<RpcInboundCall, Task<T>> next)
    {
        var methodDef = context.MethodDef;
        if (!Filter.Invoke(methodDef))
            return next;
        if (methodDef.Kind is not RpcMethodKind.Command || !Filter.Invoke(methodDef))
            return next;

        // The line below suppresses the RpcRouteValidator middleware.
        // RpcCommandHandler.HandleRpcCommand handles "reroute unless local" logic.
        // Search for ".RouteOutboundCall" there to see how it works.
        context.RemainingMiddlewares.RemoveAll(x => x is RpcRouteValidator);

        // This logic is a part of RpcRouteValidator middleware we just suppressed, so we keep it here
        if (methodDef.Service.Mode is RpcServiceMode.Client)
            return _ => throw Errors.PureClientCannotProcessInboundCalls(methodDef.Service.Name);

        ICommander? commander = null;
        return call => {
            commander ??= call.Hub.Services.Commander();
            var args = call.Arguments!;
            var command = (ICommand<T>?)args.Get0Untyped()!;
            if (command is null)
#pragma warning disable CA2208
                throw new ArgumentNullException(nameof(command));
#pragma warning restore CA2208

            var cancellationToken = args.GetCancellationToken(1);
            var commandContext = CommandContext.New(commander, command, isOutermost: true);
            commandContext.Items.KeylessSet(call); // This is the reason we manually create CommandContext here
            return commandContext.Call(cancellationToken);
        };
    }
}
