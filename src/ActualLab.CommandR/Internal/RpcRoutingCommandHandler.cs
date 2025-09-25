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
    private RpcSafeCallRouter SafeCallRouter => field ??= RpcHub.InternalServices.SafeCallRouter;
    [field: AllowNull, MaybeNull]
    private RpcRerouteDelayer RerouteDelayer => field ??= RpcHub.InternalServices.RerouteDelayer;
    [field: AllowNull, MaybeNull]
    private ILogger Log => field ??= Services.LogFor(GetType());

    // Cache for resolved RpcMethodDef by (serviceType, method)
    private readonly ConcurrentDictionary<(Type serviceType, MethodInfo method), RpcMethodDef?> _rpcMethodCache = new();

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
            while (true) {
                try {
                    var arguments = ArgumentList.New(command, cancellationToken);
                    var peer = SafeCallRouter.Invoke(rpcMethodDef, arguments);

                    context.Items.KeylessSet(peer); // Let CommandServiceInterceptor to use it
                    context.ExecutionState = peer.ConnectionKind is RpcPeerConnectionKind.Local
                        ? baseExecutionState // Local call -> continue the pipeline
                        : preFinalExecutionState; // Remote call -> trigger just RPC call by invoking the final handler only

                    Task invokeRemainingHandlersTask;
                    using (RpcCallRouteOverride.Activate(peer))
                        invokeRemainingHandlersTask = context.InvokeRemainingHandlers(cancellationToken);
                    await invokeRemainingHandlersTask.ConfigureAwait(false);
                    return;
                }
                catch (RpcRerouteException e) {
                    context.ResetResult();
                    Log.LogWarning(e, "Rerouting command: {Command}", context.UntypedCommand);
                    await RerouteDelayer.Invoke(cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    // Private methods

    private RpcMethodDef? GetRpcMethodDef(Type serviceType, MethodInfo method)
        => _rpcMethodCache.GetOrAdd((serviceType, method),
            static (key, self) => {
                var (serviceType, method) = key;
                if (!self.RpcHub.Configuration.Services.TryGetValue(serviceType, out var serviceBuilder))
                    return null; // Not an RPC service

                var mode = serviceBuilder.Mode;
                if (!mode.IsAnyClient() && !mode.IsAnyDistributed())
                    return null; // Not a client or distributed service

                var serviceDef = self.RpcHub.ServiceRegistry[serviceType];
                var methodDef = serviceDef.GetOrFindMethod(method);
                if (methodDef is null)
                    return null; // The handling method isn't exposed via RPC

                if (methodDef.ParameterTypes is not { Length: 2 }) {
                    // A bug in user code: the method must have just 2 parameters
                    self.Log.LogError(
                        "RpcMethodDef matching '{ServiceType}.{Method}' must have 2 parameters instead of {ParameterCount}",
                        serviceType.GetName(), method.Name, methodDef.ParameterTypes);
                    return null;
                }

                return methodDef;
            }, this);
}
