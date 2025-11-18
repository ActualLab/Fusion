using System.Diagnostics.CodeAnalysis;
using ActualLab.Interception;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;

namespace ActualLab.CommandR.Internal;

public sealed class RpcCommandRoutingHandler(IServiceProvider services) : ICommandHandler<ICommand>
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
        var rpcMethodDef = GetRpcMethodDef(serviceType, method);
        return rpcMethodDef is null
            ? context.InvokeRemainingHandlers(cancellationToken) // Not an RPC method we can (re)route
            : InvokeWithRerouting();

        async Task InvokeWithRerouting() {
            var baseExecutionState = context.ExecutionState;
            var preFinalExecutionState = baseExecutionState.PreFinalState;
            var rerouteCount = 0;
            while (true) {
                var arguments = ArgumentList.New(command, cancellationToken);
                var peer = rpcMethodDef.RouteOutboundCall(arguments);
                var isLocalCall = peer.ConnectionKind is RpcPeerConnectionKind.Local;
                var routeState = peer.Ref.RouteState;
                var shardRouteState = routeState.AsShardRouteState(rpcMethodDef);
                try {
                    peer.Ref.RouteState.ThrowIfChanged();
                    var routeChangedToken = routeState?.ChangedToken ?? default;
                    CancellationTokenSource? linkedCts = null;
                    var linkedToken = cancellationToken;
                    try {
                        if (isLocalCall) {
                            // Local call -> continue the pipeline
                            context.ExecutionState = baseExecutionState;
                            if (shardRouteState is not null)
                                // ReSharper disable once PossiblyMistakenUseOfCancellationToken
                                routeChangedToken = await shardRouteState.WhenShardOwned(cancellationToken).ConfigureAwait(false);
                        }
                        else {
                            // Remote call -> trigger just RPC call by invoking the final handler only
                            context.ExecutionState = preFinalExecutionState;
                        }

                        if (routeChangedToken.CanBeCanceled) {
                            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, routeChangedToken);
                            linkedToken = linkedCts.Token;
                        }

                        Task invokeRemainingHandlersTask;
                        using (new RpcOutboundCallSetup(peer).Activate())
                            invokeRemainingHandlersTask = context.InvokeRemainingHandlers(linkedToken);
                        await invokeRemainingHandlersTask.ConfigureAwait(false);
                        return;
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && routeChangedToken.IsCancellationRequested) {
                        throw new RpcRerouteException(routeChangedToken);
                    }
                    finally {
                        linkedCts.DisposeSilently();
                    }
                }
                catch (RpcRerouteException e) {
                    context.ResetResult();
                    if (shardRouteState is not null && !shardRouteState.IsChanged()) {
                        Log.LogWarning(e, "Re-acquiring shard ownership for command: {Command}", context.UntypedCommand);
                        continue;
                    }

                    ++rerouteCount;
                    Log.LogWarning(e, "Rerouting command #{RerouteCount}: {Command}", rerouteCount, context.UntypedCommand);
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
