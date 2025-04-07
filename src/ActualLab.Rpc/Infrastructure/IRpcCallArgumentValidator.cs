using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public interface IRpcCallArgumentValidator
{
    public bool IsValidCall(RpcInboundContext context, ref ArgumentList arguments, ref bool needsArgumentPolymorphism);
}
