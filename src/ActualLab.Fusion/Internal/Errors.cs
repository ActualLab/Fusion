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
            "invalidation replay cannot resolve scoped services.");

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
