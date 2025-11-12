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
                try {
                    var arguments = ArgumentList.New(command, cancellationToken);
                    var peer = rpcMethodDef.RouteOutboundCall(arguments);
                    peer.ThrowIfRerouted();

                    context.ExecutionState = peer.ConnectionKind is RpcPeerConnectionKind.Local
                        ? baseExecutionState // Local call -> continue the pipeline
                        : preFinalExecutionState; // Remote call -> trigger just RPC call by invoking the final handler only

                    Task invokeRemainingHandlersTask;
                    using (new RpcOutboundCallSetup(peer).Activate())
                        invokeRemainingHandlersTask = context.InvokeRemainingHandlers(cancellationToken);
                    await invokeRemainingHandlersTask.ConfigureAwait(false);
                    return;
                }
                catch (RpcRerouteException e) {
                    context.ResetResult();
                    ++rerouteCount;
                    Log.LogWarning(e, "Rerouting command #{RerouteCount}: {Command}", rerouteCount, context.UntypedCommand);
                    await RpcHub.InternalServices.OutboundCallOptions.ReroutingDelayFactory
                        .Invoke(rerouteCount, cancellationToken)
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
