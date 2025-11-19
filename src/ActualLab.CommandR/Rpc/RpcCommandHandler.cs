using System.Diagnostics.CodeAnalysis;
using ActualLab.CommandR.Internal;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.CommandR.Rpc;

public sealed class RpcCommandHandler(IServiceProvider services) : ICommandHandler<ICommand>
{
    private IServiceProvider Services { get; } = services;
    private RpcHub RpcHub { get; } = services.RpcHub();
    [field: AllowNull, MaybeNull]
    private ILogger Log => field ??= Services.LogFor(GetType());

    [CommandFilter(Priority = CommanderCommandHandlerPriority.RpcRoutingCommandHandler)]
    public Task OnCommand(ICommand command, CommandContext context, CancellationToken cancellationToken)
    {
        var finalHandler = context.ExecutionState.Handlers.FinalHandler;
        if (finalHandler is not IMethodCommandHandler methodHandler)
            return context.InvokeRemainingHandlers(cancellationToken); // Final handler isn't a method handler

        var serviceType = methodHandler.ServiceType;
        var method = methodHandler.Method;
        if (GetRpcMethodDef(serviceType, method) is { } rpcMethodDef)
            return HandleRpcCommand();

        return context.InvokeRemainingHandlers(cancellationToken);

        async Task HandleRpcCommand() {
            // See RpcInboundCallOptionsExt.InboundCallServerInvokerDecorator,
            // it replaces the default InboundCallServerInvoker for any command method with a logic that:
            // - Creates outermost CommandContext with RpcInboundCall item set
            // - Sends the call to Commander (with this context) instead of invoking service.Method directly.
            var mustRerouteUnlessLocal = context.IsOutermost && context.Items.KeylessGet<RpcInboundCall>() is not null;
            var baseExecutionState = context.ExecutionState;
            var preFinalExecutionState = baseExecutionState.PreFinalState;
            var rerouteCount = 0;
            while (true) {
                var arguments = ArgumentList.New(command, cancellationToken);
                var peer = rpcMethodDef.RouteOutboundCall(arguments);
                if (peer.ConnectionKind is not RpcPeerConnectionKind.Local && mustRerouteUnlessLocal) {
                    // Inbound RPC calls must be routed to local peers only
                    throw RpcRerouteException.MustRerouteInbound();
                }

                var routeState = peer.Ref.RouteState;
                var shardRouteState = routeState.AsShardRouteState(rpcMethodDef);
                try {
                    peer.Ref.RouteState.RerouteIfChanged();
                    var routeChangedToken = routeState?.ChangedToken ?? default;
                    CancellationTokenSource? linkedCts = null;
                    var linkedToken = cancellationToken;
                    try {
                        if (peer.ConnectionKind is RpcPeerConnectionKind.Local) {
                            if (shardRouteState is not null)
                                // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                                routeChangedToken = await shardRouteState.WhenShardOwned(cancellationToken)
                                    .ConfigureAwait(false);

                            if (routeChangedToken.CanBeCanceled) {
                                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,
                                    routeChangedToken);
                                linkedToken = linkedCts.Token;
                            }

                            // Local call -> continue the pipeline
                            context.ExecutionState = baseExecutionState;
                        }
                        else {
                            // Remote call -> trigger just RPC call by invoking the final handler only
                            context.ExecutionState = preFinalExecutionState;
                        }

                        // MethodCommandHandler picks up RpcOutboundCallSetup (if any) and activates it
                        context.Items.KeylessSet(new RpcOutboundCallSetup(peer));
                        await context.InvokeRemainingHandlers(linkedToken).ConfigureAwait(false);
                        return;
                    }
                    catch (Exception e) when (e.IsCancellationOf(routeChangedToken) && !cancellationToken.IsCancellationRequested) {
                        throw new RpcRerouteException(routeChangedToken);
                    }
                    finally {
                        linkedCts.DisposeSilently();
                    }
                }
                catch (RpcRerouteException e) {
                    context.ResetResult();
                    if (shardRouteState is not null && !shardRouteState.IsChanged()) {
                        Log.LogWarning(e, "Re-acquiring shard ownership for command: {Command}",
                            context.UntypedCommand);
                        continue;
                    }

                    ++rerouteCount;
                    Log.LogWarning(e, "Rerouting command #{RerouteCount}: {Command}", rerouteCount,
                        context.UntypedCommand);
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
