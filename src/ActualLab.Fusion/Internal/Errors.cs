using System.Security;
using ActualLab.Rpc;

namespace ActualLab.Fusion.Internal;

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
        => new InvalidOperationException("Computed.Current == null.");
    public static Exception NoComputedCaptured()
        => new InvalidOperationException($"No {nameof(IComputed)} was captured.");

    public static Exception ComputedInputCategoryCannotBeSet()
        => new NotSupportedException(
            "Only IState and IAnonymousComputedInput allow to manually set Category property.");

    public static Exception ComputeServiceMethodAttributeOnStaticMethod(MethodInfo method)
        => new InvalidOperationException($"{nameof(ComputeMethodAttribute)} is applied to static method '{method}'.");
    public static Exception ComputeServiceMethodAttributeOnNonVirtualMethod(MethodInfo method)
        => new InvalidOperationException($"{nameof(ComputeMethodAttribute)} is applied to non-virtual method '{method}'.");
    public static Exception ComputeServiceMethodAttributeOnNonAsyncMethod(MethodInfo method)
        => new InvalidOperationException($"{nameof(ComputeMethodAttribute)} is applied to non-async method '{method}'.");
    public static Exception ComputeServiceMethodAttributeOnAsyncMethodReturningNonGenericTask(MethodInfo method)
        => new InvalidOperationException($"{nameof(ComputeMethodAttribute)} is applied to a method " +
            $"returning non-generic Task/ValueTask: '{method}'.");

    public static Exception InvalidContextCallOptions(CallOptions callOptions)
        => new InvalidOperationException(
            $"{nameof(ComputeContext)} with {nameof(CallOptions)} = {callOptions} cannot be used here.");

    // Rpc related

    public static Exception RpcComputeMethodCallFromTheSameService(RpcMethodDef methodDef, RpcPeerRef peerRef)
        => new InvalidOperationException(
            $"Incoming RPC compute service call to {methodDef} via '{peerRef}' " +
            "is originating from the same compute service instance. " +
            "Such calls cannot be completed, because 'local' and 'remote' calls are effectively the same " +
            "(same service instance, same arguments, so the same ComputedInput). " +
            "You must fix RpcCallRouter logic to make sure it never returns an RpcPeer connected to localhost for such calls.");

    // Session-related

    public static Exception InvalidSessionId(string parameterName)
        => new ArgumentOutOfRangeException(parameterName, "Provided Session.Id is invalid.");
    public static Exception SessionResolverSessionCannotBeSetForRootInstance()
        => new InvalidOperationException("ISessionResolver.Session can't be set for root (non-scoped) ISessionResolver.");
    public static Exception SessionUnavailable()
        => new SecurityException("The Session is unavailable.");
}
