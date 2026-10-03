using System.Security;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Internal;

/// <summary>
/// Factory methods for Fusion-specific exceptions.
/// </summary>
public static class Errors
{
    public static Exception WrongComputedState(
        ConsistencyState expectedState, ConsistencyState state)
        => new InvalidOperationException(
            $"Wrong Computed.State: expected {expectedState}, was {state}.");
    public static Exception WrongComputedState(ConsistencyState state)
        => new InvalidOperationException(
            $"Wrong Computed.State: {state}.");

    public static Exception CurrentComputedIsNull()
        => new InvalidOperationException("Computed.Current is null.");
    public static Exception NoComputedCaptured()
        => new InvalidOperationException($"No {nameof(Computed)} was captured.");

    public static Exception ComputedInputCategoryCannotBeSet()
        => new NotSupportedException(
            "Only IState and IAnonymousComputedInput allow to manually set Category property.");

    public static Exception ComputeMethodAttributeOnStaticMethod(MethodInfo method)
        => new InvalidOperationException($"{nameof(ComputeMethodAttribute)} is applied to static method '{method}'.");
    public static Exception ComputeMethodAttributeOnNonVirtualMethod(MethodInfo method)
        => new InvalidOperationException($"{nameof(ComputeMethodAttribute)} is applied to non-virtual method '{method}'.");
    public static Exception ComputeMethodAttributeOnNonAsyncMethod(MethodInfo method)
        => new InvalidOperationException($"{nameof(ComputeMethodAttribute)} is applied to non-async method '{method}'.");
    public static Exception ComputeMethodAttributeOnAsyncMethodReturningNonGenericTask(MethodInfo method)
        => new InvalidOperationException($"{nameof(ComputeMethodAttribute)} is applied to a method " +
            $"returning non-generic Task/ValueTask: '{method}'.");
    public static Exception ComputeMethodAttributeOnAsyncMethodReturningRpcNoWait(MethodInfo method)
        => new InvalidOperationException($"{nameof(ComputeMethodAttribute)} is applied to a method " +
            $"returning {nameof(RpcNoWait)}: '{method}'.");

    public static Exception ConsolidationOnDistributedServiceMethod(Type serviceType, MethodInfo method)
        => new InvalidOperationException(
            $"{nameof(ComputedOptions.ConsolidationDelay)} cannot be used on " +
            $"'{serviceType.GetName()}.{method.Name}', because it's an RPC-exposed method of a " +
            $"{nameof(RpcServiceMode)}.{nameof(RpcServiceMode.Distributed)} service: even when the routing " +
            "resolves to the local peer, such calls are served by RemoteComputeMethodFunction, " +
            "which always produces a plain ComputeMethodComputed - so the consolidation is silently ignored. " +
            "It can't be routed either: consolidation recomputes its source via a local ComputeMethodFunction, " +
            "bypassing the RPC routing, and its shape is fixed at method-def construction time, " +
            "while the routing is per-call and may flip when a shard moves. " +
            "Move the consolidation to a non-RPC-visible (e.g. protected virtual) compute method " +
            $"and make '{method.Name}' derive its result from it. " +
            $"{nameof(ComputedOptions.ConsolidationDelay)} is fine on " +
            $"{nameof(RpcServiceMode.Local)}, {nameof(RpcServiceMode.Server)}, " +
            $"{nameof(RpcServiceMode.Client)} and {nameof(RpcServiceMode.ServerAndClient)} services.");

    public static Exception ConsolidationComparerWithoutConsolidationDelay(Type serviceType, MethodInfo method)
        => new InvalidOperationException(
            $"{nameof(ComputeMethodAttribute.ConsolidationComparer)} is set on " +
            $"'{serviceType.GetName()}.{method.Name}', but its {nameof(ComputedOptions.ConsolidationDelay)} isn't, " +
            "so the comparer would never be used. " +
            $"Set {nameof(ComputedOptions.ConsolidationDelay)} as well, or remove the comparer.");

