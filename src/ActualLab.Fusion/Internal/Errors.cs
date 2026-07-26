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

    public static Exception ComputeServiceWithCommandHandlersMustBeSingleton(Type serviceType)
        => new InvalidOperationException(
            $"Compute service '{serviceType.GetName()}' has command handlers and must be registered as a singleton: " +
            "invalidation replay cannot resolve scoped services.");

    // Deferred invalidation

    public static Exception NoDeferInvalidationScope()
        => new InvalidOperationException(
            $"{nameof(Invalidation)}.{nameof(Invalidation.Defer)} is called outside of a " +
            $"{nameof(DeferInvalidationScope)}.");
    public static Exception NoDeferInvalidationRecorder()
        => new InvalidOperationException(
            $"{nameof(CallOptions)}.{nameof(CallOptions.DeferInvalidate)} is set, but no " +
            $"{nameof(DeferInvalidationScope)} is harvesting.");
    public static Exception DeferInvalidationInsideInvalidationPass()
        => new InvalidOperationException(
            "Deferred invalidation cannot be used while an invalidation pass is active.");
    public static Exception DeferInvalidationRequiresDeferredMode(InvalidationMode mode)
        => new InvalidOperationException(
            $"{nameof(Invalidation)}.{nameof(Invalidation.Defer)} requires " +
            $"{nameof(InvalidationMode)}.{nameof(InvalidationMode.Local)} or " +
            $"{nameof(InvalidationMode)}.{nameof(InvalidationMode.Replicated)}, but the handler is {mode}.");
    public static Exception ReplicatedInvalidationRequiresStoredOperation(Type? scopeType)
        => new InvalidOperationException(
            $"{nameof(InvalidationMode)}.{nameof(InvalidationMode.Replicated)} requires an operation scope " +
            $"that stores its operation, so the recorded invalidation calls reach the other hosts, but " +
            $"'{scopeType?.GetName() ?? "none"}' doesn't. Use {nameof(InvalidationMode)}." +
            $"{nameof(InvalidationMode.Local)} instead, or store the operation.");
    public static Exception InvalidationModeOverrideIsNotAllowed(
        Type serviceType, InvalidationMode declaredMode, InvalidationMode overrideMode)
        => new InvalidOperationException(
            $"Cannot override {nameof(InvalidationMode)} of '{serviceType.GetName()}' from " +
            $"{declaredMode} to {overrideMode}: only Local <-> Replicated overrides are allowed.");

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
