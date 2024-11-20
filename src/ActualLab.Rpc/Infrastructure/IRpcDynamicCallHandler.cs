using ActualLab.Interception;

namespace ActualLab.Rpc.Infrastructure;

public interface IRpcDynamicCallHandler
{
    public bool IsValidCall(RpcInboundContext context, ref ArgumentList arguments, ref bool allowPolymorphism);
}