    public static Exception ConsolidationComparerMustImplementIEqualityComparer(
        Type comparerType, Type valueType, MethodInfo method)
        => new InvalidOperationException(
            $"{nameof(ComputeMethodAttribute.ConsolidationComparer)} type '{comparerType.GetName()}' " +
            $"used on '{method.DeclaringType?.GetName()}.{method.Name}' must implement " +
            $"IEqualityComparer<{valueType.GetName()}>.");

    public static Exception ConsolidationComparerMustHaveParameterlessConstructor(
        Type comparerType, MethodInfo method)
        => new InvalidOperationException(
            $"{nameof(ComputeMethodAttribute.ConsolidationComparer)} type '{comparerType.GetName()}' " +
            $"used on '{method.DeclaringType?.GetName()}.{method.Name}' must be a non-abstract type " +
            "with a public parameterless constructor.");

    public static Exception ComputeServiceWithCommandHandlersMustBeSingleton(Type serviceType)
        => new InvalidOperationException(
            $"Compute service '{serviceType.GetName()}' has command handlers and must be registered as a singleton: " +
            "deferred invalidation applies its calls from the root provider, which cannot resolve scoped services.");

    // Deferred invalidation

    public static Exception NoDeferredInvalidationContext()
        => new InvalidOperationException(
            $"{nameof(Invalidation)}.{nameof(Invalidation.Defer)} is called outside of a " +
            $"{nameof(DeferredInvalidationContext)}.");

    public static Exception ComputeContextDoesNotCaptureInvalidation(CallOptions callOptions)
        => new InvalidOperationException(
            $"An invalidation can be recorded only by a {nameof(ComputeContext)} with " +
            $"{nameof(CallOptions)}.{nameof(CallOptions.CaptureInvalidation)} set, and this one has " +
            $"{callOptions}.");
    public static Exception CommandCannotRunDuringInvalidation(ICommand command)
        => new InvalidOperationException(
            $"Command '{command.GetType().GetName()}' is started while an invalidation pass is active.");

    public static Exception DeferredInvalidationBlockTypeIsNotSupported(Type blockType)
        => new InvalidOperationException(
            $"A deferred invalidation block must be an Action or a Func<Task>, but it is '{blockType.GetName()}'.");
    public static Exception DeferredInvalidationContextIsAlreadyActive()
        => new InvalidOperationException(
            $"A {nameof(DeferredInvalidationContext)} is already active. One operation gets one of them, "
            + $"so pass requireOutermost: false to {nameof(DeferredInvalidationContext.Activate)} "
            + "if a nested one is really what you want.");
    public static Exception DeferredInvalidationContextIsClosed()
        => new InvalidOperationException(
            $"This operation's deferred invalidation blocks are already final, so the one being added " +
            $"now would never run. {nameof(Invalidation)}.{nameof(Invalidation.Defer)} has to be " +
            $"called while the command handler is still running - not from a task it left behind, " +
            $"and not from inside another block.");

    public static Exception DeferredInvalidationContextIsNotClosed()
        => new InvalidOperationException(
            $"A {nameof(DeferredInvalidationContext)}'s blocks can only be read once it's closed, " +
            $"so that what's read is all there will ever be - which means after the command handler " +
            $"returned. A handler that commits its own operation scope and defers invalidation " +
            $"cannot do both: the row is written before the blocks are final.");

    public static Exception DeferredInvalidationContextCannotBeReactivated()
        => new InvalidOperationException(
            $"A {nameof(DeferredInvalidationContext)} can be activated once: its blocks belong to " +
            $"one operation.");

    public static Exception DeferredInvalidationInsideInvalidationPass()
        => new InvalidOperationException(
            "Deferred invalidation cannot be used while an invalidation pass is active.");
    public static Exception DeferredInvalidationModeConflict(
        DeferredInvalidationMode decidedMode, DeferredInvalidationMode requestedMode)
        => new InvalidOperationException(
            $"This operation's invalidation is already deferred as " +
            $"{nameof(DeferredInvalidationMode)}.{decidedMode:G}, and a command in it needs " +
            $"{nameof(DeferredInvalidationMode)}.{requestedMode:G}. " +
            "All commands running as a single operation must agree on the mode; " +
            "call the ones that don't with isOutermost: true, so each gets its own operation.");

