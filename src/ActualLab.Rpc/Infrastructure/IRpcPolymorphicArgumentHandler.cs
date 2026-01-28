using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public delegate bool RpcPolymorphicArgumentHandlerIsValidCallFunc(
    RpcInboundContext context, ref ArgumentList arguments, ref bool needsArgumentPolymorphism);

public interface IRpcPolymorphicArgumentHandler
{
    public bool IsValidCall(RpcInboundContext context, ref ArgumentList arguments, ref bool needsArgumentPolymorphism);
}
