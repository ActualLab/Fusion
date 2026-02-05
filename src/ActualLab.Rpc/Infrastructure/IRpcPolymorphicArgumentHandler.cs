using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

/// <summary>
/// Delegate for validating inbound calls with polymorphic arguments.
/// </summary>
public delegate bool RpcPolymorphicArgumentHandlerIsValidCallFunc(
    RpcInboundContext context, ref ArgumentList arguments, ref bool needsArgumentPolymorphism);

/// <summary>
/// Validates inbound RPC calls that have polymorphic arguments.
/// </summary>
public interface IRpcPolymorphicArgumentHandler
{
    public bool IsValidCall(RpcInboundContext context, ref ArgumentList arguments, ref bool needsArgumentPolymorphism);
}