    public static Exception DeferredInvalidationModeIsUndeclared(Type? type)
        => new InvalidOperationException(
            $"{nameof(Invalidation)}.{nameof(Invalidation.Defer)} needs a " +
            $"[{nameof(DeferredInvalidationModeAttribute)}] on " +
            (type is null
                ? "the command handler that calls it, and there is no command running here."
                : $"'{type.GetName()}', its method, or the service type it's registered as.") +
            $" There is no default: how far an invalidation has to reach is the handler's call.");

    public static Exception DeferredInvalidationModeResolverRequired()
        => new InvalidOperationException(
            $"{nameof(DeferredInvalidationContext)}.{nameof(DeferredInvalidationContext.ModeResolver)} " +
            $"is required to add a block to a context that has no " +
            $"{nameof(DeferredInvalidationContext.Mode)} yet.");
    public static Exception DeferredInvalidationRequiresDeferredMode(DeferredInvalidationMode mode)
        => new InvalidOperationException(
            $"{nameof(Invalidation)}.{nameof(Invalidation.Defer)} requires " +
            $"{nameof(DeferredInvalidationMode)}.{nameof(DeferredInvalidationMode.Local)}, " +
            $"{nameof(DeferredInvalidationMode)}.{nameof(DeferredInvalidationMode.Replicated)}, or " +
            $"{nameof(DeferredInvalidationMode)}.{nameof(DeferredInvalidationMode.Distributed)}, " +
            $"but the handler is {mode}.");
    public static Exception DeferredInvalidationRequiresStoredOperation(
        DeferredInvalidationMode mode, Type? scopeType)
        => new InvalidOperationException(
            $"{nameof(DeferredInvalidationMode)}.{mode} requires an operation scope " +
            $"that stores its operation, so the recorded invalidation calls survive this host, but " +
            $"'{scopeType?.GetName() ?? "none"}' doesn't. Use {nameof(DeferredInvalidationMode)}." +
            $"{nameof(DeferredInvalidationMode.Local)} instead, or store the operation.");
    public static Exception ReplicatedInvalidationRequiresOperationRow()
        => new InvalidOperationException(
            $"{nameof(DeferredInvalidationMode)}.{nameof(DeferredInvalidationMode.Replicated)} needs the " +
            $"operation row every host reads, but this operation stores an event row instead, which " +
            $"only one host claims. Use {nameof(DeferredInvalidationMode)}." +
            $"{nameof(DeferredInvalidationMode.Distributed)} instead, or store the operation.");

    public static Exception InvalidContextCallOptions(CallOptions callOptions)
        => new InvalidOperationException(
            $"{nameof(ComputeContext)} with {nameof(CallOptions)} = {callOptions} cannot be used here.");

    // Rpc related

    public static Exception RemoteComputeMethodCallFromTheSameService(RpcMethodDef methodDef, RpcRef rpcRef)
        => new InvalidOperationException(
            $"Incoming RPC compute service call to {methodDef} via '{rpcRef}' " +
            "is originating from the same compute service instance. " +
            "Such calls cannot be completed, because 'local' and 'remote' calls are effectively the same " +
            "(same service instance, same arguments, so the same ComputedInput). " +
            "You must fix RpcCallRouter logic to make sure it never returns " +
            "an RpcRef resolving to the localhost for such calls.");

    // Session-related

    public static Exception InvalidSessionId(string parameterName)
        => new ArgumentOutOfRangeException(parameterName, "Provided Session.Id is invalid.");
    public static Exception SessionResolverSessionCannotBeSetForRootInstance()
        => new InvalidOperationException("ISessionResolver.Session can't be set for root (non-scoped) ISessionResolver.");
    public static Exception SessionUnavailable()
        => new SecurityException("The Session is unavailable.");
}
