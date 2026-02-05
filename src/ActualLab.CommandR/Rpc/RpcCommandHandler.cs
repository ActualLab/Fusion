using ActualLab.CommandR.Internal;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.CommandR.Rpc;

/// <summary>
/// A command filter that routes commands to remote RPC peers or executes them
/// locally, with automatic rerouting on topology changes.
/// </summary>
public sealed class RpcCommandHandler(IServiceProvider services) : ICommandHandler<ICommand>
{
    private IServiceProvider Services { get; } = services;
    private RpcHub RpcHub { get; } = services.RpcHub();
    private ILogger Log => field ??= Services.LogFor(GetType());

    [CommandFilter(Priority = CommanderCommandHandlerPriority.RpcRoutingCommandHandler)]
    public Task OnCommand(ICommand command, CommandContext context, CancellationToken cancellationToken)
    {
        var finalHandler = context.ExecutionState.Handlers.FinalHandler;
        if (finalHandler is not IMethodCommandHandler methodHandler)
            return context.InvokeRemainingHandlers(cancellationToken); // Final handler isn't a method handler

        var serviceType = methodHandler.ServiceType;
        var method = methodHandler.Method;
        if (GetRpcMethodDef(serviceType, method) is not { } rpcMethodDef)
            return context.InvokeRemainingHandlers(cancellationToken); // Not an RPC method

        var isDistributedOrClient =
            rpcMethodDef.Service.Mode is RpcServiceMode.Distributed
            || serviceType.NonProxyType().IsInterface; // The final handler's service is an interface
        if (!isDistributedOrClient)
            return context.InvokeRemainingHandlers(cancellationToken);

        // If we're here, the final handler's service is a distributed RPC service or a pure RPC client

        var isInboundRpc = context.IsOutermost && context.Items.KeylessGet<RpcInboundCall>() is not null;
        var routingMode = isInboundRpc ? RpcRoutingMode.Inbound : RpcRoutingMode.Outbound;
        return HandleWithRerouting();

        async Task HandleWithRerouting() {
            var baseExecutionState = context.ExecutionState;
            var preFinalExecutionState = baseExecutionState.PreFinalState;
            var rerouteCount = 0;
            while (true) {
                var args = ArgumentList.New(command, cancellationToken);
                var peer = rpcMethodDef.RouteCall(args, routingMode);
                var routeState = peer.Ref.RouteState;
                try {
                    if (peer.ConnectionKind is not RpcPeerConnectionKind.Local) {
                        // Remote call -> send the RPC call by invoking the final handler
                        context.ExecutionState = preFinalExecutionState;
                        context.Items.KeylessSet(new RpcOutboundCallSetup(peer));
                        await context.InvokeRemainingHandlers(cancellationToken).ConfigureAwait(false);
                        return;
                    }

                    // Local call -> continue the pipeline
                    var linkedCts = await routeState
                        // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                        .PrepareLocalExecution(rpcMethodDef, cancellationToken)
                        .ConfigureAwait(false);
                    try {
                        context.ExecutionState = baseExecutionState;
                        context.Items.KeylessSet(new RpcOutboundCallSetup(peer));
                        await context
                            .InvokeRemainingHandlers(linkedCts?.Token ?? cancellationToken)
                            .ConfigureAwait(false);
                        return;
                    }
                    // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                    catch (OperationCanceledException e) when (routeState.MustConvertToRpcRerouteException(e, linkedCts, cancellationToken)) {
                        throw RpcRerouteException.MustReroute();
                    }
                    finally {
                        linkedCts.CancelAndDisposeSilently();
                    }
                }
                catch (RpcRerouteException e) {
                    Services.ThrowIfDisposedOrDisposing();
                    ++rerouteCount;
                    context.ResetResult();
                    Log.LogWarning(e, "Rerouting command #{RerouteCount}: {Command}",
                        rerouteCount, context.UntypedCommand);
                    await RpcHub.InternalServices.OutboundCallOptions
                        .GetReroutingDelay(rerouteCount, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
    }

    // Private methods

    private RpcMethodDef? GetRpcMethodDef(Type serviceType, MethodInfo method)
    {
        var serviceDef = RpcHub.ServiceRegistry.Get(serviceType);
        if (serviceDef is null || !serviceDef.Mode.IsAnyClient())
            return null; // Not a client or distributed RPC service

        var methodDef = serviceDef.FindMethod(method);
        if (methodDef is null)
            return null; // The handling method isn't exposed via RPC

        if (methodDef.ParameterTypes is not { Length: 2 }) {
            // A bug in user code: the method must have just 2 parameters
            Log.LogError(
                "RpcMethodDef matching '{ServiceType}.{Method}' must have 2 parameters instead of {ParameterCount}",
                serviceType.GetName(), method.Name, methodDef.ParameterTypes);
            return null;
        }
        return methodDef;
    }
}
